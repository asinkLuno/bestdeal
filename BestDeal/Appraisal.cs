using System;
using System.Collections.Generic;

namespace BestDeal;

/// <summary>
/// 鉴定估价提示（只读）：鉴定顾客上门时，把物品拿到检查台会弹出估价输入面板
/// （InspectionUIManager.appraisalPanel——游戏自己的显示条件就是"检查中的物品带 APPRAISAL_ITEM
/// 标签且当前顾客 intent=APPRAISAL_SERVICE"，所以面板可见 = 玩家正在估价，不用自己再判一遍）。
/// 真值读游戏自己的 AppraisalHelper.GetCorrectBaseValue；输入框当前值游戏会实时写进物品标签
/// APPRAISAL_DEFAULT_VALUE（OnValueChanged→ModifyTag），不用碰输入框控件（省一个 TMPro 引用）。
/// 评级公式逆向自游戏 GetEstimatePrecision（阈值同为 整值×百分比/100 截断）：
/// 出价/真值 95%~105% 完美、90%~110% 极好、之外更差；50%~150% 之外直接被拒且耐心 +49，两次就翻脸走人。
/// 评级名不硬编码：复用游戏自己的 appraisal_quality_* 本地化表，跟随游戏语言。
/// </summary>
public static class Appraisal
{
    internal static string Title;                        // 空 = 当前没有鉴定可提示
    internal static readonly List<string> Lines = new();

    const string TagItem = "APPRAISAL_ITEM";
    const string TagInput = "APPRAISAL_DEFAULT_VALUE";

    /// <summary>每帧刷新（Hud.Update 驱动）。只在估价面板可见时给出提示。</summary>
    internal static void Tick()
    {
        Title = null;
        Lines.Clear();
        try
        {
            var insp = InspectionUIManager.Instance;
            if (insp == null) return;
            var panel = insp.appraisalPanel;
            if (panel == null || !panel.activeInHierarchy) return;
            var item = insp.currentInspectingItem;
            if (item == null || !item.IsTag(TagItem)) return;

            int correct = 0;
            try { correct = AppraisalHelper.GetCorrectBaseValue(item); } catch { }
            if (correct <= 0) return;

            string name;
            try { name = string.IsNullOrEmpty(item.name) ? "?" : item.name; } catch { name = "?"; }
            Title = $"鉴定 — {name}";
            Lines.Add($"真实价值：{correct}　照抄 = 完美");

            int input = 0;
            try { input = item.GetTagReadonly(TagInput)?.GetInt() ?? 0; } catch { }
            if (input <= 0) return;                       // 没填过价（标签缺失/还没解析出数字）
            if (input == correct)
                Lines.Add("当前输入：已照抄 ✓");
            else
                Lines.Add($"当前输入：{input} → {QualityName(GradeOf(correct, input))}" +
                          $"（{(input < correct ? "偏低" : "偏高")}，改成 {correct} 拿完美）");
        }
        catch { }   // 检查台字段在场景切换瞬间可能失效，提示失败就当没有
    }

    /// <summary>游戏自己的评级（GetEstimatePrecision 的逆向，返回值 1最好 → 5最差）。</summary>
    static int GradeOf(int correct, int value)
    {
        int Pct(int p) => correct * p / 100;
        if (value >= Pct(95) && value <= Pct(105)) return 1;
        if (value < Pct(95)) return value <= Pct(75) ? 5 : value <= Pct(85) ? 4 : value <= Pct(90) ? 3 : 2;
        return value >= Pct(125) ? 5 : value >= Pct(115) ? 4 : value >= Pct(110) ? 3 : 2;
    }

    /// <summary>评级名：游戏自己的 appraisal_quality_* 本地化，查不到退回中文。</summary>
    static string QualityName(int grade)
    {
        var key = "appraisal_quality_" + grade switch
        {
            1 => "perfect",
            2 => "excellent",
            3 => "good",
            4 => "mediocre",
            _ => "awful",
        };
        try
        {
            var s = LocHelper.GetLocalizedMechanic(key);
            if (!string.IsNullOrEmpty(s) && s != key) return s;
        }
        catch { }
        return grade switch { 1 => "完美", 2 => "极好", 3 => "不错", 4 => "一般", _ => "离谱" };
    }
}
