using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BestDeal;

/// <summary>原生 UI 的展示与生命周期；数据轮询不受面板开关影响。</summary>
public class Hud : MonoBehaviour
{
    internal static Hud Instance;
    internal static string ModId;
    CustomUIWindow _panel, _launcher;
    CustomUIElement _inspectionTitle, _inspectionBody, _taskTitle, _taskBody;
    HudPreferences _preferences;
    float _retryAt;
    float _refreshAt;
    string _locale;
    string _windowTitle;
    RectTransform _scrollContent;
    bool _sizeDirty = true;
    Vector2 _lastBounds;

    void Awake()
    {
        Instance = this;
        _preferences = HudPreferences.Load(ModId);
    }

    void Update()
    {
        ClientSession.Tick();
        SellerMarketWatch.Tick();
        Appraisal.Tick();

        if (EmporiumEntry.Instance == null)
        {
            DisposeWindows();
            return;
        }
        if (CustomUIManager.Instance == null || Time.unscaledTime < _retryAt) return;
        try
        {
            if (_panel == null || !_panel.IsAlive || _launcher == null || !_launcher.IsAlive
                || _locale != L10n.LocaleCode)
            {
                DisposeWindows();
                CreateWindows();
            }
            FitPanel();
            SetVisible(_panel, !_preferences.collapsed);
            SetVisible(_launcher, _preferences.collapsed);
            if (!_preferences.collapsed && Time.unscaledTime >= _refreshAt)
            {
                RefreshContent();
                _refreshAt = Time.unscaledTime + 0.1f;
            }
        }
        catch (System.Exception e)
        {
            DisposeWindows();
            _retryAt = Time.unscaledTime + 5f;
            BestDealMod.Log.Warning($"HUD 更新失败：{e.Message}");
        }
    }

    void CreateWindows()
    {
        _locale = L10n.LocaleCode;
        var builder = CustomUIManager.Instance.CreateWindow(ModId + ".hud", "BestDeal",
            CustomUIManager.LAYER_OVERLAY, style: CustomUIManager.Style.Machinery)
            .SetSize(520, 600).SetDraggable(false).SetCloseOnEscape(false);
        _panel = builder.Handle;
        _windowTitle = null;
        TopLeft(_panel.Rect);
        builder.BeginScrollFlexible();
        _scrollContent = builder.CurrentContainer;
        builder.AddHeader("", "inspection_title");
        _inspectionTitle = builder.Last;
        builder.AddLabel("", "inspection_body");
        _inspectionBody = builder.Last;
        builder.AddHeader("", "task_title");
        _taskTitle = builder.Last;
        builder.AddLabel("", "task_body");
        _taskBody = builder.Last;
        builder.End();
        // 原生 Close 会销毁窗口；这里改为隐藏，保留滚动位置和入口。
        _panel.frame.closeButton.onClick.RemoveAllListeners();
        _panel.frame.closeButton.onClick.AddListener(Collapse);
        // 只让标题栏拖动，正文保留原生滚动操作。
        var drag = _panel.frame.titleText.transform.parent.gameObject.AddComponent<HudDragHandle>();
        drag.Owner = this;
        _panel.SetPosition(new Vector2(_preferences.x, _preferences.y));
        builder.GetWindow();

        // 无边框的窗口宿主，只放一个游戏原生按钮。
        var template = new GameObject("BestDealLauncherTemplate", typeof(RectTransform));
        template.SetActive(false);
        try
        {
            var layout = template.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            template.AddComponent<CustomUIWindowFrame>().contentRoot = (RectTransform)template.transform;
            var entry = CustomUIManager.Instance.CreateWindow(ModId + ".launcher", null,
                CustomUIManager.LAYER_OVERLAY, template).UseMachineryStyle()
                .SetSize(132, 36).SetCloseOnEscape(false);
            entry.AddButton("BestDeal", Expand, "open");
            _launcher = entry.GetWindow();
            TopLeft(_launcher.Rect);
            _launcher.SetPosition(new Vector2(16, -16));
        }
        finally { Destroy(template); }
        RefreshContent();
    }

    static void TopLeft(RectTransform rect)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
    }

    void FitPanel()
    {
        var bounds = ((RectTransform)_panel.Rect.parent).rect.size;
        if (_sizeDirty || bounds != _lastBounds)
        {
            SizeToContent(bounds);
            _lastBounds = bounds;
            _sizeDirty = false;
        }
        ClampPosition(bounds);
    }

    void SizeToContent(Vector2 bounds)
    {
        var elements = new[] { _inspectionTitle, _inspectionBody, _taskTitle, _taskBody };
        var layout = _scrollContent.GetComponent<VerticalLayoutGroup>();
        float horizontalPadding = layout != null ? layout.padding.horizontal : 0;
        float height = layout != null ? layout.padding.vertical : 0;
        float spacing = layout != null ? layout.spacing : 0;
        // 原生窗口边框和滚动条预留；不要用上一帧的 Rect 高度测量文本。
        var content = _panel.ContentRoot;
        float frameWidth = content.offsetMin.x - content.offsetMax.x;
        float frameHeight = content.offsetMin.y - content.offsetMax.y;
        float widthPadding = frameWidth + horizontalPadding + 32;
        float naturalWidth = 0;
        foreach (var element in elements)
            if (element.root.activeSelf)
                naturalWidth = Mathf.Max(naturalWidth,
                    element.text.GetPreferredValues(element.GetText(), Mathf.Infinity, Mathf.Infinity).x);
        float maxWidth = Mathf.Min(680, Mathf.Max(160, bounds.x - 32));
        float titleWidth = _panel.frame.titleText.GetPreferredValues(
            _windowTitle ?? "", Mathf.Infinity, Mathf.Infinity).x + 88;
        float width = Mathf.Clamp(Mathf.Ceil(Mathf.Max(naturalWidth + widthPadding, titleWidth)),
            Mathf.Min(300, maxWidth), maxWidth);
        float textWidth = Mathf.Max(1, width - widthPadding);
        int count = 0;
        foreach (var element in elements)
        {
            if (!element.root.activeSelf) continue;
            height += Mathf.Max(LayoutUtility.GetMinHeight(element.Rect),
                element.text.GetPreferredValues(element.GetText(), textWidth, Mathf.Infinity).y);
            count++;
        }
        height += Mathf.Max(0, count - 1) * spacing;
        float maxHeight = Mathf.Max(120, bounds.y - 96);
        _panel.SetSize(width, Mathf.Min(maxHeight, Mathf.Max(120, Mathf.Ceil(height + frameHeight + 8))));
        CustomUIBuilder.RebuildLayout(_panel.Rect);
    }

    void ClampPosition(Vector2 bounds)
    {
        var size = _panel.Rect.rect.size;
        var position = _panel.Rect.anchoredPosition;
        _panel.SetPosition(new Vector2(
            Mathf.Clamp(position.x, 0, Mathf.Max(0, bounds.x - size.x)),
            Mathf.Clamp(position.y, -Mathf.Max(0, bounds.y - size.y), 0)));
    }

    internal void Drag(Vector2 delta)
    {
        if (_panel == null || !_panel.IsAlive) return;
        _panel.SetPosition(_panel.Rect.anchoredPosition + delta);
        FitPanel();
    }

    internal void RememberPosition()
    {
        if (_panel == null || !_panel.IsAlive) return;
        var position = _panel.Rect.anchoredPosition;
        _preferences.x = position.x;
        _preferences.y = position.y;
        _preferences.Save(ModId);
    }

    void Collapse()
    {
        _preferences.collapsed = true;
        RememberPosition();
        _panel.Hide();
        _launcher.Show();
    }

    void Expand()
    {
        _preferences.collapsed = false;
        _preferences.Save(ModId);
        RefreshContent();
        _launcher.Hide();
        _panel.Show();
    }

    void RefreshContent()
    {
        bool inspecting = Appraisal.Title != null && Appraisal.Lines.Count > 0;
        SetSection(_inspectionTitle, _inspectionBody,
            inspecting ? Appraisal.Title : null, inspecting ? string.Join("\n", Appraisal.Lines) : "", showHeader: false);
        var lines = new List<string>();
        string title = null;
        var client = ClientSession.CurrentClient;
        if (client != null && !client.isBusinessComplete)
        {
            bool buying = client.IsClientBuying(), selling = client.IsClientSelling();
            string titleKey = buying && selling ? "CustomerTradingTitle"
                : buying ? "CustomerBuyingTitle" : selling ? "CustomerSellingTitle" : "TradeTitle";
            title = L10n.Text(titleKey);
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
                lines.AddRange(SellerMarketWatch.Lines);
            }
        }
        else if (FeatureSettings.MorningMarket && MarketWatch.WantsPanel())
        {
            title = L10n.Text("MarketTitle");
            if (!inspecting) lines.AddRange(HudContent.Preparation);
        }
        if (!inspecting && lines.Count == 0)
        {
            title = title ?? L10n.Text("IdleTitle");
            lines.Add(L10n.Text(client != null && !client.isBusinessComplete ? "NoHints" : "NoCustomer"));
        }
        string windowTitle = inspecting ? Appraisal.Title : title ?? L10n.Text("IdleTitle");
        if (_windowTitle != windowTitle)
        {
            _panel.SetTitle(windowTitle);
            _windowTitle = windowTitle;
            _sizeDirty = true;
        }
        SetSection(_taskTitle, _taskBody, lines.Count > 0 ? title : null,
            string.Join("\n", lines), showHeader: inspecting);
        FitPanel();
    }

    void SetSection(CustomUIElement header, CustomUIElement body, string title, string content, bool showHeader = true)
    {
        bool headerVisible = title != null && showHeader;
        if (header.root.activeSelf != headerVisible || body.root.activeSelf != (title != null)) _sizeDirty = true;
        header.SetVisible(headerVisible);
        body.SetVisible(title != null);
        if (title == null) return;
        if (header.GetText() != title) { header.SetText(title); _sizeDirty = true; }
        if (body.GetText() != content) { body.SetText(content); _sizeDirty = true; }
    }

    static void SetVisible(CustomUIWindow window, bool visible)
    {
        if (window.IsVisible == visible) return;
        if (visible) window.Show(); else window.Hide();
    }

    void DisposeWindows()
    {
        if (_panel == null && _launcher == null) return;
        RememberPosition();
        _panel?.Close();
        _launcher?.Close();
        _panel = _launcher = null;
    }

    void OnDestroy()
    {
        DisposeWindows();
        if (Instance == this) Instance = null;
    }
}
