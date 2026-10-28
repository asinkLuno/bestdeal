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
            var line = f.IsExposed() ? $"⚠ 已被识破：{reason}" : $"⚠ {reason}";
            if (!lines.Contains(line)) lines.Add(line);
        }
        return lines;
    }
}
