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
    static readonly (string Tag, string Key)[] InspectionIssues =
    {
        ("CIGARETTE_LETTERING_ISSUE", "ins_cig_issue_lettering"),
        ("CIGARETTE_COLOR_ISSUE", "ins_cig_issue_color"),
        ("CIGARETTE_SEAL_ISSUE", "ins_cig_issue_seal"),
        ("CIGARETTE_LOGO_ISSUE", "ins_cig_issue_logo"),
        ("CIGARETTE_BOX_ISSUE", "ins_cig_issue_box"),
        ("STAMP_LETTERING_ISSUE", "ins_cig_issue_lettering"),
        ("STAMP_LOGO_ISSUE", "ins_cig_issue_logo"),
        ("STAMP_SEAL_ISSUE", "ins_cig_issue_seal"),
        ("STAMP_AMOUNT_ISSUE", "ins_stamp_issue_amount"),
        ("STAMP_CHIP_ISSUE", "ins_stamp_issue_chip"),
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
                var line = L10n.Format("ActualCondition", actual);
                if (f.fakeCondition != null)
                    line += f.IsFeatureMatch()
                        ? L10n.Text("MatchesClaim")
                        : L10n.Format("ClaimMismatch", f.GetBaseFakeDisplay());
                if (!lines.Contains(line)) lines.Add(line);
            }
            catch { } // 单个特征不可用不影响其余鉴定结果
        }
        int reasonCount = 0;
        foreach (var issue in InspectionIssues)
        {
            if (!item.IsTag(issue.Tag)) continue;
            string reason = null;
            try { reason = LocHelper.GetLocalizedMechanicOrDefault(issue.Key, ""); } catch { }
            if (string.IsNullOrEmpty(reason) || reason == issue.Key) continue;
            lines.Add(L10n.Format("Issue", reason));
            reasonCount++;
        }
        if (counterfeit && reasonCount == 0)
            lines.Add(L10n.Text("CounterfeitUnknown"));
        if (lines.Count == 0) lines.Add(L10n.Text("AuthenticityUnknown"));
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
            var line = f.IsExposed() ? L10n.Format("Exposed", reason) : $"⚠ {reason}";
            if (!lines.Contains(line)) lines.Add(line);
        }
        return lines;
    }
}
