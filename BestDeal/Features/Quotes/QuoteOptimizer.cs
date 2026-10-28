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
        if (c == null)
        {
            HudContent.Set("BestDeal", new[] { "暂无顾客" });
            return;
        }

        var name = string.IsNullOrEmpty(c.displayName) ? "?" : c.displayName;
        bool requireVariety = c.isRequireVariety;
        var cands = Collect(c, requireVariety);
        if (cands.Count == 0)
        {
            HudContent.Set($"BestDeal — {name}", new[] { HeadLine(c), "无合适商品" });
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

        // 候选都在预算之外：顾客一件也买不起（别推荐超预算的东西）
        if (picked.Count == 0)
        {
            HudContent.Set($"BestDeal — {name}", new[]
            {
                $"超预算（最低 {cands.Min(x => x.Price)}）",
            });
            return;
        }

        long total = 0;
        foreach (var p in picked)
            total += p.Price;

        var lines = new List<string>
        {
            HeadLine(c),
            $"合计：{total}" +
            (c.useClientBudget ? $"（余额 {Math.Max(0, budget - total)}）" : ""),
        };
        for (int i = 0; i < picked.Count; i++)
        {
            var p = picked[i];
            lines.Add(!p.OnCounter ? $"→ 上柜：{p.Name} ×{p.Count} = {p.Price}"
                : $"✔ 在柜：{p.Name} ×{p.Count} = {p.Price}");
            // 赝品警告：理由是游戏自己的本地化文本（顾客识破时说的原话），mod 不硬编码
            foreach (var w in Authenticity.FakeWarnings(p.Item))
                lines.Add($"   {w}");
        }

        HudContent.Set($"BestDeal — {name}", lines);
    }

    /// <summary>清单首行（标题里已有顾客名，正文不重复）：预算 + 想买类别。</summary>
    static string HeadLine(StoreClient c) =>
        $"预算：{ClientDemand.GetBudgetSafe(c)}  类别：{ClientDemand.TagsSummary(c)}" +
        (c.isRequireVariety ? "  [不重复]" : "");

    /// <summary>
    /// 扫描各售卖/仓储区收集物品；只扫玩家能拿去卖的存储区。
    /// 每件候选当场用 PriceProbe 模拟读出游戏实价（同步、不动库存），读不出价（失败/零价值）的不推荐。
    /// </summary>
    static List<Cand> Collect(StoreClient c, bool requireVariety)
    {
        var result = new List<Cand>();
        var seen = new HashSet<GameItem>();

        foreach (var (inv, zone) in ShopInventories.Enumerate())
        {
            string invName = string.IsNullOrEmpty(inv.identifier) ? zone : inv.identifier;
            var items = inv.childItems;
            if (items == null) continue;
            foreach (var gi in items)
            {
                if (gi == null) continue;

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
                    Name = $"{gi.name}[{invName}]",
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
