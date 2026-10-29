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

    Vector2 _taskScroll, _inspectionScroll;

    private void OnGUI()
    {
        if (EmporiumEntry.Instance == null) return;
        bool inspecting = Appraisal.Title != null && Appraisal.Lines.Count > 0;
        var client = ClientSession.CurrentClient;
        var lines = new System.Collections.Generic.List<string>();
        string title = null;
        if (client != null && !client.isBusinessComplete)
        {
            title = L10n.Format("TradeTitle", string.IsNullOrEmpty(client.displayName) ? "?" : client.displayName);
            if (HudContent.QuoteSummary.Count > 0)
            {
                lines.AddRange(HudContent.QuoteSummary);
                if (!inspecting)
                {
                    lines.Add(L10n.Text("SellSection"));
                    lines.AddRange(HudContent.QuoteItems);
                }
            }
            if (SellerMarketWatch.Lines.Count > 0)
            {
                lines.Add(L10n.Text("BuySection"));
                if (!inspecting) lines.AddRange(SellerMarketWatch.Lines);
            }
        }
        else if (FeatureSettings.MorningMarket && MarketWatch.WantsPanel())
        {
            title = L10n.Text("MarketTitle");
            if (!inspecting) lines.AddRange(HudContent.Preparation);
        }

        float width = Mathf.Min(520f, Screen.width - 32f);
        float available = Mathf.Max(80, Screen.height - 32f);
        if (inspecting)
        {
            // 检查是当前操作；先展示物品，任务面板只保留摘要。
            float inspectionHeight = DrawPanel(Appraisal.Title, Appraisal.Lines, 16, width,
                available * (lines.Count > 0 ? 0.7f : 1f), ref _inspectionScroll);
            if (title != null && lines.Count > 0)
                DrawPanel(title, lines, inspectionHeight + 24, width,
                    available - inspectionHeight - 8, ref _taskScroll);
        }
        else if (title != null && lines.Count > 0)
            DrawPanel(title, lines, 16, width, available, ref _taskScroll);
    }

    static float DrawPanel(string title, System.Collections.Generic.List<string> lines,
        float y, float width, float maxHeight, ref Vector2 scroll)
    {
        var style = new GUIStyle(GUI.skin.label) { wordWrap = true };
        float contentWidth = width - 44;
        float contentHeight = 0;
        foreach (var line in lines)
            contentHeight += Mathf.Max(22, style.CalcHeight(new GUIContent(line), contentWidth));
        float height = Mathf.Min(contentHeight + 42, Mathf.Max(64, maxHeight));
        GUI.Box(new Rect(16, y, width, height), title);
        scroll = GUI.BeginScrollView(new Rect(24, y + 30, width - 16, height - 36), scroll,
            new Rect(0, 0, contentWidth, contentHeight));
        float lineY = 0;
        foreach (var line in lines)
        {
            float lineHeight = Mathf.Max(22, style.CalcHeight(new GUIContent(line), contentWidth));
            GUI.Label(new Rect(6, lineY, contentWidth, lineHeight), line, style);
            lineY += lineHeight;
        }
        GUI.EndScrollView();
        return height;
    }
}
