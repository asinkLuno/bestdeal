using System.Collections.Generic;

namespace BestDeal;

/// <summary>
/// 店内库存发现：售卖/仓储区枚举 + 后柜台。
/// 只列玩家能拿去卖的区域；已售陈列、垃圾桶、拖拽缓冲、设施、夜间区等不在其中。
/// </summary>
public static class ShopInventories
{
    public static IEnumerable<(GameInventory inv, string zone)> Enumerate()
    {
        var entry = EmporiumEntry.Instance;
        if (entry == null)
        {
            BestDealMod.Log.Warning("EmporiumEntry.Instance 为空（不在店内场景？）");
            yield break;
        }

        if (entry.invElement != null) yield return (entry.invElement, "shop");
        if (entry.showcaseElement != null) yield return (entry.showcaseElement, "showcase");
        if (entry.frontInvinvElement != null) yield return (entry.frontInvinvElement, "front");
        if (entry.backInvinvElement != null) yield return (entry.backInvinvElement, "back");
        if (entry.bazarLeftinvElement != null) yield return (entry.bazarLeftinvElement, "bazar");
        if (entry.hirelingInv != null) yield return (entry.hirelingInv, "hireling");
    }

    /// <summary>后柜台：游戏自己的 MoveItemToBackCounter 就是往这里放，顾客议价读的也是它。</summary>
    public static GameInventory BackCounter
    {
        get
        {
            try { return EmporiumEntry.Instance != null ? EmporiumEntry.Instance.backInvinvElement : null; }
            catch { return null; }
        }
    }

    /// <summary>
    /// 后柜台内容的指纹：玩家摆上/拿走物品就变化。编排层按 0.5 秒轮询它，
    /// 一变就重算报价，让"已在柜台 ✔"标记即时跟上玩家的动作。探测进行中不要调（探测自己在动柜台）。
    /// </summary>
    public static string BackCounterSignature()
    {
        try
        {
            var items = BackCounter?.childItems;
            if (items == null || items.Count == 0) return "0";
            var parts = new List<string>(items.Count);
            foreach (var gi in items) parts.Add(gi == null ? "-" : gi.GetHashCode().ToString());
            return items.Count + ":" + string.Join(",", parts);
        }
        catch { return null; }
    }
}
