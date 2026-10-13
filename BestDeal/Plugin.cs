using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace BestDeal;

/// <summary>
/// BestDeal — 顾客上门时按类别+预算计算最佳售卖组合，HUD 显示推荐清单（F8 重算 F9 显隐）。
/// 只读不写：绝不移动或修改游戏状态。
/// 计价（实测取证）：售价 = 单价 × (1 + 零售加价% + 多样化卖家加成%)，整数定点 + 银行家舍入；
/// 数值读 PlayerStore.retailMarkup 与 ItemFeatureList.VarietyBuyerBonus().GetCurrentValueMod()。
/// 重复商品约束：读游戏自己的 StoreClient.isRequireVariety / IsItemInVarietyList，
/// 要求不重复的顾客每类最多推荐 1 件（分组背包）。
/// </summary>
[BepInPlugin("guozr.skdp.bestdeal", "BestDeal", "1.0.0")]
public class Plugin : BasePlugin
{
    internal static new ManualLogSource Log;
    private Harmony _harmony;

    public override void Load()
    {
        Log = base.Log;
        _harmony = new Harmony("guozr.skdp.bestdeal");
        _harmony.PatchAll(typeof(Patches));

        ClassInjector.RegisterTypeInIl2Cpp<Hud>();
        Log.LogInfo("BestDeal v1.0 已加载：顾客上门自动提示最佳组合。F8 重算，F9 显隐");
    }

    internal static void EnsureHud()
    {
        if (Hud.Instance != null) return;
        try
        {
            var go = new GameObject("BestDealHud");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent(Il2CppInterop.Runtime.Il2CppType.Of<Hud>()).TryCast<Hud>();
        }
        catch (Exception e)
        {
            Log.LogWarning($"HUD 创建失败：{e.Message}");
        }
    }
}

/// <summary>悬浮窗状态（HUD 读取）。</summary>
public static class Result
{
    public static string Title = "BestDeal";
    public static readonly List<string> Lines = new();
    public static bool Visible = true;

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
    public Hud(IntPtr h) : base(h) { }
    internal static Hud Instance;

    private void Awake()
    {
        Instance = this;
    }

    private void OnGUI()
    {
        if (!Result.Visible || Result.Lines.Count == 0) return;
        var rect = new Rect(16, 16, 700, 64 + Result.Lines.Count * 22);
        GUI.Box(rect, Result.Title);
        for (int i = 0; i < Result.Lines.Count; i++)
            GUI.Label(new Rect(30, 46 + i * 22, rect.width - 28, 22), Result.Lines[i]);
    }
}

public static class Patches
{
    internal static StoreClient CurrentClient;
    private static int _lastHotkeyFrame;

    // 顾客开始对话 = 顾客已到柜台，自动计算一次
    [HarmonyPatch(typeof(StoreClient), "StartMainDialogue")]
    [HarmonyPostfix]
    static void OnClientArrived(StoreClient __instance)
    {
        CurrentClient = __instance;
        Plugin.EnsureHud();
        TryRun("抓取顾客信息", () => ClientInfo.Dump(__instance));
        TryRun("自动计算", Optimizer.Run);
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
                CurrentClient = null;
                Plugin.Log.LogInfo($"[顾客] {Ref.Get(__instance, "displayName", "?")} 交易结束");
            }
        }
        catch { }
    }

    // 热键轮询（挂游戏自己的 Update；带同帧去重防多实例）
    [HarmonyPatch(typeof(StoreClientCounter), "Update")]
    [HarmonyPostfix]
    static void PollHotkey()
    {
        int frame = Time.frameCount;
        if (frame == _lastHotkeyFrame) return;
        try
        {
            if (Input.GetKeyDown(KeyCode.F8)) { _lastHotkeyFrame = frame; Optimizer.Run(); }
            else if (Input.GetKeyDown(KeyCode.F9)) { _lastHotkeyFrame = frame; Result.Visible = !Result.Visible; }
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"PollHotkey: {e}");
        }
    }

    /// <summary>静默对账守卫：成交价与预测不符才出声（含两个方向：卖出=顾客购买，买入=顾客卖给玩家）。</summary>
    [HarmonyPatch(typeof(StoreClient), "OnItemBought")]
    [HarmonyPostfix]
    static void OnItemBoughtPost(GameItem __0, int __1) => CheckDeal("卖出", __0, __1);

    [HarmonyPatch(typeof(StoreClient), "OnItemSold")]
    [HarmonyPostfix]
    static void OnItemSoldPost(GameItem __0, int __1) => CheckDeal("买入", __0, __1);

    static void CheckDeal(string tag, GameItem item, int paid)
    {
        try
        {
            if (CurrentClient == null) return;
            long uv = Ref.ToLong(Ref.Get(item, "unitValue"), 0);
            long cnt = Ref.ToLong(Ref.Get(item, "unitCount"), 1);
            if (uv <= 0) return;
            if (cnt <= 0) cnt = 1;
            long expected = (Pricing.Accept ? Pricing.PricePerUnit(uv, Pricing.LastRequireVariety) : uv) * cnt;
            if (paid != expected)
                Plugin.Log.LogWarning($"[计价偏差] {tag} {Ref.Get(item, "name", "?")}：实付={paid} 预测={expected}（unit={uv}×{cnt}）");
        }
        catch { }
    }

    internal static void TryRun(string what, Action fn)
    {
        try
        {
            fn();
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"[{what}] 失败：{e}");
        }
    }
}

/// <summary>
/// 售价公式（实测取证，现金差对账验证）：
///   售价 = 单价 × (1 + 零售加价% + 多样化卖家加成%)，整数定点 + 银行家舍入（half-to-even）。
/// 验证点：普客 15→17、25→29；要求不重复顾客 15→20（19.5 进位到偶）、25→32（32.5 舍到偶）。
/// 数值来源：零售加价 PlayerStore.retailMarkup；多样化加成
/// ItemFeatureList.VarietyBuyerBonus().GetCurrentValueMod()（仅 isRequireVariety 顾客叠加）。
/// </summary>
public static class Pricing
{
    public static double RawMarkup = double.NaN;      // retailMarkup 原始值
    public static double RetailPercent = double.NaN;  // 零售加价 %
    public static double VarietyPercent = double.NaN; // 多样化卖家加成 %
    public static bool Accept = true;                 // 当前顾客是否接受零售加价
    public static bool LastRequireVariety;            // 上次计价顾客是否要求不重复（对账用）

    /// <summary>成交数据反推的多样化加成兜底值（游戏读取失败时使用，HUD 会标注「推导」）。</summary>
    public const double DerivedVarietyPercent = 15.0;

    public static object GetPlayerStore()
    {
        try
        {
            var t = typeof(PlayerStore);
            var p = t.GetProperty("instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (p != null) return p.GetValue(null);
            var f = t.GetField("instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            return f?.GetValue(null);
        }
        catch { return null; }
    }

    public static void Refresh()
    {
        RawMarkup = double.NaN;
        RetailPercent = double.NaN;
        VarietyPercent = double.NaN;

        try
        {
            var store = GetPlayerStore();
            if (store == null) Plugin.Log.LogWarning("拿不到 PlayerStore.instance，无法读取零售加价");
            else
            {
                var v = Ref.Get(store, "retailMarkup");
                if (v != null)
                {
                    RawMarkup = Convert.ToDouble(v.ToString());
                    RetailPercent = (Interpret(RawMarkup) - 1.0) * 100.0;
                }
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"读取 retailMarkup 失败：{e.Message}");
        }

        try
        {
            var feat = ItemFeatureList.VarietyBuyerBonus();
            if (feat != null)
            {
                // 实测：GetCurrentValueMod()=15 是真实加成（GetActualValueModifier 对未挂载的工厂对象返回 0）
                int cur = -9999;
                try { cur = feat.GetCurrentValueMod(); } catch { }
                if (cur == -9999 || cur == 0)
                {
                    try { cur = feat.GetActualValueModifier(); } catch { }
                }
                if (cur == -9999 || cur == 0)
                    cur = (int)Ref.ToLong(Ref.Get(feat, "valueModifier"), 0);
                VarietyPercent = cur;
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"读取 VarietyBuyerBonus 失败：{e.Message}");
        }
    }

    /// <summary>多样化加成取值：游戏读取 → 成交反推兜底（HUD 标注来源）。</summary>
    public static double EffectiveVarietyPercent()
    {
        if (!double.IsNaN(VarietyPercent) && VarietyPercent != 0) return VarietyPercent;
        return DerivedVarietyPercent;
    }

    /// <summary>总加成（百分比×10 的整数，如 15%+15% → 300）。</summary>
    static long TotalPercent10(bool requireVariety)
    {
        long p10 = 0;
        if (!double.IsNaN(RetailPercent)) p10 += (long)Math.Round(RetailPercent * 10);
        if (requireVariety) p10 += (long)Math.Round(EffectiveVarietyPercent() * 10);
        return p10;
    }

    /// <summary>
    /// 单件售价 = 单价 × (1000 + 加成×10) / 1000，精确整数运算 + 银行家舍入（half-to-even）。
    /// 游戏即为此算法：15×(1+30%)=19.5→20、25×1.3=32.5→32、15×1.15=17.25→17、25×1.15=28.75→29 全吻合。
    /// （勿改回 double 乘法：浮点会把 19.5 变成 19.4999...96 从而舍成 19。）
    /// </summary>
    public static long PricePerUnit(long unit, bool requireVariety)
    {
        long num = unit * (1000 + TotalPercent10(requireVariety));
        return BankersDiv(num, 1000);
    }

    /// <summary>精确的银行家舍入除法（.5 时向偶数取整）。</summary>
    static long BankersDiv(long num, long den)
    {
        long q = num / den;
        long r = num % den;
        long twice = r * 2;
        if (twice > den) return q + 1;
        if (twice < den) return q;
        return (q % 2 == 0) ? q : q + 1;
    }

    /// <summary>总乘数（仅展示/日志用）。实际计价走整数运算 PricePerUnit（避免浮点误差）。</summary>
    public static double Factor(bool requireVariety)
    {
        return 1.0 + TotalPercent10(requireVariety) / 1000.0;
    }

    /// <summary>加价的存储形式未知（0.15 / 1.15 / 15），三种都换算成乘数。</summary>
    static double Interpret(double raw)
    {
        if (raw <= 0) return 1.0;
        if (raw < 1.0) return 1.0 + raw;    // 0.15 → 1.15
        if (raw < 2.0) return raw;          // 1.15 → 1.15
        return 1.0 + raw / 100.0;           // 15 → 1.15
    }

    public static string RawText => double.IsNaN(RawMarkup) ? "?" : RawMarkup.ToString("0.###");

    public static string VarietyText
    {
        get
        {
            double p = EffectiveVarietyPercent();
            string tag = (!double.IsNaN(VarietyPercent) && VarietyPercent != 0) ? "(游戏)" : "(推导)";
            return p.ToString("0.###") + tag;
        }
    }
}

/// <summary>读取 StoreClient 的需求与预算。</summary>
public static class ClientInfo
{
    public static void Dump(StoreClient c)
    {
        Plugin.Log.LogInfo($"===== 顾客上门：{Ref.Get(c, "displayName", "?")} =====");
        Plugin.Log.LogInfo($"  预算：{Safe(() => Convert.ToInt64(c.GetBudget()))}  类别：{TagsSummary(c)}  " +
                           $"要求不重复：{Safe(() => Ref.Get(c, "isRequireVariety", "?"))}  " +
                           $"接受加价：{Safe(() => Ref.Get(c, "acceptRetailMarkup", "?"))}");
    }

    static object Safe(Func<object> f) { try { return f(); } catch (Exception e) { return $"<{e.Message}>"; } }

    public static string TagsSummary(StoreClient c)
    {
        var parts = new List<string>();
        foreach (var x in Ref.Items(Ref.Get(c, "clientBuyingTagList"))) parts.Add(x?.ToString());
        foreach (var x in Ref.Items(Ref.Get(c, "clientBuyingIdList"))) parts.Add(x?.ToString());
        var s = string.Join(",", parts);
        return s.Length == 0 ? "无限制" : s;
    }
}

/// <summary>反射小工具。</summary>
public static class Ref
{
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
    private static int _skipped; // 价格异常被跳过的候选数

    class Cand
    {
        public GameItem Item;
        public string Name;
        public string GroupKey; // 同类型标识（identifier；空则用名字）
        public long Count;
        public long Price;
        public string Detail;
    }

    public static void Run()
    {
        var c = Patches.CurrentClient;
        if (c == null)
        {
            Result.Set("BestDeal", new[] { "暂无顾客（等顾客上门后自动提示）" });
            Plugin.Log.LogInfo("当前没有顾客");
            return;
        }

        var name = Ref.Get(c, "displayName", "?")?.ToString();
        bool requireVariety = Ref.GetBool(c, "isRequireVariety");
        var cands = Collect(c, requireVariety);
        if (cands.Count == 0)
        {
            Result.Set($"BestDeal — {name}", new[]
            {
                $"顾客：{name}  预算：{SafeGetBudget(c)}  类别：{ClientInfo.TagsSummary(c)}",
                "他不会买你店里的任何东西",
                "[F8重算 F9显隐]",
            });
            return;
        }

        long budget = Ref.ToLong(SafeGetBudget(c), 0);
        if (budget <= 0) budget = long.MaxValue / 4;

        // 分组：要求不重复 → 同类最多 1 件；否则每件独立
        List<List<Cand>> groups = requireVariety
            ? cands.GroupBy(x => x.GroupKey).Select(g => g.ToList()).ToList()
            : cands.Select(x => new List<Cand> { x }).ToList();

        // 0/1 分组背包：容量=预算，每组最多取 1 件，最大化售出总额
        long scale = Math.Max(1, budget / 2_000_000 + 1);
        int cap = (int)(budget / scale);
        var dp = new long[cap + 1];
        var choice = new int[groups.Count][];

        for (int g = 0; g < groups.Count; g++)
        {
            var items = groups[g];
            var w = new int[items.Count];
            for (int k = 0; k < items.Count; k++) w[k] = (int)Math.Min(cap, items[k].Price / scale);

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
                pos -= (int)Math.Min(cap, it.Price / scale);
            }
        }

        long total = 0;
        foreach (var p in picked) total += p.Price;

        var lines = new List<string>
        {
            $"顾客：{name}  预算：{budget}  类别：{ClientInfo.TagsSummary(c)}" +
            (requireVariety ? "  [要求不重复]" : ""),
            $"可售出总额：{total}（剩预算 {Math.Max(0, budget - total)}），候选 {cands.Count} 件" +
            (_skipped > 0 ? $"（另 {_skipped} 件价格异常已跳过）" : ""),
            $"计价：×{Pricing.Factor(requireVariety):0.####}（零售 {Pricing.RawText}% + 多样 {(requireVariety ? Pricing.VarietyText : "0")}%，接受：{Pricing.Accept}）",
        };
        foreach (var p in picked)
            lines.Add($"→ 拿上柜台：{p.Name} ×{p.Count} = {p.Price}");
        lines.Add($"共 {picked.Count} 件  [F8重算 F9显隐]");

        Plugin.Log.LogInfo($"最佳组合（预算 {budget}，可售出 {total}，不重复={requireVariety}）");
        foreach (var cd in cands)
            Plugin.Log.LogInfo($"  候选 {cd.Name} ×{cd.Count} = {cd.Price}（{cd.Detail}）");
        foreach (var l in lines) Plugin.Log.LogInfo("  " + l);
        Result.Set($"BestDeal — {name}", lines);
    }

    static object SafeGetBudget(StoreClient c)
    {
        try { return c.GetBudget(); } catch { return Ref.Get(c, "clientBudget"); }
    }

    /// <summary>扫描各售卖/仓储区收集物品；排除已售/垃圾桶/中转等非售卖区。</summary>
    static List<Cand> Collect(StoreClient c, bool requireVariety)
    {
        var result = new List<Cand>();
        var seenPtr = new HashSet<long>();
        _skipped = 0;
        Pricing.Refresh();
        Pricing.Accept = Ref.GetBool(c, "acceptRetailMarkup", true);
        Pricing.LastRequireVariety = requireVariety;

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
                long val = Ref.ToLong(Ref.Get(gi, "unitValue"), 0);
                if (cnt <= 0) cnt = 1;

                long perUnit = Pricing.Accept ? Pricing.PricePerUnit(val, requireVariety) : val;
                if (perUnit <= 0)
                {
                    _skipped++;
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
                    Price = perUnit * cnt,
                    Detail = $"unit={val}×{cnt} per={perUnit} @{invName} key={gk}",
                });
            }
        }
        Plugin.Log.LogInfo($"候选物品 {result.Count} 件（零售 {Pricing.RawText}% + 多样 {Pricing.VarietyText}%" +
                           $"{(requireVariety ? "（要求不重复）" : "")} → ×{Pricing.Factor(requireVariety):0.####}）");
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
            Plugin.Log.LogWarning("EmporiumEntry.Instance 为空（要在店内场景按 F8）");
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
