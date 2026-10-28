using System.Collections.Generic;
using UnityEngine;

namespace BestDeal;

/// <summary>悬浮窗状态（Hud 每帧读取；由编排层/功能层写入）。</summary>
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

/// <summary>游戏内悬浮窗（只负责画；逻辑由 ClientSession 驱动）。</summary>
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
        ClientSession.Tick();
        Appraisal.Tick();     // 鉴定估价提示：只在检查台估价面板可见时有内容
    }

    private void OnGUI()
    {
        bool showAppraisal = Appraisal.Title != null && Appraisal.Lines.Count > 0;
        bool showMain = EmporiumEntry.Instance != null && Result.Lines.Count > 0;
        // 主菜单不画（占位提示只在店内显示）；鉴定提示独立成框
        if (!showMain && !showAppraisal) return;
        float width = Mathf.Min(560f, Screen.width - 32f);
        float y = 16;
        if (showMain)
        {
            var rect = new Rect(16, y, width, 64 + Result.Lines.Count * 22);
            GUI.Box(rect, Result.Title);
            for (int i = 0; i < Result.Lines.Count; i++)
                GUI.Label(new Rect(30, y + 30 + i * 22, rect.width - 28, 22), Result.Lines[i]);
            y += rect.height + 8;
        }
        if (showAppraisal)
        {
            var rect = new Rect(16, y, width, 64 + Appraisal.Lines.Count * 22);
            GUI.Box(rect, Appraisal.Title);
            for (int i = 0; i < Appraisal.Lines.Count; i++)
                GUI.Label(new Rect(30, y + 30 + i * 22, rect.width - 28, 22), Appraisal.Lines[i]);
        }
    }
}
