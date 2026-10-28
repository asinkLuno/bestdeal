using System.Collections.Generic;

namespace BestDeal;

/// <summary>
/// 真伪判断（只读）：读物品的 ItemFeature（游戏自己的赝品/瑕疵系统，宣称 vs 实际），
/// 找出"宣称与实际不符"的赝品点。
/// 理由文本不在 mod 里硬编码：直接调游戏自己的 CreateCustomerConfrontDialog()——
/// 顾客识破赝品时说的那句话，内部按类别映射到 LocHelper 的 dialog_expose_* 本地化表，
/// 所以显示的语言自动跟随游戏语言。
/// </summary>
public static class Authenticity
{
    // 与游戏 InsCigaretteHelper / InsStampHelper 的质疑判定及本地化映射一致。
    static readonly (string Tag, string Key, string Fallback)[] InspectionIssues =
    {
        ("CIGARETTE_LETTERING_ISSUE", "ins_cig_issue_lettering", "香烟字样有问题"),
        ("CIGARETTE_COLOR_ISSUE", "ins_cig_issue_color", "香烟颜色有问题"),
        ("CIGARETTE_SEAL_ISSUE", "ins_cig_issue_seal", "香烟封条有问题"),
        ("CIGARETTE_LOGO_ISSUE", "ins_cig_issue_logo", "香烟标志有问题"),
        ("CIGARETTE_BOX_ISSUE", "ins_cig_issue_box", "烟盒有问题"),
        ("STAMP_LETTERING_ISSUE", "ins_cig_issue_lettering", "票券字样有问题"),
        ("STAMP_LOGO_ISSUE", "ins_cig_issue_logo", "票券标志有问题"),
        ("STAMP_SEAL_ISSUE", "ins_cig_issue_seal", "票券封条有问题"),
        ("STAMP_AMOUNT_ISSUE", "ins_stamp_issue_amount", "票券面额有问题"),
        ("STAMP_CHIP_ISSUE", "ins_stamp_issue_chip", "票券芯片有问题"),
    };

    /// <summary>检查台实际状态；匹配仅表示标称属实，不能据此推断是真品。</summary>
    public static List<string> InspectionLines(GameItem item)
    {
        var lines = new List<string>();
        if (item == null) return lines;
        bool counterfeit = false;
        foreach (var f in item.itemFeatures ?? new List<ItemFeature>())
        {
            if (f == null || f.isDisabled || !f.useCondition || f.realCondition == null) continue;
            try
            {
                var actual = f.GetBaseRealDisplay();
                counterfeit |= f.realCondition.identifier == "counterfeitCigarette" ||
                               f.realCondition.identifier == "counterfeitStamp";
                if (string.IsNullOrEmpty(actual)) continue;
                var line = $"实际：{actual}";
                if (f.fakeCondition != null)
                    line += f.IsFeatureMatch()
                        ? "（标称一致）"
                        : $"　标称：{f.GetBaseFakeDisplay()}（不符）";
                if (!lines.Contains(line)) lines.Add(line);
            }
            catch { } // 单个特征不可用不影响其余鉴定结果
        }
        int reasonCount = 0;
        foreach (var issue in InspectionIssues)
        {
            if (!item.IsTag(issue.Tag)) continue;
            string reason = null;
            try { reason = LocHelper.GetLocalizedMechanic(issue.Key); } catch { }
            if (string.IsNullOrEmpty(reason) || reason == issue.Key) reason = issue.Fallback;
            lines.Add($"疑点：{reason}");
            reasonCount++;
        }
        if (counterfeit && reasonCount == 0)
            lines.Add("赝品（外观疑点未知）");
        if (lines.Count == 0) lines.Add("真伪未知");
        return lines;
    }

    /// <summary>
    /// 这件物品的赝品警告行；没有赝品点返回空列表。
    /// 已被识破（IsExposed）的会加"已被识破"前缀，方便区分顾客知不知道。
    /// </summary>
    public static List<string> FakeWarnings(GameItem item)
    {
        var lines = new List<string>();
        if (item?.itemFeatures == null) return lines;
        foreach (var f in item.itemFeatures)
        {
            if (f == null || !f.useCondition || f.fakeCondition == null) continue;
            if (f.IsFeatureMatch()) continue;              // 宣称与实际相符：货真，不算赝品
            string reason = null;
            try { reason = f.CreateCustomerConfrontDialog()?.plainText; } catch { }
            if (string.IsNullOrEmpty(reason)) continue;
            var line = f.IsExposed() ? $"⚠ 已识破：{reason}" : $"⚠ {reason}";
            if (!lines.Contains(line)) lines.Add(line);
        }
        return lines;
    }
}
