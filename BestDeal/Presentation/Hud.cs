using UnityEngine;

namespace BestDeal;

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
        SellerMarketWatch.Tick();
        Appraisal.Tick();     // 检查台实际状态及估价提示
    }

    private void OnGUI()
    {
        bool showAppraisal = Appraisal.Title != null && Appraisal.Lines.Count > 0;
        bool showSeller = EmporiumEntry.Instance != null && SellerMarketWatch.Lines.Count > 0;
        bool showMain = EmporiumEntry.Instance != null && HudContent.Lines.Count > 0;
        // 主菜单不画（占位提示只在店内显示）；鉴定提示独立成框
        if (!showMain && !showAppraisal && !showSeller) return;
        float width = Mathf.Min(560f, Screen.width - 32f);
        float y = 16;
        if (showMain)
            y += DrawWrappedPanel(HudContent.Title, HudContent.Lines, y, width) + 8;
        if (showSeller)
            y += DrawWrappedPanel(L10n.Text("SellerTitle"), SellerMarketWatch.Lines, y, width) + 8;
        if (showAppraisal)
            DrawWrappedPanel(Appraisal.Title, Appraisal.Lines, y, width);
    }

    static float DrawWrappedPanel(string title, System.Collections.Generic.List<string> lines, float y, float width)
    {
        var style = new GUIStyle(GUI.skin.label) { wordWrap = true };
        float height = 42;
        foreach (var line in lines)
            height += Mathf.Max(22, style.CalcHeight(new GUIContent(line), width - 28));
        var rect = new Rect(16, y, width, height);
        GUI.Box(rect, title);
        float lineY = y + 30;
        foreach (var line in lines)
        {
            float lineHeight = Mathf.Max(22, style.CalcHeight(new GUIContent(line), width - 28));
            GUI.Label(new Rect(30, lineY, rect.width - 28, lineHeight), line, style);
            lineY += lineHeight;
        }
        return height;
    }
}
