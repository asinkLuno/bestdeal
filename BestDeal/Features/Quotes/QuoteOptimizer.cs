using System;
using System.Collections.Generic;
using System.Linq;

namespace BestDeal;

/// <summary>
/// 报价组合（只读，不改库存）：扫描各售卖区收集这位顾客会买的候选，
/// 按"分组背包"算出让售出总额最大的组合，生成 HUD 文案。
/// 价格用 PriceProbe 内存模拟读出的游戏实算价（与真实摆上柜台一致）；预算来自顾客自身。
/// </summary>
public static class QuoteOptimizer
{
    class Cand
    {
        public GameItem Item;
        public string Name;
        public string GroupKey; // 同类型标识（identifier；空则用名字）
        public long Count;
        public long Price;   // 游戏自己算出来的最终成交价（模拟摆上柜台读得）
        public bool OnCounter;  // 已经摆在后柜台上（玩家不用再动）
    }

    public static void Run(StoreClient c)
    {
        if (!FeatureSettings.Quotes)
        {
            HudContent.ClearQuote();
            return;
        }
        if (c == null)
        {
            HudContent.ClearQuote();
            return;
        }

        HudContent.ClearQuote();
        HudContent.QuoteSummary.Add(HeadLine(c));
        HudContent.QuoteSummary.Add(L10n.Format("HeatFilter", FeatureSettings.MaxStolenHeat));
        bool requireVariety = c.isRequireVariety;
        var cands = Collect(c, requireVariety);
        if (cands.Count == 0)
        {
            HudContent.QuoteItems.Add(L10n.Text("NoItems"));
            return;
        }

        long budget = ClientDemand.GetBudgetSafe(c);
        if (!c.useClientBudget) budget = long.MaxValue;
        else budget = Math.Max(0, budget);

        // 分组：要求不重复 → 同类最多 1 件；否则每件独立
        List<List<Cand>> groups = requireVariety
            ? cands.GroupBy(x => x.GroupKey).Select(g => g.ToList()).ToList()
            : cands.Select(x => new List<Cand> { x }).ToList();

        var picked = GroupedKnapsack.Select(groups, budget, x => x.Price);
        PreferCounterEquivalents(picked, cands);

        // 候选都在预算之外：顾客一件也买不起（别推荐超预算的东西）
        if (picked.Count == 0)
        {
            HudContent.QuoteItems.Add(L10n.Format("OverBudget", cands.Min(x => x.Price)));
            return;
        }

        long total = 0;
        foreach (var p in picked)
            total += p.Price;

        HudContent.QuoteSummary.Add(L10n.Format("Total", total) +
            (c.useClientBudget ? L10n.Format("Remaining", Math.Max(0, budget - total)) : ""));
        foreach (bool onCounter in new[] { false, true })
        {
            var items = picked.Where(x => x.OnCounter == onCounter).ToList();
            if (items.Count == 0) continue;
            HudContent.QuoteItems.Add(L10n.Text(onCounter ? "OnCounter" : "ToCounter"));
            foreach (var p in items)
            {
                HudContent.QuoteItems.Add($"{(onCounter ? "✓" : "•")} {p.Name} ×{p.Count} = {p.Price}");
                if (FeatureSettings.Authenticity)
                    foreach (var warning in Authenticity.FakeWarnings(p.Item))
                        HudContent.QuoteItems.Add($"    ⚠ {warning}");
            }
        }
    }

    /// <summary>清单首行（标题里已有顾客名，正文不重复）：预算 + 想买类别。</summary>
    static string HeadLine(StoreClient c) =>
        L10n.Format("Demand", c.useClientBudget ? ClientDemand.GetBudgetSafe(c).ToString() : L10n.Text("Any"), ClientDemand.TagsSummary(c)) +
        (c.isRequireVariety ? L10n.Text("NoDuplicates") : "");

    // 保留最优金额和数量，用柜台上的等价同款替换推荐实例。
    // 先占用已经选中的柜台物品，避免一件实物被多条推荐重复使用。
    static void PreferCounterEquivalents(List<Cand> picked, List<Cand> candidates)
    {
        var used = new HashSet<GameItem>(picked.Where(x => x.OnCounter).Select(x => x.Item));
        for (int i = 0; i < picked.Count; i++)
        {
            var original = picked[i];
            if (original.OnCounter) continue;
            var warnings = Authenticity.FakeWarnings(original.Item);
            var replacement = candidates.FirstOrDefault(x => x.OnCounter
                && !used.Contains(x.Item)
                && x.GroupKey == original.GroupKey
                && x.Count == original.Count
                && x.Price == original.Price
                && warnings.SequenceEqual(Authenticity.FakeWarnings(x.Item)));
            if (replacement == null) continue;
            picked[i] = replacement;
            used.Add(replacement.Item);
        }
    }

    /// <summary>
    /// 扫描各售卖/仓储区收集物品；只扫玩家能拿去卖的存储区。
    /// 每件候选当场用 PriceProbe 模拟读出游戏实价（同步、不动库存），读不出价（失败/零价值）的不推荐。
    /// </summary>
    static List<Cand> Collect(StoreClient c, bool requireVariety)
    {
        var result = new List<Cand>();
        var seen = new HashSet<GameItem>();

        foreach (var (inv, _) in ShopInventories.Enumerate())
        {
            var items = inv.childItems;
            if (items == null) continue;
            foreach (var gi in items)
            {
                if (gi == null) continue;
                // 按实际热度过滤；阈值包含边界，无赃物状态的商品视为无热度。
                if (StolenHelper.IsStolenItem(gi)
                    && StolenHelper.GetFoundChance(gi) > FeatureSettings.MaxStolenHeat) continue;

                bool ok;
                try { ok = c.IsClientBuyingThisItem(gi); } catch { ok = false; }
                if (!ok) continue;

                if (!seen.Add(gi)) continue;

                // 游戏自己的重复判定：已在多样性列表里 → 这位顾客不会再要同类
                bool tracked = false;
                try { tracked = c.IsItemInVarietyList(gi); } catch { }
                if (requireVariety && tracked) continue;

                long cnt = gi.unitCount;
                if (cnt <= 0) cnt = 1;

                // 价格 = 游戏自己算的最终成交价（模拟摆上柜台读得）
                long price = PriceProbe.ReadPrice(c, gi);
                if (price <= 0) continue;

                var gk = gi.identifier;
                if (string.IsNullOrEmpty(gk)) gk = gi.name ?? "?";
                result.Add(new Cand
                {
                    Item = gi,
                    Name = gi.name,
                    GroupKey = gk,
                    Count = cnt,
                    Price = price,
                    OnCounter = ShopInventories.IsCounter(inv),
                });
            }
        }
        // 柜台搬运会改变枚举顺序；按实例 ID 固定同价时的选择。
        return result.OrderBy(x => x.GroupKey, StringComparer.Ordinal)
            .ThenBy(x => x.Item.GetUniqueID()).ToList();
    }
}
