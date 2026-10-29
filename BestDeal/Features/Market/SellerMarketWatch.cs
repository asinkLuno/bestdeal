using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BestDeal;

/// <summary>只读匹配顾客待售物品与当前新闻和已知日程，不修改价格。</summary>
internal static class SellerMarketWatch
{
    internal static readonly List<string> Lines = new();
    static StoreClient _client;
    static float _nextCheck;

    internal static void Tick()
    {
        var client = ClientSession.CurrentClient;
        var entry = EmporiumEntry.Instance;
        if (!FeatureSettings.SellerMarket || entry == null || client == null || client.isBusinessComplete || !client.IsClientSelling())
        {
            Lines.Clear();
            _client = null;
            return;
        }
        if (ReferenceEquals(client, _client) && Time.unscaledTime < _nextCheck) return;
        _client = client;
        _nextCheck = Time.unscaledTime + 0.5f;
        Lines.Clear();
        ClientSession.TryRun("顾客售卖行情", () =>
        {
            var deltas = MarketWatch.CurrentDeltas();
            // 与游戏检查顾客所售物品相同：前后柜台中尚未归玩家所有的物品。
            var items = entry.GetAllNonOwnedItem().Where(x => x != null).Distinct().ToList();
            var matches = new Dictionary<string, HashSet<string>>();
            foreach (var item in items)
            {
                var types = item.GetGameItemType();
                if (types == null) continue;
                foreach (var type in types.Distinct())
                {
                    if (type == null || !deltas.TryGetValue(type, out var delta) || delta == 0) continue;
                    if (!matches.TryGetValue(type, out var names))
                        matches[type] = names = new HashSet<string>();
                    names.Add(item.name);
                }
            }
            var lines = new List<string>();
            foreach (var match in matches.OrderByDescending(x => deltas[x.Key]).ThenBy(x => x.Key))
            {
                int delta = deltas[match.Key];
                lines.Add(L10n.Format("NewsChange", delta > 0 ? "↑" : "↓", ClientDemand.LocalizeTag(match.Key), delta > 0 ? "+" : "", delta));
                var names = match.Value.OrderBy(x => x).ToList();
                lines.Add(L10n.Text("Affected") + string.Join(L10n.Text("ListSeparator"), names.Take(4)) + (names.Count > 4 ? L10n.Format("TypeCount", names.Count) : ""));
            }
            Lines.AddRange(lines);
            Lines.AddRange(FutureMarketWatch.BuildLines(items, buying: true));
        });
    }
}
