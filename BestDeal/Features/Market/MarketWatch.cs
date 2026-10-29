using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BestDeal;

/// <summary>
/// 开店前行情提示（只读）：读 StoreStation 当日生效的 StoreEvent（游戏自己的新闻事件，
/// negociationDatas 的 value 正负号就是涨跌——游戏把它直接累加进 GetCurrentValue 的 marketMult）。
/// 涨价类：扫库存，有 affected 物品就提示摆进橱窗（display case，浏览客按含事件加成的价格买走）；
/// 没有现货则提示改摆库存里最多的那个种类。
/// 降价类：提示把对应类别的收购牌（sign_* 物品，ToolDirectory 注册、挂在橱窗吸引该类别卖家）摆上橱窗。
/// 仅在开店前（PlayerStore.storeState == MORNING）显示，营业和闭店后隐藏。
/// itemType → 收购牌的家族划分抄游戏自己的 TypeHelper.GetTypeColor 分组表。
/// </summary>
public static class MarketWatch
{
    const float RefreshInterval = 2f;

    static float _nextCheck;

    // 事件 itemType → 收购牌（item_sign_<key>_name 本地化）。家族与 TypeHelper.GetTypeColor 一致。
    static readonly (string[] tags, string signKey)[] SignFamilies =
    {
        (new[] { "WATER", "WATER_RATION", "WATER_PURIFICATION_SUPPLY" }, "water"),
        (new[] { "FOOD", "FRESH_FOOD", "PROCESSED_FOOD", "TREAT", "BREVAGE", "FARMING_SUPPLIES", "SEED", "MODIFIED_SEED" }, "food"),
        (new[] { "MEDICAL", "MEDICAL_TOOL", "ORGAN" }, "medical"),
        (new[] { "MATERIAL", "BASIC_MATERIAL", "REFINED_MATERIAL", "ENERGY_CELL" }, "material"),
        (new[] { "HOUSEHOLD_GOOD" }, "household"),
        (new[] { "LUXURY_ITEM", "ART" }, "luxury"),
        (new[] { "WEAPON", "MELEE_WEAPON", "FIREARM", "MODDABLE", "GUN_MOD", "AMMUNITION", "MUNITION", "ARMOR" }, "firearm"),
    };

    /// <summary>店铺是否处于开店前的早晨；是否正在接待顾客由 ClientSession 判断。</summary>
    public static bool WantsPanel()
    {
        try
        {
            if (!PlayerStore.HasInstance) return false;
            return PlayerStore.Instance.storeState == PlayerStore.StoreState.MORNING;
        }
        catch { return false; }
    }

    /// <summary>由编排层在 WantsPanel() 时每帧调用；内部节流，把行情写进 HUD。</summary>
    public static void Tick()
    {
        if (Time.unscaledTime < _nextCheck) return;
        _nextCheck = Time.unscaledTime + RefreshInterval;
        HudContent.Set(L10n.Text("MarketTitle"), BuildLines());
    }

    static List<string> BuildLines()
    {
        var lines = new List<string>();
        var deltas = CurrentDeltas();
        if (deltas.Count == 0)
        {
            lines.Add(L10n.Text("NoChanges"));
            return lines;
        }

        var stock = StockItems();   // 库存（不含已在橱窗的）
        string suggestedTop = null; // 本轮已经提示过的"库存最多"种类，多个涨价类没货时不重复提示
        bool any = false;
        foreach (var kv in deltas.OrderByDescending(x => x.Value))
        {
            if (kv.Value == 0) continue;
            any = true;
            string pct = kv.Value > 0 ? $"+{kv.Value}%" : $"{kv.Value}%";
            lines.Add($"{(kv.Value > 0 ? "↑" : "↓")} {ClientDemand.LocalizeTag(kv.Key)} {pct}");
            if (kv.Value > 0)
            {
                var inStock = AffectedInStock(stock, kv.Key);
                if (inStock.Count > 0)
                {
                    lines.Add(L10n.Text("Display") + string.Join(L10n.Text("ListSeparator"), inStock));
                }
                else
                {
                    string top = TopStockType(stock);
                    if (top == null || top == suggestedTop)
                    {
                        lines.Add(L10n.Text("OutOfStock"));
                    }
                    else
                    {
                        suggestedTop = top;
                        var names = AffectedInStock(stock, top, 2);
                        lines.Add(L10n.Format("AlternativeStock", ClientDemand.LocalizeTag(top), string.Join(L10n.Text("ListSeparator"), names)));
                    }
                }
            }
            else
            {
                string sign = SignFor(kv.Key);
                lines.Add(sign != null ? L10n.Format("BuyingSign", sign) : L10n.Text("NoSign"));
            }
        }
        if (!any) lines.Add(L10n.Text("NoChanges"));
        return lines;
    }

    /// <summary>当前生效新闻按类别合并；供开店前行情和顾客收购提示共用。</summary>
    internal static Dictionary<string, int> CurrentDeltas()
    {
        var events = ActiveEvents();
        var deltas = new Dictionary<string, int>();
        if (events != null)
        {
            foreach (var ev in events)
            {
                if (ev?.negociationDatas == null) continue;
                foreach (var nd in ev.negociationDatas)
                {
                    if (nd == null || string.IsNullOrEmpty(nd.itemType) || nd.value == 0) continue;
                    // 多事件叠加时游戏是把 value 直接累加进 marketMult，这里同样按类别合并
                    deltas[nd.itemType] = (deltas.TryGetValue(nd.itemType, out var v) ? v : 0) + nd.value;
                }
            }
        }
        return deltas;
    }

    static List<StoreEvent> ActiveEvents()
    {
        try { return StoreStation.Instance?.storeEventManager?.GetActiveEvents(); }
        catch { return null; }
    }

    /// <summary>可挪动的库存物品（与推荐清单同一套区域，跳过已在橱窗的）。</summary>
    static List<GameItem> StockItems()
    {
        var result = new List<GameItem>();
        foreach (var (inv, zone) in ShopInventories.Enumerate())
        {
            if (zone == "showcase") continue;   // 已经在橱窗里的不算"待摆"
            var items = inv.childItems;
            if (items == null) continue;
            foreach (var gi in items)
                if (gi != null) result.Add(gi);
        }
        return result;
    }

    /// <summary>库存里受该类别涨价影响的物品，按名字合并件数（判定与游戏 NegociationData 一致：itemType 与物品类型精确相等）。</summary>
    static List<string> AffectedInStock(List<GameItem> stock, string itemType, int cap = 4)
    {
        var counts = new Dictionary<string, long>();
        foreach (var gi in stock)
        {
            bool hit = false;
            try { hit = gi.GetGameItemType()?.Contains(itemType) ?? false; } catch { }
            if (!hit) continue;
            long n = gi.unitCount;
            if (n <= 0) n = 1;
            counts[gi.name] = (counts.TryGetValue(gi.name, out var v) ? v : 0) + n;
        }
        // 行宽有限：最多列 cap 种，剩下的合并计数
        var parts = counts.Take(cap).Select(kv => kv.Value > 1 ? $"{kv.Key} ×{kv.Value}" : kv.Key).ToList();
        if (counts.Count > cap) parts.Add(L10n.Format("TypeCount", counts.Count));
        return parts;
    }

    /// <summary>
    /// 库存里物品最多的种类（按物品自身类型标签计件；多类型物品在每个类型下都各计一次）。
    /// 空库存或读不出类型返回 null。
    /// </summary>
    static string TopStockType(List<GameItem> stock)
    {
        var counts = new Dictionary<string, long>();
        bool any = false;
        foreach (var gi in stock)
        {
            any = true;
            List<string> types = null;
            try { types = gi.GetGameItemType(); } catch { }
            if (types == null) continue;
            long n = gi.unitCount;
            if (n <= 0) n = 1;
            foreach (var t in types)
                if (!string.IsNullOrEmpty(t))
                    counts[t] = (counts.TryGetValue(t, out var v) ? v : 0) + n;
        }
        if (!any || counts.Count == 0) return null;
        string best = null;
        long bestN = 0;
        foreach (var kv in counts)
            if (kv.Value > bestN) { bestN = kv.Value; best = kv.Key; }
        return best;
    }

    /// <summary>类别对应的收购牌本地化名（查不到就回退原始键）；没有对应牌的类别返回 null。</summary>
    static string SignFor(string itemType)
    {
        foreach (var fam in SignFamilies)
        {
            if (!fam.tags.Contains(itemType)) continue;
            string key = "item_sign_" + fam.signKey + "_name";
            try
            {
                var s = LocHelper.GetLocalizedItem(key);
                return string.IsNullOrEmpty(s) ? key : s;
            }
            catch { return key; }
        }
        return null;
    }
}
