using System;
using System.Collections.Generic;
using Il2Cpp;   // LocHelper（游戏自己的本地化助手）在 Il2Cpp 命名空间下

namespace BestDeal;

/// <summary>
/// HUD 与日志文案的多语言支持。
///
/// 语言跟着**游戏自己的设置**走（LocHelper.GetCurrentLocaleCode()，玩家在游戏选项里选的那个），
/// 不猜系统语言：zh 开头用中文，其余一律英文（国际版用户/N 网）。
/// 物品名、类别名不需要在这里翻译——它们直接来自游戏的本地化表，本来就是游戏语言的。
///
/// 加文案：在 Table 里加一行 key → [中文, English]，然后用 Loc.S("key", 参数…) 取。
/// </summary>
public static class Loc
{
    private static bool _zh;

    /// <summary>跟随游戏语言。在加载时和每次重算前调一次即可（语言改了会跟着变）。</summary>
    public static void Refresh()
    {
        try
        {
            var code = LocHelper.GetCurrentLocaleCode();
            _zh = !string.IsNullOrEmpty(code) && code.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            _zh = false;   // 读不到就当英文（国际用户的默认）
        }
    }

    /// <summary>取文案：key 不存在时原样返回 key，方便一眼看出漏翻。</summary>
    public static string S(string key, params object[] args)
    {
        var s = Table.TryGetValue(key, out var row) ? (_zh ? row[0] : row[1]) : key;
        return args == null || args.Length == 0 ? s : string.Format(s, args);
    }

    // key → [中文, English]
    private static readonly Dictionary<string, string[]> Table = new()
    {
        // —— HUD ——
        ["noCustomer"]      = new[] { "暂无顾客（等顾客上门后自动提示）", "No customer yet — list appears when one walks up" },
        ["head"]            = new[] { "顾客：{0}　预算：{1}　类别：{2}", "Customer: {0}  Budget: {1}  Wants: {2}" },
        ["requireVariety"]  = new[] { "　[要求不重复]", "  [no duplicates]" },
        ["unlimited"]       = new[] { "无限制", "anything" },
        ["probingNow"]      = new[] { "正在逐件摆上柜台探测价格…（探完自动更新）", "Reading prices off the counter… (updates by itself)" },
        ["unpricedRetry"]   = new[] { "候选 {0} 件，价格还没探到（自动重探中）", "{0} candidates, prices not read yet (retrying)" },
        ["buysNothing"]     = new[] { "他不会买你店里的任何东西", "They won't buy anything you have" },
        ["tooExpensive"]    = new[] { "候选 {0} 件，但最便宜的也要 {1}，超出预算，他一件也买不起",
                                      "{0} candidates, but the cheapest is {1} — over budget, they can't afford any" },
        ["totalSold"]       = new[] { "可售出总额：{0}", "Total sale: {0}" },
        ["budgetLeft"]      = new[] { "（剩预算 {0}）", " (budget left {0})" },
        ["candidateCount"]  = new[] { "，候选 {0} 件", ", {0} candidates" },
        ["skippedUnpriced"] = new[] { "（另 {0} 件未探到价已跳过）", " ({0} without a price skipped)" },
        ["pickLine"]        = new[] { "→ {0} ×{1} = {2}", "→ {0} x{1} = {2}" },
        ["pickCount"]       = new[] { "共 {0} 件", "{0} items" },
        ["probeStatus"]     = new[] { "价格探测中 {0}/{1}：物品会短暂摆上柜台，读完价自动收回",
                                      "Reading prices {0}/{1}: items briefly go on the counter, then back" },

        // —— 告警与日志 ——
        ["warnNoCounter"]    = new[] { "价格探测：没找到后柜台（backInv），本轮跳过", "Price probe: back counter (backInv) not found, skipping" },
        ["warnExpelFailed"]  = new[] { "价格探测：取出 {0} 失败：{1}", "Price probe: could not take out {0}: {1}" },
        ["warnPlaceFailed"]  = new[] { "价格探测：上柜台失败，已放回：{0}", "Price probe: could not place on counter, put back: {0}" },
        ["warnNotReturned"]  = new[] { "价格探测：{0} 没能放回原处，快检查柜台！", "Price probe: {0} was NOT put back — check your counter!" },
        ["warnReturnError"]  = new[] { "价格探测：收回 {0} 出错：{1}", "Price probe: error putting {0} back: {1}" },
        ["warnNoEmporium"]   = new[] { "EmporiumEntry.Instance 为空（不在店内场景？）", "EmporiumEntry.Instance is null (not in the store scene?)" },
        ["errRunFailed"]     = new[] { "[{0}] 失败：{1}", "[{0}] failed: {1}" },
        ["whatAuto"]         = new[] { "自动计算", "auto compute" },
        ["whatReprobe"]      = new[] { "探测后重算", "recompute after probe" },
        ["loaded"]           = new[] { "BestDeal v{0} 已加载（MelonLoader，已打补丁 {1} 处）",
                                       "BestDeal v{0} loaded (MelonLoader, {1} patches applied)" },
    };
}
