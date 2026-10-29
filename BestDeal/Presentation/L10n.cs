using System;
using System.Collections.Generic;

namespace BestDeal;

/// <summary>Mod-owned text; game-owned names and descriptions still use LocHelper.</summary>
internal static class L10n
{
    internal static string LocaleCode
    {
        get { try { return LocHelper.GetCurrentLocaleCode() ?? ""; } catch { return ""; } }
    }

    static readonly Dictionary<string, (string Chinese, string English)> Strings = new()
    {
        ["SettingQuotes"] = ("顾客报价推荐", "Customer quote recommendations"),
        ["SettingQuotesTip"] = ("按顾客需求和预算推荐售卖组合，显示库存位置和柜台就位标记。", "Recommend items within the customer's budget, with inventory locations and counter markers."),
        ["SettingStolenHeat"] = ("推荐赃物最高热度", "Maximum stolen heat for recommendations"),
        ["SettingStolenHeatTip"] = ("仅推荐热度不超过此值的商品，包括已在柜台上的商品。0：仅无热度；100：允许最高 100 热度。默认 100。", "Only recommend items with heat at or below this value, including counter items. 0: no heat; 100: allow heat up to 100. Default: 100."),
        ["SettingAuthenticity"] = ("真伪提醒", "Authenticity hints"),
        ["SettingAuthenticityTip"] = ("显示放大镜检查中的实际状态、标称差异与赝品依据，以及推荐清单中的赝品警告。", "Show actual condition, claim mismatches and counterfeit clues during inspection, plus counterfeit warnings in recommendations."),
        ["SettingAppraisal"] = ("鉴定估价提示", "Appraisal hints"),
        ["SettingAppraisalTip"] = ("鉴定估价时显示真实价值及当前输入的评级。", "Show actual value and the grade of your current estimate during appraisal."),
        ["SettingMorningMarket"] = ("开店前行情", "Morning market hints"),
        ["SettingMorningMarketTip"] = ("开店前显示新闻涨跌、橱窗库存建议和收购牌提示。", "Show market news, display stock suggestions and buying signs before opening."),
        ["SettingSellerMarket"] = ("卖家新闻提醒", "Seller market news"),
        ["SettingSellerMarketTip"] = ("提示卖家待售物品涉及的新闻涨跌。", "Show market news affecting items offered by sellers."),
        ["NoCustomer"] = ("暂无顾客", "No customer"),
        ["NoItems"] = ("无合适商品", "No suitable items"),
        ["Any"] = ("无限制", "Any"),
        ["WinningTicket"] = ("中奖彩票", "Winning lottery ticket"),
        ["NoDuplicates"] = ("  [不重复]", "  [No duplicates]"),
        ["ListSeparator"] = ("、", ", "),
        ["MarketTitle"] = ("BestDeal — 今日行情", "BestDeal — Today's market"),
        ["NoChanges"] = ("今日无涨跌", "No price changes today"),
        ["Display"] = ("   → 摆橱窗：", "   → Display: "),
        ["OutOfStock"] = ("   → 无货", "   → Out of stock"),
        ["NoSign"] = ("   →无对应收购牌", "   → No matching buying sign"),
        ["Affected"] = ("涉及：", "Affected: "),
        ["SellerTitle"] = ("BestDeal — 顾客售卖 · 新闻提醒", "BestDeal — Seller · Market news"),
        ["TooLow"] = ("偏低", "Too low"),
        ["TooHigh"] = ("偏高", "Too high"),
        ["MatchesClaim"] = ("（标称一致）", " (Matches claim)"),
        ["CounterfeitUnknown"] = ("赝品（外观疑点未知）", "Counterfeit (visual clues unknown)"),
        ["AuthenticityUnknown"] = ("真伪未知", "Authenticity unknown"),
        ["OverBudget"] = ("超预算（最低 {0}）", "Over budget (minimum {0})"),
        ["Total"] = ("合计：{0}", "Total: {0}"),
        ["Remaining"] = ("（余额 {0}）", " (Remaining: {0})"),
        ["Demand"] = ("预算：{0}  类别：{1}", "Budget: {0}  Wants: {1}"),
        ["AppraisalTitle"] = ("鉴定 — {0}", "Inspection — {0}"),
        ["TrueValue"] = ("真值：{0}", "Actual value: {0}"),
        ["EstimateExact"] = ("估价：{0} · {1} ✓", "Estimate: {0} · {1} ✓"),
        ["Estimate"] = ("估价：{0} · {1}（{2}）", "Estimate: {0} · {1} ({2})"),
        ["AlternativeStock"] = ("   → 无货；可摆：{0}（{1}）", "   → Out of stock; try: {0} ({1})"),
        ["BuyingSign"] = ("   → 挂收购牌：{0}", "   → Hang buying sign: {0}"),
        ["TypeCount"] = (" 等 {0} 种", " ({0} types total)"),
        ["NewsChange"] = ("{0} {1}：新闻{2}{3}%", "{0} {1}: market news {2}{3}%"),
        ["ActualCondition"] = ("实际：{0}", "Actual: {0}"),
        ["ClaimMismatch"] = ("　标称：{0}（不符）", "  Claimed: {0} (Mismatch)"),
        ["Issue"] = ("疑点：{0}", "Clue: {0}"),
        ["Exposed"] = ("⚠ 已识破：{0}", "⚠ Exposed: {0}"),
    };

    internal static string Text(string key)
    {
        var text = Strings[key];
        return LocaleCode.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? text.Chinese : text.English;
    }

    internal static string Format(string key, params object[] args) => string.Format(Text(key), args);
}
