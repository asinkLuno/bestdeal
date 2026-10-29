using BestDeal;

// 失败时进程以非零状态退出。
static void Check(long budget, long[][] prices)
{
    var groups = prices.Select((row, group) => row.Select(price => (group, price)).ToList()).ToList();
    var picked = GroupedKnapsack.Select(groups, budget, x => x.price);
    var total = picked.Sum(x => x.price);

    long Exhaustive(int group, long remaining)
    {
        if (group == prices.Length) return 0;
        long best = Exhaustive(group + 1, remaining);
        foreach (long price in prices[group])
            if (price <= remaining)
                best = Math.Max(best, price + Exhaustive(group + 1, remaining - price));
        return best;
    }

    long expected = Exhaustive(0, budget);
    if (total != expected || total > budget || picked.Select(x => x.group).Distinct().Count() != picked.Count)
        throw new Exception($"预算 {budget}：得到 {total}，期望 {expected}；分组约束或预算不满足");
}

Check(10, Array.Empty<long[]>());
Check(0, new[] { new long[] { 1 } });
Check(10, new[] { new long[] { 11 } });
Check(10, new[] { new long[] { 10 } });
Check(10, new[] { new long[] { 6, 4 } }); // 同组不能把两件一起选中
Check(10, new[] { new long[] { 8 }, new long[] { 5 }, new long[] { 5 } }); // 非贪心
Check(10, new[] { Array.Empty<long>(), new long[] { 3, 7 }, new long[] { 3 } });
Check(2_000_001, new[] { new long[] { 1_000_001 }, new long[] { 1_000_001 } });
Check(long.MaxValue, new[] { new long[] { long.MaxValue - 1 }, new long[] { 2 } });
Check(2_000_001, new[] { new long[] { 1_200_000, 1_000_000 }, new long[] { 1_000_001 } });
var tied = new List<List<string>> { new() { "first", "second" } };
foreach (long budget in new long[] { 10, 2_000_001 })
    if (GroupedKnapsack.Select(tied, budget, _ => 10).Single() != "first")
        throw new Exception("同价选择不稳定");
var random = new Random(4348910);
for (int i = 0; i < 300; i++)
{
    var groups = Enumerable.Range(0, random.Next(1, 7))
        .Select(_ => Enumerable.Range(0, random.Next(1, 4)).Select(_ => (long)random.Next(1, 40)).ToArray()).ToArray();
    Check(random.Next(1, 100), groups);
    Check(random.Next(2_000_001, 5_000_000), groups.Select(row => row.Select(p => p * 100_003).ToArray()).ToArray());
}
Console.WriteLine("通过：10 个边界场景、同价稳定性 + 600 个固定种子穷举对照场景。");
MarketForecastChecks.Run();
