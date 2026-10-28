using System;
using System.Collections.Generic;
using System.Linq;

namespace BestDeal;

/// <summary>
/// 报价组合（只读，不改库存）：扫描各售卖区收集这位顾客会买的候选，
/// 按"分组背包"算出让售出总额最大的组合，生成 HUD 文案。
/// 价格只认 PriceProbe 探到的游戏实算价；预算来自顾客自身。
/// </summary>
public static class QuoteOptimizer
{
    private static int _unpriced; // 没探到游戏真价、未参与推荐的候选数

    class Cand
    {
        public GameItem Item;
        public string Name;
        public string GroupKey; // 同类型标识（identifier；空则用名字）
        public long Count;
        public long Price;   // 游戏自己算出来的最终成交价（摆上柜台探测得到）
        public bool OnCounter;  // 已经摆在后柜台上（玩家不用再动）
    }

    public static void Run(StoreClient c)
    {
        if (c == null)
        {
            Result.Set("BestDeal", new[] { "暂无顾客（等顾客上门后自动提示）" });
            return;
        }

        var name = string.IsNullOrEmpty(c.displayName) ? "?" : c.displayName;
        bool requireVariety = c.isRequireVariety;
        var cands = Collect(c, requireVariety, needPrice: true);
        if (cands.Count == 0)
        {
            // 一样都没定价：可能只是还没探测（本来就会自动探），别急着说"他不买"
            string note = PriceProbe.Running
                ? "正在逐件摆上柜台探测价格…（探完自动更新）"
                : (_unpriced > 0 ? $"候选 {_unpriced} 件，价格还没探到（自动重探中）"
                                 : "他不会买你店里的任何东西");
            Result.Set($"BestDeal — {name}", new[]
            {
                $"顾客：{name}  预算：{GetBudgetSafe(c)}  类别：{ClientDemand.TagsSummary(c)}",
                note,
            });
            return;
        }

        long budget = GetBudgetSafe(c);
        if (budget <= 0) budget = long.MaxValue / 4;

        // 分组：要求不重复 → 同类最多 1 件；否则每件独立
        List<List<Cand>> groups = requireVariety
            ? cands.GroupBy(x => x.GroupKey).Select(g => g.ToList()).ToList()
            : cands.Select(x => new List<Cand> { x }).ToList();

        // 0/1 分组背包：容量=预算，每组最多取 1 件，最大化售出总额。
        // 重量必须用 cap+1 表示「放不下」——若截断到 cap，超预算的物品会被当成"刚好花光预算"，
        // 结果反而比预算内的物品更"划算"而被选中（顾客根本买不起）。
        long scale = Math.Max(1, budget / 2_000_000 + 1);
        int cap = (int)(budget / scale);
        int Weight(long price) => (int)Math.Min(cap + 1L, price / scale);
        var dp = new long[cap + 1];
        var choice = new int[groups.Count][];

        for (int g = 0; g < groups.Count; g++)
        {
            var items = groups[g];
            var w = new int[items.Count];
            for (int k = 0; k < items.Count; k++) w[k] = Weight(items[k].Price);

            var prev = (long[])dp.Clone(); // 上一组的最优值（本组只能取 1 件）
            var row = new int[cap + 1];
            for (int j = 0; j <= cap; j++)
            {
                long best = prev[j];
                int bestK = -1;
                for (int k = 0; k < items.Count; k++)
                {
                    if (w[k] <= j)
                    {
                        long cand = prev[j - w[k]] + items[k].Price;
                        if (cand > best) { best = cand; bestK = k; }
                    }
                }
                dp[j] = best;
                row[j] = bestK;
            }
            choice[g] = row;
        }

        var picked = new List<Cand>();
        int pos = cap;
        for (int g = groups.Count - 1; g >= 0; g--)
        {
            int k = choice[g][pos];
            if (k >= 0)
            {
                var it = groups[g][k];
                picked.Add(it);
                pos -= Weight(it.Price);
            }
        }

        string head = $"顾客：{name}  预算：{GetBudgetSafe(c)}  类别：{ClientDemand.TagsSummary(c)}" +
                      (requireVariety ? "  [要求不重复]" : "");
        string skippedNote = _unpriced > 0 ? $"（另 {_unpriced} 件未探到价已跳过）" : "";

        // 候选都在预算之外：顾客一件也买不起（别推荐超预算的东西）
        if (picked.Count == 0)
        {
            Result.Set($"BestDeal — {name}", new[]
            {
                head,
                $"候选 {cands.Count} 件，但最便宜的也要 {cands.Min(x => x.Price)}，超出预算，他一件也买不起",
            });
            return;
        }

        long total = 0;
        int onCounter = 0;
        foreach (var p in picked)
        {
            total += p.Price;
            if (p.OnCounter) onCounter++;
        }

        var lines = new List<string>
        {
            head,
            $"可售出总额：{total}" +
            (GetBudgetSafe(c) > 0 ? $"（剩预算 {Math.Max(0, budget - total)}）" : "") +
            $"，候选 {cands.Count} 件" + skippedNote,
        };
        foreach (var p in picked)
            lines.Add(p.OnCounter
                ? $"✔ 已在柜台：{p.Name} ×{p.Count} = {p.Price}"
                : $"→ 拿上柜台：{p.Name} ×{p.Count} = {p.Price}");
        lines.Add(onCounter > 0
            ? $"共 {picked.Count} 件，其中 {onCounter} 件已在柜台"
            : $"共 {picked.Count} 件");

        Result.Set($"BestDeal — {name}", lines);
    }

    static long GetBudgetSafe(StoreClient c)
    {
        try { return c.GetBudget(); } catch { return 0; }
    }

    /// <summary>候选物品（给价格探测排队用：与 Collect 同一套筛选，但**不要求已经探到价**）。</summary>
    internal static List<GameItem> CollectCandidates(StoreClient c, bool requireVariety)
    {
        // 探测按"游戏估值从高到低"排队：超过上限时先探值钱的，尾部的低价值物品不参与推荐
        return Collect(c, requireVariety, needPrice: false)
            .OrderByDescending(x => ValueOf(x.Item))
            .Select(x => x.Item)
            .ToList();
    }

    static long ValueOf(GameItem gi)
    {
        long v = 0;
        try { v = gi.GetNegociatedValue(); } catch { }
        if (v <= 0) v = gi.unitValue;
        return v;
    }

    /// <summary>
    /// 扫描各售卖/仓储区收集物品；只扫玩家能拿去卖的存储区。
    /// needPrice=true（HUD 推荐）只保留已探到游戏价的物品；needPrice=false（给探测排队）不做价格过滤——
    /// 否则就成了"要先探到价，才肯去探价"的死结（v1.4.0 的实际 bug：探测一步都没跑过）。
    /// </summary>
    static List<Cand> Collect(StoreClient c, bool requireVariety, bool needPrice)
    {
        var result = new List<Cand>();
        var seen = new HashSet<GameItem>();
        _unpriced = 0;

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

                // 价格只认"摆上柜台探测"到的游戏真价；没探到（超出探测上限等）就不推荐它
                long price = PriceProbe.PriceFor(gi);
                if (price <= 0 && needPrice)
                {
                    _unpriced++;
                    continue;
                }

                var gk = gi.identifier;
                if (string.IsNullOrEmpty(gk)) gk = gi.name ?? "?";
                result.Add(new Cand
                {
                    Item = gi,
                    Name = $"{gi.name}[{invName}]",
                    GroupKey = gk,
                    Count = cnt,
                    Price = price,
                    OnCounter = ReferenceEquals(inv, ShopInventories.BackCounter),
                });
            }
        }
        return result;
    }
}
