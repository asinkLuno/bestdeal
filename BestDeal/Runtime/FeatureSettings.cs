namespace BestDeal;

/// <summary>原生模组配置：由游戏绘制、持久化，各存档共用。</summary>
internal static class FeatureSettings
{
    static ConfigEntry<bool> _quotes, _authenticity, _appraisal, _morningMarket, _sellerMarket;

    static ConfigEntry<int> _maxStolenHeat;

    internal static bool Quotes => _quotes?.Value ?? true;
    internal static int MaxStolenHeat => _maxStolenHeat?.Value ?? 100;
    internal static bool Authenticity => _authenticity?.Value ?? true;
    internal static bool Appraisal => _appraisal?.Value ?? true;
    internal static bool MorningMarket => _morningMarket?.Value ?? true;
    internal static bool SellerMarket => _sellerMarket?.Value ?? true;

    internal static void Init(string modId)
    {
        HeatSettingDisplay.Init(modId);
        var config = ModHelper.GetConfig(modId);
        _quotes = Define("customer_quotes", "SettingQuotes", "SettingQuotesTip");
        _maxStolenHeat = config.Define("max_stolen_heat", 100, "BestDeal",
            () => L10n.Text("SettingStolenHeat"), min: 0, max: 100,
            tooltip: () => L10n.Text("SettingStolenHeatTip"));
        _maxStolenHeat.OnChanged += _ => ClientSession.Invalidate();
        _authenticity = Define("authenticity_hints", "SettingAuthenticity", "SettingAuthenticityTip");
        _appraisal = Define("appraisal_hints", "SettingAppraisal", "SettingAppraisalTip");
        _morningMarket = Define("morning_market", "SettingMorningMarket", "SettingMorningMarketTip");
        _sellerMarket = Define("seller_market", "SettingSellerMarket", "SettingSellerMarketTip");

        ConfigEntry<bool> Define(string key, string label, string tip)
        {
            var entry = config.Define(key, true, "BestDeal", () => L10n.Text(label),
                tooltip: () => L10n.Text(tip));
            entry.OnChanged += _ => ClientSession.Invalidate();
            return entry;
        }
    }
}
