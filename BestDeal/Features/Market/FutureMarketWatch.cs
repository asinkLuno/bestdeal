using System.Collections.Generic;
using System.Linq;

namespace BestDeal;

/// <summary>读取已知的排期与当前事件剩余天数，为库存和待收购物品生成留货提示。</summary>
internal static class FutureMarketWatch
{
    internal static List<string> BuildLines(IEnumerable<GameItem> items, bool buying)
    {
        var result = new List<string>();
        var manager = StoreStation.Instance?.storeEventManager;
        if (manager == null) return result;
        var events = new List<MarketForecast.Event>();
        foreach (var ev in manager.GetActiveEvents()) Add(ev, 0);
        if (manager.futurEvents != null && manager.futurEventsDay != null)
        {
            int count = System.Math.Min(manager.futurEvents.Count, manager.futurEventsDay.Count);
            for (int i = 0; i < count; i++)
            {
                var ev = manager.futurEvents[i];
                int days = manager.futurEventsDay[i];
                if (ev != null && ev.IsKnown && days > 0) Add(ev, days);
            }
        }
        var timeline = MarketForecast.Build(events);
        var matches = new Dictionary<(int days, long current, long future), HashSet<string>>();
        foreach (var item in items.Where(x => x != null).Distinct())
        {
            List<string> types;
            try { types = item.GetGameItemType(); }
            catch { continue; }
            if (types == null) continue;
            // 多类型物品按全部类型合并，避免某一类涨价、另一类降价却误报升值。
            var rise = timeline.FirstRise(types);
            if (rise == null) continue;
            var key = (rise.Days, rise.Current, rise.Future);
            if (!matches.TryGetValue(key, out var names)) matches[key] = names = new HashSet<string>();
            names.Add(item.name);
        }
        if (matches.Count == 0) return result;
        result.Add(L10n.Text("FutureMarketTitle"));
        foreach (var match in matches.OrderBy(x => x.Key.days).ThenByDescending(x => x.Key.future - x.Key.current))
        {
            var key = match.Key;
            result.Add(L10n.Format("FutureRise", key.days, key.current, key.future, key.future - key.current));
            var names = match.Value.OrderBy(x => x).ToList();
            result.Add(L10n.Text(buying ? "FutureBuy" : "FutureHold")
                + string.Join(L10n.Text("ListSeparator"), names.Take(4))
                + (names.Count > 4 ? L10n.Format("TypeCount", names.Count) : ""));
        }
        return result;

        void Add(StoreEvent ev, int start)
        {
            if (ev == null || (!ev.isPermanent && ev.duration <= 0)) return;
            var effects = new Dictionary<string, long>();
            if (ev.negociationDatas != null)
                foreach (var data in ev.negociationDatas)
                {
                    if (data == null || string.IsNullOrEmpty(data.itemType)) continue;
                    effects[data.itemType] = (effects.TryGetValue(data.itemType, out var v) ? v : 0) + data.value;
                }
            events.Add(new MarketForecast.Event
            {
                Id = ev.identifier, Start = start,
                End = ev.isPermanent ? long.MaxValue : (long)start + ev.duration,
                Effects = effects
            });
        }
    }
}
