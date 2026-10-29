using System;
using UnityEngine;

namespace BestDeal;

/// <summary>
/// 编排层：维护"当前顾客"，把游戏事件（到店/离店）和每帧轮询转成对功能模块的调用。
/// 状态机：无顾客 →(StartMainDialogue)→ 已到店 ──需求指纹或柜台内容变化──► 重算报价
/// （QuoteOptimizer 内部同步模拟读价，无需跨帧的探测排队）。
/// 开店前的早晨（storeState == MORNING）且无顾客时，HUD 改由 MarketWatch 显示当日涨跌价行情；
/// 开店/顾客到店的瞬间把行情面板换回常规面板。
/// </summary>
public static class ClientSession
{
    internal static StoreClient CurrentClient;
    private static string _lastReqSig;     // 上次渲染时的需求指纹
    private static string _lastCounterSig; // 上次渲染时的柜台内容指纹（玩家摆上/拿走物品会变）
    private static float _nextReqCheck;
    private static bool _marketPanelShown; // 上一次写 HUD 的是不是行情面板（切换时负责收尾）

    // 顾客开始对话 = 顾客已到柜台，自动计算一次
    internal static void OnClientArrived(StoreClient __instance)
    {
        CurrentClient = __instance;
        _lastReqSig = null;
        _nextReqCheck = 0f;
        TryRun("自动计算", () => QuoteOptimizer.Run(CurrentClient));
        _lastReqSig = SafeSig(__instance);
    }

    // 交易结束清掉当前顾客
    internal static void OnBusinessDone(StoreClient __instance)
    {
        if (!ReferenceEquals(CurrentClient, __instance)) return;
        CurrentClient = null;
        _lastReqSig = null;
    }

    // 每帧轮询（Hud.Update 驱动）
    internal static void Tick()
    {
        try
        {
            if (EmporiumEntry.Instance == null) return;               // 不在店内场景（主菜单/读盘）：不扫库存、不重算

            // 开店前行情面板：涨跌价新闻 + 摆橱窗/挂收购牌提示；开店（或顾客到店）后不再显示
            if (CurrentClient == null && MarketWatch.WantsPanel())
            {
                _marketPanelShown = true;
                MarketWatch.Tick();
                return;
            }
            if (_marketPanelShown)
            {
                _marketPanelShown = false;
                if (CurrentClient == null) HudContent.Set("BestDeal", new[] { L10n.Text("NoCustomer") });
            }

            PollRequirement();
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
    /// </summary>
    static void PollRequirement()
    {
        var c = CurrentClient;
        if (c == null)
        {
            _lastReqSig = null;
            _lastCounterSig = null;
            HudContent.Set("BestDeal", new[] { L10n.Text("NoCustomer") });
            return;
        }
        if (Time.unscaledTime < _nextReqCheck) return;
        _nextReqCheck = Time.unscaledTime + 0.5f;

        var sig = SafeSig(c);
        var counterSig = ShopInventories.BackCounterSignature();
        bool counterChanged = counterSig != null && _lastCounterSig != null && counterSig != _lastCounterSig;
        if (counterSig != null) _lastCounterSig = counterSig;

        if (sig == _lastReqSig && !counterChanged) return;
        _lastReqSig = sig;
        QuoteOptimizer.Run(c);
    }

    static string SafeSig(StoreClient c)
    {
        try { return L10n.LocaleCode + "|" + ClientDemand.RequirementSignature(c); }
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
