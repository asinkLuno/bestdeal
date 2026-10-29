using System.Collections.Generic;

namespace BestDeal;

/// <summary>按玩家任务保存展示状态；各功能只更新自己的内容。</summary>
internal static class HudContent
{
    internal static readonly List<string> Preparation = new();
    internal static readonly List<string> QuoteSummary = new();
    internal static readonly List<string> QuoteItems = new();

    internal static void ClearQuote()
    {
        QuoteSummary.Clear();
        QuoteItems.Clear();
    }

    internal static void Clear()
    {
        Preparation.Clear();
        ClearQuote();
    }
}
