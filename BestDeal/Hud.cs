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
    }

    private void OnGUI()
    {
        var status = PriceProbe.Status;                       // 探测进度：每帧直接读，不走报价器
        // 主菜单不画（占位提示只在店内显示）；探测进行中则任何场景都画
        if (EmporiumEntry.Instance == null && status == null) return;
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
