using System;
using HarmonyLib;
using UnityEngine;

namespace BestDeal;

/// <summary>
/// 编排层：维护"当前顾客"，把游戏事件（到店/离店）和每帧轮询转成对功能模块的调用。
/// 状态机：无顾客 →(StartMainDialogue)→ 已到店 ──需求指纹变化──► 重算报价
///         ──有候选没价──► PriceProbe 排队探价 ──探完回调──► 用真价重算。
/// CompleteBusiness 或新顾客到店都会先清场（收回探测物品、还原多样性记录）。
/// </summary>
public static class ClientSession
{
    internal static StoreClient CurrentClient;
    private static string _lastReqSig;     // 上次渲染时的需求指纹
    private static string _lastCounterSig; // 上次渲染时的柜台内容指纹（玩家摆上/拿走物品会变）
    private static float _nextReqCheck;
    private static StoreClient _probedClient;   // 已经探过价的顾客，离开时清掉
    private static float _nextProbeCheck;
    private static float _arrivedAt;

    // 顾客开始对话 = 顾客已到柜台，自动计算一次
    internal static void OnClientArrived(StoreClient __instance)
    {
        var prev = CurrentClient;
        if (prev != null && !ReferenceEquals(prev, __instance)) PriceProbe.Stop(prev);
        CurrentClient = __instance;
        _lastReqSig = null;
        _nextReqCheck = 0f;
        _arrivedAt = Time.unscaledTime;
        _probedClient = null;
        TryRun("自动计算", () => QuoteOptimizer.Run(CurrentClient));
        _lastReqSig = SafeSig(__instance);
    }

    // 交易结束清掉当前顾客
    internal static void OnBusinessDone(StoreClient __instance)
    {
        if (!ReferenceEquals(CurrentClient, __instance)) return;
        PriceProbe.Stop(CurrentClient);
        CurrentClient = null;
        _lastReqSig = null;
        _probedClient = null;
    }

    // 每帧轮询（Hud.Update 驱动）
    internal static void Tick()
    {
        try
        {
            if (PriceProbe.Running) PriceProbe.Step(CurrentClient);   // 手里的东西先放回原位（在哪个场景都要放）
            if (EmporiumEntry.Instance == null) return;               // 不在店内场景（主菜单/读盘）：不扫库存、不重算
            PollRequirement();
            MaybeStartProbe();
        }
        catch (Exception e)
        {
            BestDealMod.Log.Error($"Tick: {e}");
        }
    }

    /// <summary>
    /// 顾客需求是「到店之后」才填的（实测：StartMainDialogue 那一刻 clientBuyingTagList 为空、
    /// isRequireVariety=False，几秒后才有 HOUSEHOLD_GOOD 且 isRequireVariety=True），
    /// 所以按 0.5 秒轮询需求指纹，一变就重算，别让 HUD 停在「无限制」。
    /// 柜台内容指纹一并轮询：玩家把推荐的东西摆上/拿下柜台时，"✔ 已在柜台"标记要即时跟上。
    /// 探测进行中柜台本来就在被探测动，跳过以免自己触发自己。
    /// </summary>
    static void PollRequirement()
    {
        var c = CurrentClient;
        if (c == null) { _lastReqSig = null; _lastCounterSig = null; return; }
        if (Time.unscaledTime < _nextReqCheck) return;
        _nextReqCheck = Time.unscaledTime + 0.5f;

        var sig = SafeSig(c);
        var counterSig = PriceProbe.Running ? null : ShopInventories.BackCounterSignature();
        bool counterChanged = counterSig != null && _lastCounterSig != null && counterSig != _lastCounterSig;
        if (counterSig != null) _lastCounterSig = counterSig;

        if (sig == _lastReqSig && !counterChanged) return;
        _lastReqSig = sig;
        QuoteOptimizer.Run(c);
    }

    /// <summary>需求填好了（或到柜台几秒后）就给候选物品排一轮"上柜台读价"，拿游戏算的真价。</summary>
    static void MaybeStartProbe()
    {
        var c = CurrentClient;
        if (c == null) { _probedClient = null; return; }
        if (PriceProbe.Running) return;
        if (ReferenceEquals(c, _probedClient)) return;   // 这位顾客已经探过一轮
        if (Time.unscaledTime < _nextProbeCheck) return;
        _nextProbeCheck = Time.unscaledTime + 1f;

        if (!ClientDemand.HasAnyRequirement(c) && Time.unscaledTime - _arrivedAt < 4f) return;   // 需求还是空的（可能还没填）就再等几秒

        var cands = QuoteOptimizer.CollectCandidates(c, c.isRequireVariety);
        if (cands.Count == 0) return;
        // 只有真的排上队才记"这位顾客探过"——找不到柜台时不记，留着重试
        if (PriceProbe.Start(c, cands, ProbeFinished)) _probedClient = c;
    }

    // 探价全部读完后的回调：用游戏真价重算 HUD
    static void ProbeFinished() => TryRun("探测后重算", () => QuoteOptimizer.Run(CurrentClient));

    static string SafeSig(StoreClient c)
    {
        try { return ClientDemand.RequirementSignature(c); }
        catch { return null; }
    }

    internal static void TryRun(string what, Action fn)
    {
        try
        {
            fn();
        }
        catch (Exception e)
        {
            BestDealMod.Log.Error($"[{what}] 失败：{e}");
        }
    }
}

// Harmony 补丁由游戏模组加载器自动应用（扫描 [HarmonyPatch] 类），不要自己 PatchAll
[HarmonyPatch(typeof(StoreClient), nameof(StoreClient.StartMainDialogue))]
public static class StoreClient_StartMainDialogue_Patch
{
    static void Postfix(StoreClient __instance) => ClientSession.OnClientArrived(__instance);
}

[HarmonyPatch(typeof(StoreClient), nameof(StoreClient.CompleteBusiness))]
public static class StoreClient_CompleteBusiness_Patch
{
    static void Postfix(StoreClient __instance) => ClientSession.OnBusinessDone(__instance);
}
