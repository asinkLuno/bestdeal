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
        ["noCustomer"]      = new[] { "等顾客上门再看推荐。", "Waiting for a customer." },
        ["head"]            = new[] { "顾客：{0}　预算：{1}　想买：{2}", "Customer: {0}  Budget: {1}  Wants: {2}" },
        ["requireVariety"]  = new[] { "　（同类只推荐一种）", "  (one option per type)" },
        ["unlimited"]       = new[] { "不限类别", "anything" },
        ["probingNow"]      = new[] { "正在核价，稍等…", "Checking prices…" },
        ["unpricedRetry"]   = new[] { "还有 {0} 件没核到价，暂时没算进去", "{0} items have no price yet and aren't included" },
        ["buysNothing"]     = new[] { "店里没有符合顾客需求的商品", "Nothing in stock matches what they want" },
        ["tooExpensive"]    = new[] { "找到 {0} 件，但最便宜的也要 {1}，超出预算了",
                                      "Found {0} matches, but even the cheapest costs {1} — over budget" },
        ["totalSold"]       = new[] { "推荐合计：{0}", "Recommended total: {0}" },
        ["budgetLeft"]      = new[] { "（还剩 {0}）", " ({0} left)" },
        ["candidateCount"]  = new[] { "，可选 {0} 件", ", {0} options" },
        ["skippedUnpriced"] = new[] { "（另有 {0} 件还没核价）", " ({0} more still need a price)" },
        ["pickLine"]        = new[] { "{0} ×{1} = {2}", "{0} x{1} = {2}" },
        ["pickCount"]       = new[] { "推荐 {0} 项", "{0} picks" },
        ["inspectionItem"] = new[] { "放大镜选中：{0}", "Magnified item: {0}" },
        ["appraisalItem"] = new[] { "鉴定服务商品：{0}", "Appraisal item: {0}" },
        ["appraisalServicePrice"] = new[] { "鉴定估值：{0} / 件", "Appraised value: {0} each" },
        ["inspectionDetails"] = new[] { "检查发现：{0}", "Inspection findings: {0}" },
        ["issueSeparator"] = new[] { "、", ", " },
        ["injectorIssueBody"] = new[] { "瓶体问题", "bottle body issue" },
        ["injectorIssueColor"] = new[] { "颜色问题", "color issue" },
        ["injectorIssueIndicator"] = new[] { "指示标记问题", "indicator issue" },
        ["injectorIssueLabel"] = new[] { "标签问题", "label issue" },
        ["injectorIssueSerial"] = new[] { "序列号问题", "serial number issue" },
        ["cigaretteIssueBox"] = new[] { "包装问题", "box issue" },
        ["cigaretteIssueColor"] = new[] { "颜色问题", "color issue" },
        ["cigaretteIssueLettering"] = new[] { "字样问题", "lettering issue" },
        ["cigaretteIssueLogo"] = new[] { "标志问题", "logo issue" },
        ["cigaretteIssueSeal"] = new[] { "封口问题", "seal issue" },
        ["probeStatus"]     = new[] { "核价中 {0}/{1}（商品会暂时放上柜台，随后放回）",
                                      "Checking prices {0}/{1} (items briefly go on the counter, then back)" },

        // —— 告警与日志 ——
        ["warnNoCounter"]    = new[] { "核价失败：没找到后柜台（backInv），本轮跳过", "Price check: back counter (backInv) not found; skipping this round" },
        ["warnExpelFailed"]  = new[] { "核价失败：没能取出 {0}：{1}", "Price check: couldn't take out {0}: {1}" },
        ["warnPlaceFailed"]  = new[] { "核价失败：没能把商品放上柜台，已放回：{0}", "Price check: couldn't place item on the counter; returned it: {0}" },
        ["warnNotReturned"]  = new[] { "注意：{0} 没能放回原处，请检查柜台！", "Warning: {0} wasn't returned to its original spot. Check the counter!" },
        ["warnReturnError"]  = new[] { "核价失败：放回 {0} 时出错：{1}", "Price check: error returning {0}: {1}" },
        ["warnNoEmporium"]   = new[] { "EmporiumEntry.Instance 为空（不在店内场景？）", "EmporiumEntry.Instance is null (not in the store scene?)" },
        ["errRunFailed"]     = new[] { "[{0}] 失败：{1}", "[{0}] failed: {1}" },
        ["whatAuto"]         = new[] { "到店计算", "arrival calculation" },
        ["whatReprobe"]      = new[] { "核价后重算", "recalculate after price check" },
        ["loaded"]           = new[] { "BestDeal v{0} 已启动（补丁：{1}）",
                                       "BestDeal v{0} started ({1} patches)" },
    };
}
