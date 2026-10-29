using BestDeal;

internal static class MarketForecastChecks
{
    internal static void Run()
    {
        MarketForecast.Event Event(string id, int start, long end, params (string tag, long value)[] effects) =>
            new() { Id = id, Start = start, End = end, Effects = effects.ToDictionary(x => x.tag, x => x.value) };
        void Check(string name, MarketForecast.Event[] events, string[] tags, int day, long current = 0, long future = 0)
        {
            var rise = MarketForecast.Build(events).FirstRise(tags);
            if (day == 0 ? rise != null : rise == null || rise.Days != day || rise.Current != current || rise.Future != future)
                throw new Exception($"行情预估失败：{name}");
        }
        Check("空日程", [], ["FOOD"], 0);
        Check("已知未来涨价", [Event("a", 2, 4, ("FOOD", 50))], ["FOOD"], 2, 0, 50);
        Check("折价结束回升", [Event("a", 0, 1, ("FOOD", -50))], ["FOOD"], 1, -50, 0);
        Check("涨价结束不会误报", [Event("a", 0, 1, ("FOOD", 50))], ["FOOD"], 0);
        Check("同日涨跌抵消", [Event("a", 2, 4, ("FOOD", 50)), Event("b", 2, 4, ("FOOD", -50))], ["FOOD"], 0);
        Check("多类型净值抵消", [Event("a", 2, 4, ("FOOD", 50), ("WATER", -50))], ["FOOD", "WATER"], 0);
        Check("类型去重", [Event("a", 2, 4, ("FOOD", 50))], ["FOOD", "FOOD"], 2, 0, 50);
        Check("同 ID 延期不叠加", [Event("a", 0, 3, ("FOOD", 50)), Event("a", 2, 5, ("FOOD", 50))], ["FOOD"], 0);
        Check("同 ID 结束后重开", [Event("a", 0, 2, ("FOOD", -50)), Event("a", 2, 4, ("FOOD", 50))], ["FOOD"], 2, -50, 50);
        Check("永久折价不会自行结束", [Event("a", 0, long.MaxValue, ("FOOD", -50))], ["FOOD"], 0);
        Check("相邻事件同时更换", [Event("a", 0, 2, ("FOOD", 50)), Event("b", 2, 4, ("FOOD", 25))], ["FOOD"], 0);
        Check("不同日期累计", [Event("a", 1, 5, ("FOOD", 20)), Event("b", 2, 4, ("WATER", 30))], ["FOOD", "WATER"], 1, 0, 20);
        Check("无关类型", [Event("a", 2, 4, ("FOOD", 50))], ["WATER"], 0);
        Check("无效区间", [Event("a", 2, 2, ("FOOD", 50))], ["FOOD"], 0);
        // 生产算法不得修改从游戏快照转换而来的输入。
        var original = Event("a", 0, 2, ("FOOD", 50));
        MarketForecast.Build([original, Event("a", 1, 4, ("FOOD", 50))]);
        if (original.End != 2) throw new Exception("行情预估修改了输入");
        Console.WriteLine("通过：15 个行情日程检查（到期回升、重叠抵消、多类型、延期及输入只读）。");
    }
}
