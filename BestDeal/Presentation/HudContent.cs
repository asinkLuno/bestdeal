using System.Collections.Generic;

namespace BestDeal;

/// <summary>悬浮窗状态（Hud 每帧读取；由编排层/功能层写入）。</summary>
internal static class HudContent
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

