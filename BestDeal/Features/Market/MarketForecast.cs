using System;
using System.Collections.Generic;
using System.Linq;

namespace BestDeal;

/// <summary>只计算已知事件日程的新闻加成；不调用游戏、不生成随机事件。</summary>
internal static class MarketForecast
{
    internal sealed class Event
    {
        internal string Id;
        internal int Start;
        internal long End; // 不含结束日；永久事件使用 long.MaxValue。
        internal Dictionary<string, long> Effects;
    }

    internal sealed class Rise
    {
        internal int Days;
        internal long Current, Future;
    }

    internal sealed class Timeline
    {
        internal readonly SortedDictionary<int, Dictionary<string, long>> Days = new();

        internal Rise FirstRise(IEnumerable<string> types)
        {
            var tags = new HashSet<string>(types.Where(x => !string.IsNullOrEmpty(x)));
            long Sum(Dictionary<string, long> effects) => tags.Sum(t => effects.TryGetValue(t, out var v) ? v : 0);
            long current = Sum(Days[0]);
            foreach (var day in Days)
            {
                if (day.Key == 0) continue;
                long future = Sum(day.Value);
                if (future > current) return new Rise { Days = day.Key, Current = current, Future = future };
            }
            return null;
        }
    }

    internal static Timeline Build(IEnumerable<Event> source)
    {
        var events = new List<Event>();
        foreach (var input in source.Where(e => e != null && e.Start >= 0 && e.End > e.Start)
                     .OrderBy(e => e.Start))
        {
            // 游戏对仍在运行的同 ID 事件延长持续时间，而非重复叠加。
            var running = events.LastOrDefault(e => !string.IsNullOrEmpty(input.Id)
                && e.Id == input.Id && e.End > input.Start);
            if (running != null) running.End = Math.Max(running.End, input.End);
            else events.Add(new Event { Id = input.Id, Start = input.Start, End = input.End, Effects = input.Effects });
        }
        var boundaries = new SortedSet<int> { 0 };
        foreach (var ev in events)
        {
            boundaries.Add(ev.Start);
            if (ev.End <= int.MaxValue) boundaries.Add((int)ev.End);
        }
        var result = new Timeline();
        foreach (int day in boundaries)
        {
            var effects = new Dictionary<string, long>();
            foreach (var ev in events)
            {
                if (day < ev.Start || day >= ev.End || ev.Effects == null) continue;
                foreach (var effect in ev.Effects)
                    effects[effect.Key] = (effects.TryGetValue(effect.Key, out var value) ? value : 0) + effect.Value;
            }
            result.Days.Add(day, effects);
        }
        return result;
    }
}
