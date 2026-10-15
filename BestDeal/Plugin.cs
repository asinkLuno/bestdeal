using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;                    // MelonLoader 生成的代理把游戏类型（原本全局命名空间）放进 Il2Cpp.*；Unity 类型仍是 UnityEngine.*
using Il2CppInterop.Runtime.InteropTypes;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(BestDeal.Plugin), "BestDeal", BestDeal.Plugin.Version, "guozr")]
[assembly: MelonID("guozr.skdp.bestdeal")]
[assembly: MelonGame("Questing Goose Studio", "Probably Stolen")]
// 关掉 MelonLoader 的自动打补丁：它等价于 Harmony.PatchAll(Assembly)，要求补丁类带**类级**
// [HarmonyPatch] 注解；本 mod 的补丁类只有方法级注解 → 自动打补丁会一个都不打（实测踩过）。
// 改成在 OnInitializeMelon 里显式打（等价于 BepInEx 版的 PatchAll(typeof(Patches))）。
[assembly: HarmonyDontPatchAll]

namespace BestDeal;

/// <summary>
/// BestDeal — 顾客上门时按类别+预算计算最佳售卖组合，HUD 显示推荐清单。
/// 价格一律用游戏自己算的：物品**摆上柜台招待这位顾客**时游戏才会算出最终成交价（实测：背包里读 55，
/// 摆上柜台变 64/37），所以 mod 会把候选物品逐件快速摆上柜台读走价格再收回（见 PriceProbe），
/// 而不是自己按加价公式估算。
/// 需求同样读游戏自己的字段：clientBuyingTagList / clientBuyingIdList（到店几秒后才填，轮询刷新）、
/// isRequireVariety + IsItemInVarietyList（要求不重复的顾客每类最多推荐 1 件，分组背包）。
/// </summary>
/// <summary>
/// 装载层：MelonLoader（社区标准；BepInEx 版见 main 分支 v1.4.2）。
/// 逻辑与 v1.4.x 一致——Harmony 补丁由 MelonLoader 自动应用（**不要**自己 PatchAll，会重复打），
/// HUD 与每帧轮询改用 MelonMod 自带的 OnGUI/OnUpdate，不再需要往 il2cpp 注入 MonoBehaviour。
/// </summary>
public class Plugin : MelonMod
{
    public const string Version = "1.6.0";

    public override void OnInitializeMelon()
    {
        Loc.Refresh();                                                     // 文案语言跟随游戏设置
        var patched = HarmonyInstance.TryPatchAll(typeof(Patches), true);   // true = 允许类上没有注解（BepInEx 版语义）
        MelonLogger.Msg(Loc.S("loaded", Version, patched?.Count ?? 0));
    }

    // 自己的 OnUpdate 每帧一定会被调到：轮询不能只挂在游戏自己的 StoreClientCounter.Update 上
    public override void OnUpdate() => Patches.Poll();

    public override void OnGUI()
    {
        var status = PriceProbe.Status;                       // 探测进度：每帧直接读，不走 Optimizer
        var appraisalServiceItem = ItemInspection.AppraisalServiceItem();
        var magnifiedItem = ItemInspection.CurrentInspectionItem();
        var inspectedItem = appraisalServiceItem ?? magnifiedItem;
        bool isAppraisalService = appraisalServiceItem != null;
        long? appraisalPrice = isAppraisalService ? AppraisalValue.CorrectBasePrice(appraisalServiceItem) : null;
        string inspectionDetails = magnifiedItem == null ? null : InspectionDetails.Read(magnifiedItem);
        int inspectionRows = inspectedItem == null ? 0 : 1 + (appraisalPrice.HasValue ? 1 : 0) + (inspectionDetails == null ? 0 : 2);
        int total = Result.Lines.Count + inspectionRows + (status == null ? 0 : 1);
        if (total == 0) return;
        float width = Mathf.Min(560f, Screen.width - 32f);
        var rect = new Rect(16, 16, width, 64 + total * 22);
        GUI.Box(rect, Result.Title);
        int i = 0;
        for (; i < Result.Lines.Count; i++)
        {
            string line = Result.Lines[i];
            if (i >= Result.PickLineStart && i < Result.PickLineStart + Result.PickedItems.Count)
            {
                var item = Result.PickedItems[i - Result.PickLineStart];
                line = (PriceProbe.IsOnCounter(item) ? "✔ " : "- ") + line;
            }
            GUI.Label(new Rect(30, 46 + i * 22, rect.width - 28, 22), line);
        }
        if (inspectedItem != null)
        {
            string name = Ref.Get(inspectedItem, "name", "?")?.ToString() ?? "?";
            GUI.Label(new Rect(30, 46 + i++ * 22, rect.width - 28, 22),
                      Loc.S(isAppraisalService ? "appraisalItem" : "inspectionItem", name));
            if (appraisalPrice.HasValue)
                GUI.Label(new Rect(30, 46 + i++ * 22, rect.width - 28, 22),
                          Loc.S("appraisalServicePrice", appraisalPrice.Value));
            if (inspectionDetails != null)
            {
                GUI.Label(new Rect(30, 46 + i * 22, rect.width - 28, 44),
                          Loc.S("inspectionDetails", inspectionDetails));
                i += 2;
            }
        }
        if (status != null)
            GUI.Label(new Rect(30, 46 + i * 22, rect.width - 28, 22), status);
    }
}

/// <summary>读取放大镜或鉴定服务当前选中的商品。</summary>
public static class ItemInspection
{
    public static GameItem CurrentInspectionItem()
    {
        try
        {
            var instance = Instance();
            if (!IsPanelActive(Ref.Get(instance, "panel"))) return null;
            var selected = Ref.Get(instance, "currentInspectingItem") as GameItem;
            return selected;
        }
        catch { return null; }
    }

    public static GameItem AppraisalServiceItem()
    {
        try
        {
            var instance = Instance();
            if (!IsPanelActive(Ref.Get(instance, "appraisalPanel"))) return null;
            return Ref.Get(instance, "currentInspectingItem") as GameItem;
        }
        catch { return null; }
    }

    private static object Instance()
    {
        var type = typeof(GameItem).Assembly.GetType("Il2Cpp.InspectionUIManager");
        return type?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
    }

    private static bool IsPanelActive(object panel)
    {
        if (panel == null) return false;
        var active = Ref.Get(panel, "activeInHierarchy") ?? Ref.Get(panel, "activeSelf");
        return active != null && Convert.ToBoolean(active.ToString());
    }
}

/// <summary>读取游戏鉴定系统计算的商品正确基准估值。</summary>
public static class AppraisalValue
{
    private static readonly Dictionary<long, long?> PriceCache = new();

    public static long? CorrectBasePrice(GameItem item)
    {
        if (item == null) return null;
        long pointer = Ref.Pointer(item);
        if (pointer != 0 && PriceCache.TryGetValue(pointer, out var cachedPrice)) return cachedPrice;

        long? price = ReadCorrectBasePrice(item);
        if (pointer != 0)
        {
            if (PriceCache.Count >= 256) PriceCache.Clear();
            PriceCache[pointer] = price;
        }
        return price;
    }

    private static long? ReadCorrectBasePrice(GameItem item)
    {
        try
        {
            var helper = typeof(GameItem).Assembly.GetType("Il2Cpp.AppraisalHelper");
            var method = helper?.GetMethod("GetCorrectBaseValue", BindingFlags.Public | BindingFlags.Static,
                                           null, new[] { typeof(GameItem) }, null);
            if (method == null) return null;
            long price = Convert.ToInt64(method.Invoke(null, new object[] { item }));
            return price > 0 ? price : null;
        }
        catch { return null; }
    }
}

/// <summary>读取放大镜当前选中商品的缺陷信息。</summary>
public static class InspectionDetails
{
    private static long _detailsItemPointer;
    private static string _detailsCache;
    private static bool _detailsRead;

    private static readonly (string Tag, string Label)[] CigaretteIssueTags =
    {
        ("CIGARETTE_BOX_ISSUE", "cigaretteIssueBox"),
        ("CIGARETTE_COLOR_ISSUE", "cigaretteIssueColor"),
        ("CIGARETTE_LETTERING_ISSUE", "cigaretteIssueLettering"),
        ("CIGARETTE_LOGO_ISSUE", "cigaretteIssueLogo"),
        ("CIGARETTE_SEAL_ISSUE", "cigaretteIssueSeal"),
    };

    private static readonly (string Tag, string Label)[] InjectorIssueTags =
    {
        ("ISSUE_BODY", "injectorIssueBody"),
        ("ISSUE_COLOR", "injectorIssueColor"),
        ("ISSUE_INDICATOR", "injectorIssueIndicator"),
        ("ISSUE_LABEL", "injectorIssueLabel"),
        ("ISSUE_SERIAL", "injectorIssueSerial"),
    };

    public static string Read(GameItem item)
    {
        if (item == null) return null;
        long pointer = Ref.Pointer(item);
        if (_detailsRead && pointer != 0 && pointer == _detailsItemPointer) return _detailsCache;

        _detailsItemPointer = pointer;
        _detailsRead = pointer != 0;
        _detailsCache = ReadDetails(item);
        return _detailsCache;
    }

    private static string ReadDetails(GameItem item)
    {
        try
        {
            string identity = ItemIdentity(item).ToLowerInvariant();
            bool isCigarette = identity.Contains("cig") || HasTag(item, "CIGARETTE_TAG");
            bool isInjector = IsInjector(item, identity);
            bool isRibwich = identity.Contains("ribwich");
            bool isStamp = IsStamp(item, identity);
            bool isAppHand = identity.Replace("_", "").Replace(" ", "").Contains("apphand");
            string helperName = isRibwich ? "Il2Cpp.RibwichHelper"
                : isStamp ? "Il2Cpp.InsStampHelper"
                : isInjector ? "Il2Cpp.InsInjectorHelper"
                : isCigarette ? "Il2Cpp.InsCigaretteHelper"
                : isAppHand ? "Il2Cpp.AppHandHelper"
                : null;

            if (isInjector)
            {
                var issues = IssueDetails(item, InjectorIssueTags);
                if (!string.IsNullOrWhiteSpace(issues)) return issues;
            }
            if (isCigarette)
            {
                var issues = IssueDetails(item, CigaretteIssueTags);
                if (!string.IsNullOrWhiteSpace(issues)) return issues;
            }

            // 各类商品优先用游戏自己的检查 helper；其它可检查商品走通用 helper。
            string details = helperName == null ? null : BuildHelperTooltip(helperName, "CreateDebugTooltip", item);
            if (string.IsNullOrWhiteSpace(details))
                details = BuildHelperTooltip("Il2Cpp.InspectableHelper", "CreateInspectableTooltip", item);
            if (string.IsNullOrWhiteSpace(details)) return null;

            details = string.Join(" ", details.Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
            return details.Length > 110 ? details.Substring(0, 107) + "…" : details;
        }
        catch { return null; }
    }

    private static string ItemIdentity(GameItem item)
    {
        return string.Join(" ", new[]
        {
            Ref.Get(item, "identifier", "")?.ToString(),
            Ref.Get(item, "identifierName", "")?.ToString(),
            Ref.Get(item, "name", "")?.ToString(),
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static bool IsInjector(GameItem item, string identity)
    {
        return HasTag(item, "INS_INJECTOR_TAG") || identity.Contains("inject") ||
               identity.Contains("immunivax") || identity.Contains("免疫宁");
    }

    private static bool IsStamp(GameItem item, string identity)
    {
        return HasTag(item, "INS_STAMP_TAG") || identity.Contains("stamp");
    }

    private static string IssueDetails(GameItem item, (string Tag, string Label)[] issueTags)
    {
        var reasons = new List<string>();
        foreach (var issue in issueTags)
            if (HasTag(item, issue.Tag)) reasons.Add(Loc.S(issue.Label));
        return reasons.Count == 0 ? null : string.Join(Loc.S("issueSeparator"), reasons);
    }

    private static string BuildHelperTooltip(string helperName, string methodName, GameItem item)
    {
        try
        {
            var assembly = typeof(GameItem).Assembly;
            var helper = assembly.GetType(helperName);
            var builderType = assembly.GetType("Il2Cpp.RichTextBuilder");
            if (helper == null || builderType == null) return null;

            var constructor = builderType.GetConstructor(new[] { typeof(int) });
            var builder = constructor?.Invoke(new object[] { 512 });
            if (builder == null) return null;

            var createTooltip = helper.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static,
                null, new[] { builderType, typeof(GameItem) }, null);
            if (createTooltip == null) return null;
            createTooltip.Invoke(null, new[] { builder, item });
            var build = builderType.GetMethod("Build", BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(int) }, null);
            var richText = build?.Invoke(builder, new object[] { 512 });
            string details = Ref.Get(richText, "nakedText")?.ToString();
            return string.IsNullOrWhiteSpace(details) ? null : details;
        }
        catch { return null; }
    }

    private static bool HasTag(GameItem item, string tag)
    {
        try
        {
            var state = Ref.Call1(item, "GetTagReadonly", tag);
            if (state != null)
            {
                var enabled = Ref.Call0(state, "IsEnabled");
                if (enabled != null) return Convert.ToBoolean(enabled.ToString());
                return Ref.GetBool(state, "valueEnabled");
            }

            return Convert.ToBoolean(Ref.Call1(item, "IsTag", tag)?.ToString() ?? "false");
        }
        catch { return false; }
    }
}

/// <summary>悬浮窗状态（HUD 读取）。</summary>
public static class Result
{
    public static string Title = "BestDeal";
    public static readonly List<string> Lines = new();
    public static readonly List<GameItem> PickedItems = new();
    public static int PickLineStart = -1;

    public static void Set(string title, IEnumerable<string> lines,
                           IEnumerable<GameItem> pickedItems = null, int pickLineStart = -1)
    {
        Title = title;
        Lines.Clear();
        Lines.AddRange(lines);
        PickedItems.Clear();
        if (pickedItems != null) PickedItems.AddRange(pickedItems);
        PickLineStart = PickedItems.Count > 0 ? pickLineStart : -1;
    }
}

public static class Patches
{
    internal static StoreClient CurrentClient;
    private static int _lastPollFrame;
    private static string _lastReqSig;     // 上次渲染时的需求指纹
    private static float _nextReqCheck;

    // 顾客开始对话 = 顾客已到柜台，自动计算一次
    [HarmonyPatch(typeof(StoreClient), "StartMainDialogue")]
    [HarmonyPostfix]
    static void OnClientArrived(StoreClient __instance)
    {
        CurrentClient = __instance;
        _lastReqSig = null;
        _nextReqCheck = 0f;
        _arrivedAt = Time.unscaledTime;
        _probedPtr = 0;
        Loc.Refresh();                      // 玩家可能刚在选项里换过语言
        PriceProbe.Stop(CurrentClient);
        TryRun(Loc.S("whatAuto"), Optimizer.Run);
        _lastReqSig = SafeSig(__instance);
    }

    // 交易结束清掉当前顾客（注意：interop 代理对象引用永不相同，必须比指针）
    [HarmonyPatch(typeof(StoreClient), "CompleteBusiness")]
    [HarmonyPostfix]
    static void OnBusinessDone(StoreClient __instance)
    {
        try
        {
            var cur = CurrentClient;
            if (cur == null) return;
            if ((long)((Il2CppObjectBase)cur).Pointer == (long)((Il2CppObjectBase)__instance).Pointer)
            {
                PriceProbe.Stop(CurrentClient);
                CurrentClient = null;
                _lastReqSig = null;
                _probedPtr = 0;
            }
        }
        catch { }
    }

    // 每帧轮询：挂游戏自己的 Update + HUD 自己的 Update（两个都在时靠同帧去重只跑一次）
    [HarmonyPatch(typeof(StoreClientCounter), "Update")]
    [HarmonyPostfix]
    internal static void Poll()
    {
        int frame = Time.frameCount;
        if (frame == _lastPollFrame) return;
        _lastPollFrame = frame;
        try
        {
            if (PriceProbe.Running) PriceProbe.Step(CurrentClient);   // 手里的东西先放回原位（在哪个场景都要放）
            if (EmporiumEntry.Instance == null) return;               // 不在店内场景（主菜单/读盘）：不扫库存、不重算
            PollRequirement();
            MaybeStartProbe();
        }
        catch (Exception e)
        {
            MelonLogger.Error($"Poll: {e}");
        }
    }

    /// <summary>
    /// 顾客需求是「到店之后」才填的（实测：StartMainDialogue 那一刻 clientBuyingTagList 为空、
    /// isRequireVariety=False，几秒后才有 HOUSEHOLD_GOOD 且 isRequireVariety=True），
    /// 所以按 0.5 秒轮询需求指纹，一变就重算，别让 HUD 停在「无限制」。
    /// </summary>
    static void PollRequirement()
    {
        var c = CurrentClient;
        if (c == null) { _lastReqSig = null; return; }
        if (Time.unscaledTime < _nextReqCheck) return;
        _nextReqCheck = Time.unscaledTime + 0.5f;

        var sig = SafeSig(c);
        if (sig == _lastReqSig) return;
        _lastReqSig = sig;
        Optimizer.Run();
    }

    /// <summary>需求填好了（或到柜台几秒后）就给候选物品排一轮"上柜台读价"，拿游戏算的真价。</summary>
    static void MaybeStartProbe()
    {
        var c = CurrentClient;
        if (c == null) { _probedPtr = 0; return; }
        if (PriceProbe.Running) return;
        if ((long)((Il2CppObjectBase)c).Pointer == _probedPtr) return;   // 这位顾客已经探过一轮
        if (Time.unscaledTime < _nextProbeCheck) return;
        _nextProbeCheck = Time.unscaledTime + 1f;

        bool hasReq;
        try { hasReq = Ref.Items(Ref.Get(c, "clientBuyingTagList")).Any() || Ref.Items(Ref.Get(c, "clientBuyingIdList")).Any(); }
        catch { hasReq = false; }
        if (!hasReq && Time.unscaledTime - _arrivedAt < 4f) return;   // 需求还是空的（可能还没填）就再等几秒

        var cands = Optimizer.CollectCandidates(c, Ref.GetBool(c, "isRequireVariety"));
        if (cands.Count == 0) return;
        // 只有真的排上队才记"这位顾客探过"——找不到柜台时不记，留着重试
        if (PriceProbe.Start(c, cands)) _probedPtr = (long)((Il2CppObjectBase)c).Pointer;
    }

    static long _probedPtr;   // 已经探过价的顾客（il2cpp 指针），离开时清零
    static float _nextProbeCheck;
    static float _arrivedAt;

    static string SafeSig(StoreClient c)
    {
        try { return Optimizer.RequirementSignature(c); }
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
            MelonLogger.Error(Loc.S("errRunFailed", what, e));
        }
    }
}

/// <summary>
/// 价格探测：游戏只在"物品被摆给当前顾客看"的时候才算最终价（实测：背包里读 55，摆上柜台后变 64/37）。
/// 所以这里把候选物品逐件放上柜台 → 读走游戏算好的价 → 立刻收回原库存（一件一件、每步校验）。
/// 只借价格，不改归属：源库存取物品自己的 parentInventory，收回后校验指针，收回失败立即停手并告警。
/// </summary>
public static class PriceProbe
{
    private static readonly Dictionary<long, long> Prices = new();   // il2cpp 指针 → 游戏算的最终价
    private static readonly List<GameItem> Queue = new();
    private static GameItem _cur;
    private static GameInventory _src, _dst;
    private static float _nextAt;
    private static int _total;                    // 本轮要探的件数（算进度用）
    private static List<string> _varietyBefore;   // 探测前顾客的多样性记录（探测后把新增项删掉，别污染判定）

    public static bool Running => _cur != null || Queue.Count > 0;

    /// <summary>探测进度（HUD 每帧直接画；为空表示没在探测）。</summary>
    public static string Status
    {
        get
        {
            if (!Running) return null;
            int done = Math.Max(0, _total - Queue.Count - (_cur != null ? 1 : 0));
            return Loc.S("probeStatus", done, _total);
        }
    }

    /// <summary>游戏给这件物品算出的最终价（没探测过返回 0）。</summary>
    public static long PriceFor(GameItem gi)
    {
        try { return Prices.TryGetValue((long)((Il2CppObjectBase)gi).Pointer, out var v) ? v : 0; }
        catch { return 0; }
    }

    /// <summary>推荐物品是否已经被玩家放到顾客可见的后柜台。</summary>
    public static bool IsOnCounter(GameItem gi)
    {
        try
        {
            var inv = Ref.Get(gi, "parentInventory") as GameInventory;
            var id = Ref.Get(inv, "identifier", "")?.ToString() ?? "";
            return id.IndexOf("back", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        catch { return false; }
    }

    /// <summary>给当前顾客安排一轮探测（每件约 0.25 秒）。找不到柜台就返回 false（什么也不动）。</summary>
    public static bool Start(StoreClient c, IEnumerable<GameItem> items)
    {
        var dst = FindCounter();
        if (dst == null)
        {
            MelonLogger.Warning(Loc.S("warnNoCounter"));
            return false;
        }
        Prices.Clear();
        Queue.Clear();
        _cur = null;
        _varietyBefore = SnapshotVariety(c);
        foreach (var gi in items.Take(16)) Queue.Add(gi);
        _total = Queue.Count;
        _dst = dst;
        _nextAt = Time.unscaledTime;
        return true;
    }

    public static void Stop(StoreClient c)
    {
        if (_cur != null) ReturnCurrent();          // 别把东西留在柜台上
        Queue.Clear();
        _total = 0;
        RestoreVariety(c);
    }

    /// <summary>由 StoreClientCounter.Update 的轮询驱动：一步一件。</summary>
    public static void Step(StoreClient c)
    {
        if (Time.unscaledTime < _nextAt) return;
        if (_dst == null) { _dst = FindCounter(); if (_dst == null) { Queue.Clear(); _total = 0; return; } }

        if (_cur == null)
        {
            if (Queue.Count == 0) return;
            var gi = Queue[0];
            Queue.RemoveAt(0);
            try { _src = Ref.Get(gi, "parentInventory") as GameInventory; } catch { _src = null; }
            if (_src == null) return;
            _cur = gi;
            try { _src.Expel(gi); } catch (Exception e) { MelonLogger.Warning(Loc.S("warnExpelFailed", NameOf(gi), e.Message)); _cur = null; return; }
            try { _dst.UncheckedAccept(gi); }
            catch (Exception e)
            {
                MelonLogger.Warning(Loc.S("warnPlaceFailed", e.Message));
                try { _src.UncheckedAccept(gi); } catch { }
                _cur = null;
                return;
            }
            _nextAt = Time.unscaledTime + 0.25f;    // 留一点时间给游戏算价
            return;
        }

        long price = 0;
        try { price = Ref.ToLong(Ref.Call0(_cur, "GetNegociatedValue"), 0); } catch { }
        if (price > 0) { try { Prices[(long)((Il2CppObjectBase)_cur).Pointer] = price; } catch { } }
        if (_cur != null) ReturnCurrent();
        _nextAt = Time.unscaledTime + 0.1f;
        if (Queue.Count == 0)
        {
            RestoreVariety(c);                                  // 一轮结束：清掉探测造成的"多样性记录"
            Patches.TryRun(Loc.S("whatReprobe"), Optimizer.Run);          // 用游戏真价重算 HUD
        }
    }

    /// <summary>把当前这件从柜台收回原库存，并校验真的回去了。</summary>
    static void ReturnCurrent()
    {
        var item = _cur;
        _cur = null;
        if (item == null) return;
        try
        {
            var where = Ref.Get(item, "parentInventory") as GameInventory;
            long wherePtr = Ptr(where), dstPtr = Ptr(_dst), srcPtr = Ptr(_src);
            if (wherePtr == srcPtr) return;                       // 本来就在原位
            if (_dst == null || wherePtr != dstPtr) return;        // 被顾客买走/挪走了：别去抢
            where.Expel(item);
            if (_src != null) _src.UncheckedAccept(item);
            var back = Ref.Get(item, "parentInventory") as GameInventory;
            if (Ptr(back) != srcPtr)
                MelonLogger.Warning(Loc.S("warnNotReturned", NameOf(item)));
        }
        catch (Exception e)
        {
            MelonLogger.Warning(Loc.S("warnReturnError", NameOf(item), e.Message));
        }
    }

    static long Ptr(object o) { try { return o == null ? 0 : (long)((Il2CppObjectBase)o).Pointer; } catch { return 0; } }

    static string NameOf(GameItem gi) { try { return Ref.Get(gi, "name", "?")?.ToString(); } catch { return "?"; } }

    /// <summary>柜台（后柜台）库存：顾客能看到的那一个。</summary>
    static GameInventory FindCounter()
    {
        try
        {
            foreach (var (inv, _) in ShopInventories.Enumerate())
            {
                var id = Ref.Get(inv, "identifier", "")?.ToString() ?? "";
                if (id.IndexOf("back", StringComparison.OrdinalIgnoreCase) >= 0) return inv;
            }
        }
        catch { }
        return null;
    }

    /// <summary>探测前的多样性记录（按字符串值比对：interop 代理对象每次都是新的，比引用不可靠）。</summary>
    static List<string> SnapshotVariety(StoreClient c)
    {
        var list = new List<string>();
        try { foreach (var x in Ref.Items(Ref.Get(c, "varietyTrackingList"))) list.Add(x?.ToString()); }
        catch { }
        return list;
    }

    /// <summary>探测会让顾客"见过"这些物品，把新增的多样性记录删掉，免得影响"要求不重复"的判定。</summary>
    static void RestoreVariety(StoreClient c)
    {
        if (c == null || _varietyBefore == null) return;
        try
        {
            var list = Ref.Get(c, "varietyTrackingList");
            if (list != null)
            {
                var now = new List<string>();
                foreach (var x in Ref.Items(list)) now.Add(x?.ToString());
                foreach (var x in now)
                {
                    if (_varietyBefore.Contains(x)) continue;      // 探测前就有的，保留
                    var mi = list.GetType().GetMethod("Remove", new[] { typeof(string) });
                    mi?.Invoke(list, new object[] { x });
                }
            }
        }
        catch { }
        _varietyBefore = null;
    }
}

/// <summary>读取 StoreClient 的需求（类别名用游戏自己的本地化表翻成中文）。</summary>
public static class ClientInfo
{
    public static string TagsSummary(StoreClient c)
    {
        var parts = new List<string>();
        foreach (var x in Ref.Items(Ref.Get(c, "clientBuyingTagList"))) parts.Add(LocalizeTag(x?.ToString()));
        foreach (var x in Ref.Items(Ref.Get(c, "clientBuyingIdList"))) parts.Add(LocalizeId(x?.ToString()));
        var s = string.Join("、", parts);
        return s.Length == 0 ? Loc.S("unlimited") : s;
    }

    /// <summary>物品 key → 中文类型名：type_household_good → 日用品（Mechanic 表）。查不到就用原值。</summary>
    static string LocalizeTag(string key)
    {
        if (string.IsNullOrEmpty(key)) return key ?? "";
        var loc = "type_" + key.ToLowerInvariant();
        try
        {
            var s = LocHelper.GetLocalizedMechanic(loc);
            // 本地化失败时游戏返回 key 本身或空串
            if (!string.IsNullOrEmpty(s) && s != loc) return s;
        }
        catch { }
        return key;
    }

    /// <summary>物品 id → 中文名：item_scrap_metal_name → 废金属（Item 表）。查不到就用原值。</summary>
    static string LocalizeId(string id)
    {
        if (string.IsNullOrEmpty(id)) return id ?? "";
        var loc = "item_" + id + "_name";
        try
        {
            var s = LocHelper.GetLocalizedItem(loc);
            if (!string.IsNullOrEmpty(s) && s != loc) return s;
        }
        catch { }
        return id;
    }
}

/// <summary>反射小工具。</summary>
public static class Ref
{
    public static long Pointer(object o)
    {
        try { return o == null ? 0 : (long)((Il2CppObjectBase)o).Pointer; }
        catch { return 0; }
    }

    public static object Get(object o, string prop, object def = null)
    {
        try
        {
            var p = o?.GetType().GetProperty(prop);
            if (p != null) return p.GetValue(o);
            var f = o?.GetType().GetField(prop);
            if (f != null) return f.GetValue(o);
        }
        catch { }
        return def;
    }

    /// <summary>调无参方法（拿不到返回 null）。</summary>
    public static object Call0(object o, string name)
    {
        try
        {
            var m = o?.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.Instance,
                                           null, Type.EmptyTypes, null);
            return m?.Invoke(o, null);
        }
        catch { return null; }
    }

    /// <summary>调一个参数的方法。</summary>
    public static object Call1(object o, string name, object arg)
    {
        try
        {
            var m = o?.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.Instance,
                                           null, new[] { typeof(string) }, null);
            return m?.Invoke(o, new[] { arg });
        }
        catch { return null; }
    }

    public static bool GetBool(object o, string prop, bool def = false)
    {
        try
        {
            var v = Get(o, prop, null);
            return v == null ? def : Convert.ToBoolean(v.ToString());
        }
        catch { return def; }
    }

    public static long ToLong(object o, long def = 0)
    {
        try { return o == null ? def : Convert.ToInt64(o.ToString()); }
        catch { return def; }
    }

    /// <summary>il2cpp List 或数组，通用枚举（Count/索引器反射）。</summary>
    public static IEnumerable<object> Items(object list)
    {
        if (list == null) yield break;
        var t = list.GetType();
        var countProp = t.GetProperty("Count") ?? t.GetProperty("length") ?? t.GetProperty("Length");
        if (countProp == null) yield break;
        int n = Convert.ToInt32(countProp.GetValue(list));
        var itemProp = t.GetProperty("Item");
        for (int i = 0; i < n; i++)
        {
            if (itemProp != null)
                yield return itemProp.GetValue(list, new object[] { i });
            else if (list is System.Collections.IEnumerable en)
            {
                int j = 0;
                foreach (var x in en) { if (j++ == i) { yield return x; break; } }
            }
        }
    }
}

/// <summary>候选商品 + 分组背包优化（只读）。</summary>
public static class Optimizer
{
    private static int _unpriced; // 没探到游戏真价、未参与推荐的候选数

    class Cand
    {
        public GameItem Item;
        public string Name;
        public string GroupKey; // 同类型标识（identifier；空则用名字）
        public long Count;
        public long Price;   // 游戏自己算出来的最终成交价（摆上柜台探测得到）
    }

    public static void Run()
    {
        var c = Patches.CurrentClient;
        if (c == null)
        {
            Result.Set("BestDeal", new[] { Loc.S("noCustomer") });
            return;
        }

        var name = Ref.Get(c, "displayName", "?")?.ToString();
        bool requireVariety = Ref.GetBool(c, "isRequireVariety");
        var cands = Collect(c, requireVariety, needPrice: true);
        if (cands.Count == 0)
        {
            // 一样都没定价：可能只是还没探测（本来就会自动探），别急着说"他不买"
            string note = PriceProbe.Running
                ? Loc.S("probingNow")
                : (_unpriced > 0 ? Loc.S("unpricedRetry", _unpriced)
                                 : Loc.S("buysNothing"));
            Result.Set($"BestDeal — {name}", new[]
            {
                Loc.S("head", name, SafeGetBudget(c), ClientInfo.TagsSummary(c)),
                note,
            });
            return;
        }

        long budget = Ref.ToLong(SafeGetBudget(c), 0);
        if (budget <= 0) budget = long.MaxValue / 4;

        // 分组：要求不重复 → 同类最多 1 件；否则每件独立
        List<List<Cand>> groups = requireVariety
            ? cands.GroupBy(x => x.GroupKey).Select(g => g.ToList()).ToList()
            : cands.Select(x => new List<Cand> { x }).ToList();

        // 0/1 分组背包：容量=预算，每组最多取 1 件，最大化售出总额。
        // 重量必须用 cap+1 表示「放不下」——若截断到 cap，超预算的物品会被当成"刚好花光预算"，
        // 结果反而比预算内的物品更"划算"而被选中（顾客根本买不起）。
        long scale = Math.Max(1, budget / 2_000_000 + 1);
        int cap = (int)(budget / scale);
        int Weight(long price) => (int)Math.Min(cap + 1L, price / scale);
        var dp = new long[cap + 1];
        var choice = new int[groups.Count][];

        for (int g = 0; g < groups.Count; g++)
        {
            var items = groups[g];
            var w = new int[items.Count];
            for (int k = 0; k < items.Count; k++) w[k] = Weight(items[k].Price);

            var prev = (long[])dp.Clone(); // 上一组的最优值（本组只能取 1 件）
            var row = new int[cap + 1];
            for (int j = 0; j <= cap; j++)
            {
                long best = prev[j];
                int bestK = -1;
                for (int k = 0; k < items.Count; k++)
                {
                    if (w[k] <= j)
                    {
                        long cand = prev[j - w[k]] + items[k].Price;
                        if (cand > best) { best = cand; bestK = k; }
                    }
                }
                dp[j] = best;
                row[j] = bestK;
            }
            choice[g] = row;
        }

        var picked = new List<Cand>();
        int pos = cap;
        for (int g = groups.Count - 1; g >= 0; g--)
        {
            int k = choice[g][pos];
            if (k >= 0)
            {
                var it = groups[g][k];
                picked.Add(it);
                pos -= Weight(it.Price);
            }
        }

        string head = Loc.S("head", name, SafeGetBudget(c), ClientInfo.TagsSummary(c)) +
                      (requireVariety ? Loc.S("requireVariety") : "");
        string skippedNote = _unpriced > 0 ? Loc.S("skippedUnpriced", _unpriced) : "";

        // 候选都在预算之外：顾客一件也买不起（别推荐超预算的东西）
        if (picked.Count == 0)
        {
            Result.Set($"BestDeal — {name}", new[]
            {
                head,
                Loc.S("tooExpensive", cands.Count, cands.Min(x => x.Price)),
            });
            return;
        }

        long total = 0;
        foreach (var p in picked) total += p.Price;

        var lines = new List<string>
        {
            head,
            Loc.S("totalSold", total) +
            (Ref.ToLong(SafeGetBudget(c), 0) > 0 ? Loc.S("budgetLeft", Math.Max(0, budget - total)) : "") +
            Loc.S("candidateCount", cands.Count) + skippedNote,
        };
        foreach (var p in picked)
            lines.Add(Loc.S("pickLine", p.Name, p.Count, p.Price));
        lines.Add(Loc.S("pickCount", picked.Count));

        Result.Set($"BestDeal — {name}", lines, picked.Select(p => p.Item), pickLineStart: 2);
    }

    static object SafeGetBudget(StoreClient c)
    {
        try { return c.GetBudget(); } catch { return Ref.Get(c, "clientBudget"); }
    }

    /// <summary>
    /// 需求指纹：顾客的类别/黑名单/预算/约束任意一项变了就说明游戏刚填完需求（见 Patches.PollRequirement）。
    /// </summary>
    internal static string RequirementSignature(StoreClient c)
    {
        var parts = new List<string>
        {
            Ref.Get(c, "identifier", "")?.ToString() ?? "",
            Ref.ToLong(SafeGetBudget(c), 0).ToString(),
            Ref.GetBool(c, "isRequireVariety") ? "V" : "-",
            Ref.GetBool(c, "acceptRetailMarkup") ? "R" : "-",
            Ref.GetBool(c, "isOfferWholesale") ? "W" : "-",
        };
        foreach (var x in Ref.Items(Ref.Get(c, "clientBuyingTagList"))) parts.Add("t" + x);
        foreach (var x in Ref.Items(Ref.Get(c, "clientBuyingIdList"))) parts.Add("i" + x);
        foreach (var x in Ref.Items(Ref.Get(c, "clientBlackTagList"))) parts.Add("bt" + x);
        foreach (var x in Ref.Items(Ref.Get(c, "clientBlackIdList"))) parts.Add("bi" + x);
        // 顾客的买方加成特性（可能是到店后才挂上的，加了它 HUD 才会跟着刷新价格）
        foreach (var x in Ref.Items(Ref.Get(c, "clientItemFeatureBuying")))
            parts.Add("f" + x);   // 顾客的买方特性（折价/多样化等）会影响价格，变了就重算
        return string.Join("|", parts);
    }

    /// <summary>候选物品（给价格探测排队用：与 Collect 同一套筛选，但**不要求已经探到价**）。</summary>
    internal static List<GameItem> CollectCandidates(StoreClient c, bool requireVariety)
    {
        // 探测按"游戏估值从高到低"排队：超过上限时先探值钱的，尾部的低价值物品不参与推荐
        return Collect(c, requireVariety, needPrice: false)
            .OrderByDescending(x => ValueOf(x.Item))
            .Select(x => x.Item)
            .ToList();
    }

    static long ValueOf(GameItem gi)
    {
        long v = Ref.ToLong(Ref.Call0(gi, "GetNegociatedValue"), 0);
        if (v <= 0) v = Ref.ToLong(Ref.Get(gi, "unitValue"), 0);
        return v;
    }

    /// <summary>
    /// 扫描各售卖/仓储区收集物品；排除已售/垃圾桶/中转等非售卖区。
    /// needPrice=true（HUD 推荐）只保留已探到游戏价的物品；needPrice=false（给探测排队）不做价格过滤——
    /// 否则就成了"要先探到价，才肯去探价"的死结（v1.4.0 的实际 bug：探测一步都没跑过）。
    /// </summary>
    static List<Cand> Collect(StoreClient c, bool requireVariety, bool needPrice)
    {
        var result = new List<Cand>();
        var seenPtr = new HashSet<long>();
        _unpriced = 0;

        foreach (var (inv, zone) in ShopInventories.Enumerate())
        {
            string invName = Ref.Get(inv, "identifier", "")?.ToString() ?? "";
            if (string.IsNullOrEmpty(invName)) invName = zone;
            foreach (var raw in Ref.Items(Ref.Get(inv, "childItems") ?? Ref.Get(inv, "children")))
            {
                var gi = raw as GameItem;
                if (gi == null) continue;

                bool ok;
                try { ok = c.IsClientBuyingThisItem(gi); } catch { ok = false; }
                if (!ok) continue;

                var ptr = (long)((Il2CppObjectBase)gi).Pointer;
                if (!seenPtr.Add(ptr)) continue;

                // 游戏自己的重复判定：已在多样性列表里 → 这位顾客不会再要同类
                bool tracked = false;
                try { tracked = c.IsItemInVarietyList(gi); } catch { }
                if (requireVariety && tracked) continue;

                long cnt = Ref.ToLong(Ref.Get(gi, "unitCount"), 1);
                if (cnt <= 0) cnt = 1;

                // 价格只认"摆上柜台探测"到的游戏真价；没探到（超出探测上限等）就不推荐它
                long price = PriceProbe.PriceFor(gi);
                if (price <= 0 && needPrice)
                {
                    _unpriced++;
                    continue;
                }

                var gk = Ref.Get(gi, "identifier")?.ToString();
                if (string.IsNullOrEmpty(gk)) gk = Ref.Get(gi, "name", "?")?.ToString();
                result.Add(new Cand
                {
                    Item = gi,
                    Name = $"{Ref.Get(gi, "name", "?")}[{invName}]",
                    GroupKey = gk,
                    Count = cnt,
                    Price = price,
                });
            }
        }
        return result;
    }
}

/// <summary>找店内售卖/仓储库存（排除已售/垃圾桶/中转/隐藏等区域）。</summary>
public static class ShopInventories
{
    // 这些区域不放可售商品：soldElement(已售陈列)、trash、drain、hidden、response(交互暂存)、
    // swapBuffer(拖拽缓冲)、faucet/cassette/vending(设施)、afterhour(打烊区)
    private static readonly string[] Blocked = { "sold", "trash", "drain", "hidden", "response", "swap", "faucet", "cassette", "vending", "afterhour" };

    static bool IsBlocked(string propName)
    {
        var low = propName.ToLowerInvariant();
        return Blocked.Any(b => low.Contains(b));
    }

    public static IEnumerable<(GameInventory inv, string zone)> Enumerate()
    {
        var entry = EmporiumEntry.Instance;
        if (entry == null)
        {
            MelonLogger.Warning(Loc.S("warnNoEmporium"));
            yield break;
        }

        var seenPtr = new HashSet<long>();
        foreach (var (found, zone) in Walk(entry, 2, "shop"))
        {
            var p = (long)((Il2CppObjectBase)found).Pointer;
            if (seenPtr.Add(p)) yield return (found, zone);
        }
    }

    static IEnumerable<(GameInventory inv, string zone)> Walk(object node, int depth, string zone)
    {
        if (node == null || depth < 0) yield break;
        var t = node.GetType();
        foreach (var p in t.GetProperties())
        {
            if (IsBlocked(p.Name)) continue;
            object v;
            try { v = p.GetValue(node); } catch { continue; }
            if (v == null || v is string || v.GetType().IsPrimitive) continue;

            if (v is GameInventory inv)
            {
                yield return (inv, Strip(zone));
                foreach (var sub in Walk(inv, depth - 1, zone)) yield return sub;
            }
            else if (depth > 0 && (p.Name.EndsWith("Window") || p.Name.EndsWith("Element") ||
                                   p.Name.EndsWith("Inventory") || p.Name.EndsWith("element")))
            {
                foreach (var sub in Walk(v, depth - 1, Strip(p.Name))) yield return sub;
            }
        }
    }

    static string Strip(string s) => s.Replace("Window", "").Replace("Element", "");
}
