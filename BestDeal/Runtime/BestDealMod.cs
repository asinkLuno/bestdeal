using System;
using UnityEngine;

namespace BestDeal;

/// <summary>
/// BestDeal — 顾客上门时按类别+预算计算最佳售卖组合，HUD 显示推荐清单。
/// 游戏原生 mod（IMod + manifest.xml，Harmony 补丁由游戏加载器自动应用，无需 PatchAll）。
/// 系统设计与游戏 API 约束见 docs/design.md。
/// </summary>
public class BestDealMod : IMod
{
    internal static ModLog Log;

    public void Init(ModManifest manifest)
    {
        Log = new ModLog(manifest);
        Hud.ModId = manifest.ID;
        FeatureSettings.Init(manifest.ID);
        Log.Log("BestDeal v2.10.2 已加载");
    }

    public void OnEnable()
    {
        EnsureHud();
        HudContent.Clear();
    }

    public void OnDisable()
    {
        if (Hud.Instance != null) UnityEngine.Object.Destroy(Hud.Instance.gameObject);
        ClientSession.CurrentClient = null;
        HudContent.Clear();
        SellerMarketWatch.Lines.Clear();
    }

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
