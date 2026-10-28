using System;
using UnityEngine;

namespace BestDeal;

/// <summary>
/// BestDeal — 顾客上门时按类别+预算计算最佳售卖组合，HUD 显示推荐清单。
/// 游戏原生 mod（IMod + manifest.xml，Harmony 补丁由游戏加载器自动应用，无需 PatchAll）。
///
/// 模块划分（依赖自上而下，无环）：
///   入口 BestDealMod → 展示 Hud → 编排 ClientSession → 功能 PriceProbe / ClientDemand / QuoteOptimizer /
///   Authenticity / Appraisal / MarketWatch → 基础 ShopInventories → 游戏类型。
///
/// 价格一律用游戏自己算的：最终成交价完全由物品身上的 ItemFeature 决定，摆上柜台时游戏才把
/// 顾客的加价/折扣等特征挂上去（实测：背包里读 55，摆上柜台变 64/37）。PriceProbe 在内存里
/// 挂上同一批特征读 GetNegociatedValue() 再按快照还原——物品不用真的搬上柜台。
/// 需求同样读游戏自己的字段：clientBuyingTagList / clientBuyingIdList（到店几秒后才填，轮询刷新）、
/// isRequireVariety + IsItemInVarietyList（要求不重复的顾客每类最多推荐 1 件）。
/// </summary>
public class BestDealMod : IMod
{
    internal static ModLog Log;

    public void Init(ModManifest manifest)
    {
        Log = new ModLog(manifest);
        Log.Log("BestDeal v2.6.0 已加载");
    }

    public void OnEnable()
    {
        EnsureHud();
        // 开局先给一行占位，进店就能看到 HUD、确认 mod 活着（顾客上门后被推荐清单替换）
        Result.Set("BestDeal", new[] { "已加载，等顾客上门" });
    }

    public void OnDisable() { }

    internal static void EnsureHud()
    {
        if (Hud.Instance != null) return;
        try
        {
            var go = new GameObject("BestDealHud");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<Hud>();
        }
        catch (Exception e)
        {
            Log.Warning($"HUD 创建失败：{e.Message}");
        }
    }
}
