using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace BestDeal;

/// <summary>
/// BestDeal — 顾客上门时按类别+预算计算最佳售卖组合，HUD 显示推荐清单。
/// 游戏原生 mod（IMod + manifest.xml，Harmony 补丁由游戏加载器自动应用，无需 PatchAll）。
/// 价格一律用游戏自己算的：物品**摆上柜台招待这位顾客**时游戏才会算出最终成交价（实测：背包里读 55，
/// 摆上柜台变 64/37），所以 mod 会把候选物品逐件快速摆上柜台读走价格再收回（见 PriceProbe），
/// 而不是自己按加价公式估算。
/// 需求同样读游戏自己的字段：clientBuyingTagList / clientBuyingIdList（到店几秒后才填，轮询刷新）、
/// isRequireVariety + IsItemInVarietyList（要求不重复的顾客每类最多推荐 1 件）。
/// </summary>
public class BestDealMod : IMod
{
    internal static ModLog Log;

    public void Init(ModManifest manifest)
    {
        Log = new ModLog(manifest);
        Log.Log("BestDeal v2.0.0 已加载");
    }

    public void OnEnable()
    {
        EnsureHud();
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

/// <summary>悬浮窗状态（HUD 读取）。</summary>
public static class Result
{
    public static string Title = "BestDeal";
    public static readonly List<string> Lines = new();
    public static void Set(string title, IEnumerable<string> lines)
    {
        Title = title;
        Lines.Clear();
        Lines.AddRange(lines);
    }
}

/// <summary>游戏内悬浮窗（只负责画）。</summary>
public class Hud : MonoBehaviour
{
    internal static Hud Instance;

    private void Awake()
    {
        Instance = this;
    }

    // 轮询唯一的驱动：这个 GameObject 是 DontDestroyOnLoad 的，主菜单/店内/读盘每帧都会跑
    private void Update()
    {
        Patches.Poll();
    }

    private void OnGUI()
    {
        var status = PriceProbe.Status;                       // 探测进度：每帧直接读，不走 Optimizer
        int total = Result.Lines.Count + (status == null ? 0 : 1);
        if (total == 0) return;
        float width = Mathf.Min(560f, Screen.width - 32f);
        var rect = new Rect(16, 16, width, 64 + total * 22);
        GUI.Box(rect, Result.Title);
        int i = 0;
        for (; i < Result.Lines.Count; i++)
            GUI.Label(new Rect(30, 46 + i * 22, rect.width - 28, 22), Result.Lines[i]);
        if (status != null)
            GUI.Label(new Rect(30, 46 + i * 22, rect.width - 28, 22), status);
    }
}

public static class Patches
{
    internal static StoreClient CurrentClient;
    private static string _lastReqSig;     // 上次渲染时的需求指纹
    private static float _nextReqCheck;

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
        TryRun("自动计算", Optimizer.Run);
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
    internal static void Poll()
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
            BestDealMod.Log.Error($"Poll: {e}");
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
        if (c == null) { _probedClient = null; return; }
        if (PriceProbe.Running) return;
        if (ReferenceEquals(c, _probedClient)) return;   // 这位顾客已经探过一轮
        if (Time.unscaledTime < _nextProbeCheck) return;
        _nextProbeCheck = Time.unscaledTime + 1f;

        bool hasReq = (c.clientBuyingTagList != null && c.clientBuyingTagList.Count > 0)
                   || (c.clientBuyingIdList != null && c.clientBuyingIdList.Count > 0);
        if (!hasReq && Time.unscaledTime - _arrivedAt < 4f) return;   // 需求还是空的（可能还没填）就再等几秒

        var cands = Optimizer.CollectCandidates(c, c.isRequireVariety);
        if (cands.Count == 0) return;
        // 只有真的排上队才记"这位顾客探过"——找不到柜台时不记，留着重试
        if (PriceProbe.Start(c, cands)) _probedClient = c;
    }

    static StoreClient _probedClient;   // 已经探过价的顾客，离开时清掉
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
            BestDealMod.Log.Error($"[{what}] 失败：{e}");
        }
    }
}

// Harmony 补丁由游戏模组加载器自动应用（扫描 [HarmonyPatch] 类），不要自己 PatchAll
[HarmonyPatch(typeof(StoreClient), nameof(StoreClient.StartMainDialogue))]
public static class StoreClient_StartMainDialogue_Patch
{
    static void Postfix(StoreClient __instance) => Patches.OnClientArrived(__instance);
}

[HarmonyPatch(typeof(StoreClient), nameof(StoreClient.CompleteBusiness))]
public static class StoreClient_CompleteBusiness_Patch
{
    static void Postfix(StoreClient __instance) => Patches.OnBusinessDone(__instance);
}

/// <summary>
/// 价格探测：游戏只在"物品被摆给当前顾客看"的时候才算最终价（实测：背包里读 55，摆上柜台后变 64/37）。
/// 所以这里把候选物品逐件放上柜台 → 读走游戏算好的价 → 立刻收回原库存（一件一件、每步校验）。
/// 只借价格，不改归属：源库存取物品自己的 parentInventory，收回后校验，收回失败立即停手并告警。
/// 柜台用 EmporiumEntry.backInvinvElement——游戏自己的 MoveItemToBackCounter 就是往这里放。
/// </summary>
public static class PriceProbe
{
    private static readonly Dictionary<GameItem, long> Prices = new();   // 物品 → 游戏算的最终价
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
            return $"价格探测中 {done}/{_total}：物品会短暂摆上柜台，读完价自动收回";
        }
    }

    /// <summary>游戏给这件物品算出的最终价（没探测过返回 0）。</summary>
    public static long PriceFor(GameItem gi)
    {
        try { return Prices.TryGetValue(gi, out var v) ? v : 0; }
        catch { return 0; }
    }

    /// <summary>给当前顾客安排一轮探测（每件约 0.25 秒）。找不到柜台就返回 false（什么也不动）。</summary>
    public static bool Start(StoreClient c, IEnumerable<GameItem> items)
    {
        var dst = FindCounter();
        if (dst == null)
        {
            BestDealMod.Log.Warning("价格探测：没找到后柜台（backInvinvElement），本轮跳过");
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

    /// <summary>由轮询驱动：一步一件。</summary>
    public static void Step(StoreClient c)
    {
        if (Time.unscaledTime < _nextAt) return;
        if (_dst == null) { _dst = FindCounter(); if (_dst == null) { Queue.Clear(); _total = 0; return; } }

        if (_cur == null)
        {
            if (Queue.Count == 0) return;
            var gi = Queue[0];
            Queue.RemoveAt(0);
            try { _src = gi.parentInventory; } catch { _src = null; }
            if (_src == null) return;
            _cur = gi;
            try { _src.Expel(gi); } catch (Exception e) { BestDealMod.Log.Warning($"价格探测：取出 {NameOf(gi)} 失败：{e.Message}"); _cur = null; return; }
            try { _dst.UncheckedAccept(gi); }
            catch (Exception e)
            {
                BestDealMod.Log.Warning($"价格探测：上柜台失败，已放回：{e.Message}");
                try { _src.UncheckedAccept(gi); } catch { }
                _cur = null;
                return;
            }
            _nextAt = Time.unscaledTime + 0.25f;    // 留一点时间给游戏算价
            return;
        }

        long price = 0;
        try { price = _cur.GetNegociatedValue(); } catch { }
        if (price > 0) { try { Prices[_cur] = price; } catch { } }
        if (_cur != null) ReturnCurrent();
        _nextAt = Time.unscaledTime + 0.1f;
        if (Queue.Count == 0)
        {
            RestoreVariety(c);                                  // 一轮结束：清掉探测造成的"多样性记录"
            Patches.TryRun("探测后重算", Optimizer.Run);          // 用游戏真价重算 HUD
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
            var where = item.parentInventory;
            if (where == null) return;
            if (ReferenceEquals(where, _src)) return;             // 本来就在原位
            if (_dst == null || !ReferenceEquals(where, _dst)) return;   // 被顾客买走/挪走了：别去抢
            where.Expel(item);
            if (_src != null) _src.UncheckedAccept(item);
            var back = item.parentInventory;
            if (!ReferenceEquals(back, _src))
                BestDealMod.Log.Warning($"价格探测：{NameOf(item)} 没能放回原处，快检查柜台！");
        }
        catch (Exception e)
        {
            BestDealMod.Log.Warning($"价格探测：收回 {NameOf(item)} 出错：{e.Message}");
        }
    }

    static string NameOf(GameItem gi) { try { return gi.name ?? "?"; } catch { return "?"; } }

    /// <summary>柜台（后柜台）库存：顾客能看到的那一个。</summary>
    static GameInventory FindCounter()
    {
        try { return EmporiumEntry.Instance != null ? EmporiumEntry.Instance.backInvinvElement : null; }
        catch { return null; }
    }

    /// <summary>探测前的多样性记录。</summary>
    static List<string> SnapshotVariety(StoreClient c)
    {
        try { return c?.varietyTrackingList != null ? new List<string>(c.varietyTrackingList) : null; }
        catch { return null; }
    }

    /// <summary>探测会让顾客"见过"这些物品，把新增的多样性记录删掉，免得影响"要求不重复"的判定。</summary>
    static void RestoreVariety(StoreClient c)
    {
        if (c == null || _varietyBefore == null) return;
        try
        {
            var list = c.varietyTrackingList;
            if (list != null)
            {
                foreach (var x in new List<string>(list))
                    if (!_varietyBefore.Contains(x)) list.Remove(x);
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
        if (c.clientBuyingTagList != null) foreach (var x in c.clientBuyingTagList) parts.Add(LocalizeTag(x));
        if (c.clientBuyingIdList != null) foreach (var x in c.clientBuyingIdList) parts.Add(LocalizeId(x));
        var s = string.Join("、", parts);
        return s.Length == 0 ? "无限制" : s;
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
            Result.Set("BestDeal", new[] { "暂无顾客（等顾客上门后自动提示）" });
            return;
        }

        var name = string.IsNullOrEmpty(c.displayName) ? "?" : c.displayName;
        bool requireVariety = c.isRequireVariety;
        var cands = Collect(c, requireVariety, needPrice: true);
        if (cands.Count == 0)
        {
            // 一样都没定价：可能只是还没探测（本来就会自动探），别急着说"他不买"
            string note = PriceProbe.Running
                ? "正在逐件摆上柜台探测价格…（探完自动更新）"
                : (_unpriced > 0 ? $"候选 {_unpriced} 件，价格还没探到（自动重探中）"
                                 : "他不会买你店里的任何东西");
            Result.Set($"BestDeal — {name}", new[]
            {
                $"顾客：{name}  预算：{GetBudgetSafe(c)}  类别：{ClientInfo.TagsSummary(c)}",
                note,
            });
            return;
        }

        long budget = GetBudgetSafe(c);
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

        string head = $"顾客：{name}  预算：{GetBudgetSafe(c)}  类别：{ClientInfo.TagsSummary(c)}" +
                      (requireVariety ? "  [要求不重复]" : "");
        string skippedNote = _unpriced > 0 ? $"（另 {_unpriced} 件未探到价已跳过）" : "";

        // 候选都在预算之外：顾客一件也买不起（别推荐超预算的东西）
        if (picked.Count == 0)
        {
            Result.Set($"BestDeal — {name}", new[]
            {
                head,
                $"候选 {cands.Count} 件，但最便宜的也要 {cands.Min(x => x.Price)}，超出预算，他一件也买不起",
            });
            return;
        }

        long total = 0;
        foreach (var p in picked) total += p.Price;

        var lines = new List<string>
        {
            head,
            $"可售出总额：{total}" +
            (GetBudgetSafe(c) > 0 ? $"（剩预算 {Math.Max(0, budget - total)}）" : "") +
            $"，候选 {cands.Count} 件" + skippedNote,
        };
        foreach (var p in picked)
            lines.Add($"→ 拿上柜台：{p.Name} ×{p.Count} = {p.Price}");
        lines.Add($"共 {picked.Count} 件");

        Result.Set($"BestDeal — {name}", lines);
    }

    static long GetBudgetSafe(StoreClient c)
    {
        try { return c.GetBudget(); } catch { return 0; }
    }

    /// <summary>
    /// 需求指纹：顾客的类别/黑名单/预算/约束任意一项变了就说明游戏刚填完需求（见 Patches.PollRequirement）。
    /// </summary>
    internal static string RequirementSignature(StoreClient c)
    {
        var parts = new List<string>
        {
            c.identifier ?? "",
            GetBudgetSafe(c).ToString(),
            c.isRequireVariety ? "V" : "-",
            c.acceptRetailMarkup ? "R" : "-",
            c.isOfferWholesale ? "W" : "-",
        };
        if (c.clientBuyingTagList != null) foreach (var x in c.clientBuyingTagList) parts.Add("t" + x);
        if (c.clientBuyingIdList != null) foreach (var x in c.clientBuyingIdList) parts.Add("i" + x);
        if (c.clientBlackTagList != null) foreach (var x in c.clientBlackTagList) parts.Add("bt" + x);
        if (c.clientBlackIdList != null) foreach (var x in c.clientBlackIdList) parts.Add("bi" + x);
        // 顾客的买方加成特性（可能是到店后才挂上的，加了它 HUD 才会跟着刷新价格）
        if (c.clientItemFeatureBuying != null)
            foreach (var x in c.clientItemFeatureBuying)
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
        long v = 0;
        try { v = gi.GetNegociatedValue(); } catch { }
        if (v <= 0) v = gi.unitValue;
        return v;
    }

    /// <summary>
    /// 扫描各售卖/仓储区收集物品；只扫玩家能拿去卖的存储区。
    /// needPrice=true（HUD 推荐）只保留已探到游戏价的物品；needPrice=false（给探测排队）不做价格过滤——
    /// 否则就成了"要先探到价，才肯去探价"的死结（v1.4.0 的实际 bug：探测一步都没跑过）。
    /// </summary>
    static List<Cand> Collect(StoreClient c, bool requireVariety, bool needPrice)
    {
        var result = new List<Cand>();
        var seen = new HashSet<GameItem>();
        _unpriced = 0;

        foreach (var (inv, zone) in ShopInventories.Enumerate())
        {
            string invName = string.IsNullOrEmpty(inv.identifier) ? zone : inv.identifier;
            var items = inv.childItems;
            if (items == null) continue;
            foreach (var gi in items)
            {
                if (gi == null) continue;

                bool ok;
                try { ok = c.IsClientBuyingThisItem(gi); } catch { ok = false; }
                if (!ok) continue;

                if (!seen.Add(gi)) continue;

                // 游戏自己的重复判定：已在多样性列表里 → 这位顾客不会再要同类
                bool tracked = false;
                try { tracked = c.IsItemInVarietyList(gi); } catch { }
                if (requireVariety && tracked) continue;

                long cnt = gi.unitCount;
                if (cnt <= 0) cnt = 1;

                // 价格只认"摆上柜台探测"到的游戏真价；没探到（超出探测上限等）就不推荐它
                long price = PriceProbe.PriceFor(gi);
                if (price <= 0 && needPrice)
                {
                    _unpriced++;
                    continue;
                }

                var gk = gi.identifier;
                if (string.IsNullOrEmpty(gk)) gk = gi.name ?? "?";
                result.Add(new Cand
                {
                    Item = gi,
                    Name = $"{gi.name}[{invName}]",
                    GroupKey = gk,
                    Count = cnt,
                    Price = price,
                });
            }
        }
        return result;
    }
}

/// <summary>找店内售卖/仓储库存（EmporiumEntry 上玩家能拿去卖的区域，排除已售/垃圾桶/中转/设施等）。</summary>
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
}
