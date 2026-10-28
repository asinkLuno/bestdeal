using System;
using System.Collections.Generic;

namespace BestDeal;

/// <summary>分组背包：每组最多选一件，以预算为容量优化总售价；不依赖 Unity 或游戏类型。</summary>
internal static class GroupedKnapsack
{
    internal static List<T> Select<T>(List<List<T>> groups, long budget, Func<T, long> priceOf)
    {
        if (budget <= 0) return new List<T>();
        // 所有组的最高价都买得起时无需展开金额状态（包括无限预算）。
        var all = new List<T>();
        long remaining = budget;
        bool fits = true;
        foreach (var group in groups)
        {
            long maximum = 0;
            T best = default;
            foreach (var item in group)
            {
                long price = priceOf(item);
                if (price > maximum) { maximum = price; best = item; }
            }
            if (maximum > remaining) { fits = false; break; }
            remaining -= maximum;
            if (maximum > 0) all.Add(best);
        }
        if (fits) { all.Reverse(); return all; }
        if (budget > 2_000_000) return SelectSparse(groups, budget, priceOf);
        int cap = (int)budget;
        int Weight(long price) => price <= 0 ? cap + 1 : (int)Math.Min(cap + 1L, price);
        var dp = new long[cap + 1];
        var choice = new int[groups.Count][];

        for (int g = 0; g < groups.Count; g++)
        {
            var items = groups[g];
            var w = new int[items.Count];
            for (int k = 0; k < items.Count; k++) w[k] = Weight(priceOf(items[k]));

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
                        long cand = prev[j - w[k]] + priceOf(items[k]);
                        if (cand > best) { best = cand; bestK = k; }
                    }
                }
                dp[j] = best;
                row[j] = bestK;
            }
            choice[g] = row;
        }

        var picked = new List<T>();
        int pos = cap;
        for (int g = groups.Count - 1; g >= 0; g--)
        {
            int k = choice[g][pos];
            if (k >= 0)
            {
                var it = groups[g][k];
                picked.Add(it);
                pos -= Weight(priceOf(it));
            }
        }

        return picked;
    }

    sealed class Path<T>
    {
        internal T Item;
        internal Path<T> Previous;
    }

    // 大预算保留精确可达金额，避免向下取整让超预算组合混入。
    // 每组只从上一组的快照转移；相同金额保留已有路径，确保平局稳定。
    static List<T> SelectSparse<T>(List<List<T>> groups, long budget, Func<T, long> priceOf)
    {
        var states = new SortedDictionary<long, Path<T>> { [0] = null };
        foreach (var group in groups)
        {
            var previous = new List<KeyValuePair<long, Path<T>>>(states);
            foreach (var state in previous)
                foreach (var item in group)
                {
                    long price = priceOf(item);
                    if (price <= 0 || price > budget - state.Key) continue;
                    long sum = state.Key + price;
                    if (!states.ContainsKey(sum))
                        states.Add(sum, new Path<T> { Item = item, Previous = state.Value });
                }
        }
        Path<T> best = null;
        foreach (var state in states) best = state.Value;
        var picked = new List<T>();
        for (var path = best; path != null; path = path.Previous) picked.Add(path.Item);
        return picked;
    }

}
