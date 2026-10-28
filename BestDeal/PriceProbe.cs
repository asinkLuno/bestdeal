using System;
using System.Collections.Generic;
using System.Linq;

namespace BestDeal;

/// <summary>
/// 真价判定（价格模拟）：游戏的最终成交价只由物品身上的 ItemFeature 决定——顾客带来的所有影响
/// （零售加价、顾客买方特征、香水、成瘾/谈判惩罚、小费、违禁品/许可证加价、店内活动）都是物品
/// 摆上后柜台那一刻由游戏钩子挂上去的（backInvinvElement.onSlotAddItemEndFunc →
/// PlayerStore.AddedItemToWeightedArea → OnAddedToWeightedArea；GameItem 构造器钩子 →
/// clientItemFeatureBuying）。游戏没有"不上柜台直接报价"的接口，但这条挂特征的链路全是 public
/// 且与位置无关：这里在内存里挂上同一批特征 → GetNegociatedValue() → 按快照原子还原，
/// 物品原地不动，读出的价与真实摆放一致。
/// 已知边界：IsApplyMarkUp()（只影响带内容物的容器的子物品零售加价）要求物品真的 rooted 在
/// backInv 下，带货容器的内部子价模拟值会略低于真实柜台价；普通单件完全一致。
/// </summary>
public static class PriceProbe
{
    /// <summary>读"把这件摆上柜台招待这位顾客"时游戏会算的最终成交价（物品不动）。失败返回 0。</summary>
    public static long ReadPrice(StoreClient client, GameItem gi)
    {
        if (gi == null) return 0;
        try
        {
            var saved = Snapshot(gi);
            try
            {
                AttachCounterFeatures(client, gi);
                return gi.GetNegociatedValue();
            }
            finally
            {
                Restore(saved);
            }
        }
        catch (Exception e)
        {
            BestDealMod.Log.Warning($"价格模拟：{NameOf(gi)} 读价失败：{e.Message}");
            return 0;
        }
    }

    /// <summary>
    /// 复刻摆上柜台时游戏两条钩子挂特征的动作。全部调用游戏自己的方法，不抄价格公式，
    /// 游戏改了挂特征逻辑这里自动跟上。
    /// </summary>
    static void AttachCounterFeatures(StoreClient client, GameItem gi)
    {
        // 钩子一（GameItem 构造器 onSlotAddItemFunc）：物品直上柜台时挂顾客买方特征，黑店契约再挂违禁品加价
        if (client?.clientItemFeatureBuying != null)
        {
            foreach (var f in client.clientItemFeatureBuying)
            {
                if (!ItemFeatureList.SpecialFeature.TryGetValue(f, out var make)) continue;
                var feature = make();
                if (feature != null) gi.AddItemFeature(feature);
            }
        }
        if (client != null && client.chargesContrabandMarkup)
            ContrabandHelper.ApplyContrabandFeature(gi);

        // 钩子二（backInvinvElement.onSlotAddItemEndFunc → PlayerStore.AddedItemToWeightedArea）：
        // 零售加价、香水、成瘾/谈判惩罚、小费、违禁品/许可证、店内活动特征；
        // ignoreOnItemAddedToWeighted=true 跳过顾客的反应回调（读价不该惊动顾客）。
        gi.OnAddedToWeightedArea(ignoreOnItemAddedToWeighted: true);
    }

    /// <summary>挂特征前把物品和它所有子物品的特征列表原样备份（违禁品加价会挂到子物品上）。</summary>
    static List<(GameItem item, List<ItemFeature> features)> Snapshot(GameItem gi)
    {
        var saved = new List<(GameItem, List<ItemFeature>)> { (gi, gi.itemFeatures.ToList()) };
        foreach (var child in gi.FindAllChildItems())
            saved.Add((child, child.itemFeatures.ToList()));
        return saved;
    }

    static void Restore(List<(GameItem item, List<ItemFeature> features)> saved)
    {
        foreach (var (item, features) in saved)
        {
            item.itemFeatures.Clear();
            item.itemFeatures.AddRange(features);
        }
    }

    static string NameOf(GameItem gi) { try { return gi.name ?? "?"; } catch { return "?"; } }
}
