using System.Collections.Generic;

namespace BestDeal;

/// <summary>
/// 店内库存发现：售卖/仓储区枚举 + 后柜台。
/// 只列玩家能拿去卖的区域；已售陈列、垃圾桶、拖拽缓冲、设施、夜间区等不在其中。
/// 后柜台有两个容器：backInvinvElement（玩家拖物品上柜台落在这里）和
/// backInvinvElementCounter（收购商上门等流程会用 TransferBackInvToCounter 把已摆物品
/// 整体搬过去）。游戏自己的"柜台可卖物品"读取函数两个都算，这里同样两个都扫、都算"已就位"，
/// 签名也合并——两容器之间倒来倒去不算变化，不触发重算。
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
        if (entry.backInvinvElementCounter != null) yield return (entry.backInvinvElementCounter, "counter");
        if (entry.bazarLeftinvElement != null) yield return (entry.bazarLeftinvElement, "bazar");
        if (entry.hirelingInv != null) yield return (entry.hirelingInv, "hireling");
    }

    /// <summary>后柜台主容器：玩家拖物品上柜台落在这里，顾客议价读的也是它。</summary>
    public static GameInventory BackCounter
    {
        get
        {
            try { return EmporiumEntry.Instance != null ? EmporiumEntry.Instance.backInvinvElement : null; }
            catch { return null; }
        }
    }

    /// <summary>后柜台第二容器：流程性搬运（TransferBackInvToCounter）的目的地，同为"已摆上柜台"。</summary>
    public static GameInventory CounterAlt
    {
        get
        {
            try { return EmporiumEntry.Instance != null ? EmporiumEntry.Instance.backInvinvElementCounter : null; }
            catch { return null; }
        }
    }

    /// <summary>这个库存是不是后柜台（两个柜台容器都算"已就位"）。</summary>
    public static bool IsCounter(GameInventory inv)
    {
        return ReferenceEquals(inv, BackCounter) || ReferenceEquals(inv, CounterAlt);
    }

    /// <summary>
    /// 后柜台内容的指纹（两个柜台容器合并算）：玩家摆上/拿走物品就变化。编排层按 0.5 秒轮询它，
    /// 一变就重算报价，让"✔ 已在柜台"标记即时跟上玩家的动作。
    /// 合并的意义：游戏把物品在两容器之间整体搬运（如收购商上门的 TransferBackInvToCounter）
    /// 时物品集合不变，指纹不变，不会误判成"东西不在库存里"而重刷推荐清单。
    /// </summary>
    public static string BackCounterSignature()
    {
        try
        {
            var parts = new List<string>();
            foreach (var inv in new[] { BackCounter, CounterAlt })
            {
                var items = inv?.childItems;
                if (items == null) continue;
                foreach (var gi in items) parts.Add(gi == null ? "-" : gi.GetHashCode().ToString());
            }
            parts.Sort(System.StringComparer.Ordinal);
            return parts.Count + ":" + string.Join(",", parts);
        }
        catch { return null; }
    }
}
