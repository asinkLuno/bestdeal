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
}
