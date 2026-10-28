using System.Collections.Generic;

namespace BestDeal;

/// <summary>
/// 需求识别：读取 StoreClient 的需求（想买的类别/物品、黑名单、预算约束），
/// 用游戏自己的本地化表翻成中文；并提供需求指纹（编排层靠它发现"游戏刚填完需求"）。
/// </summary>
public static class ClientDemand
{
    public static string TagsSummary(StoreClient c)
    {
        var parts = new List<string>();
        if (c.clientBuyingTagList != null) foreach (var x in c.clientBuyingTagList) parts.Add(LocalizeTag(x));
        if (c.clientBuyingIdList != null) foreach (var x in c.clientBuyingIdList) parts.Add(LocalizeId(x));
        var s = string.Join("、", parts);
        return s.Length == 0 ? "无限制" : s;
    }

    /// <summary>顾客是否已声明了任何想买的东西（到店后游戏才填，刚到时是空的）。</summary>
    public static bool HasAnyRequirement(StoreClient c)
    {
        return (c.clientBuyingTagList != null && c.clientBuyingTagList.Count > 0)
            || (c.clientBuyingIdList != null && c.clientBuyingIdList.Count > 0);
    }

    /// <summary>
    /// 需求指纹：顾客的类别/黑名单/预算/约束任意一项变了就返回不同的值
    /// （编排层按 0.5 秒轮询它，发现"游戏刚填完需求"就重算报价）。
    /// </summary>
    internal static string RequirementSignature(StoreClient c)
    {
        var parts = new List<string>
        {
            c.identifier ?? "",
            GetBudgetSafe(c).ToString(),
            c.isRequireVariety ? "V" : "-",
            c.acceptRetailMarkup ? "R" : "-",
            c.isOfferWholesale ? "W" : "-",
        };
        if (c.clientBuyingTagList != null) foreach (var x in c.clientBuyingTagList) parts.Add("t" + x);
        if (c.clientBuyingIdList != null) foreach (var x in c.clientBuyingIdList) parts.Add("i" + x);
        if (c.clientBlackTagList != null) foreach (var x in c.clientBlackTagList) parts.Add("bt" + x);
        if (c.clientBlackIdList != null) foreach (var x in c.clientBlackIdList) parts.Add("bi" + x);
        // 顾客的买方加成特性（可能是到店后才挂上的，加了它 HUD 才会跟着刷新价格）
        if (c.clientItemFeatureBuying != null)
            foreach (var x in c.clientItemFeatureBuying)
                parts.Add("f" + x);   // 顾客的买方特性（折价/多样化等）会影响价格，变了就重算
        return string.Join("|", parts);
    }

    static long GetBudgetSafe(StoreClient c)
    {
        try { return c.GetBudget(); } catch { return 0; }
    }

    /// <summary>物品 key → 中文类型名：type_household_good → 日用品（Mechanic 表）。查不到就用原值。</summary>
    static string LocalizeTag(string key)
    {
        if (string.IsNullOrEmpty(key)) return key ?? "";
        var loc = "type_" + key.ToLowerInvariant();
        try
        {
            var s = LocHelper.GetLocalizedMechanic(loc);
            // 本地化失败时游戏返回 key 本身或空串
            if (!string.IsNullOrEmpty(s) && s != loc) return s;
        }
        catch { }
        return key;
    }

    /// <summary>物品 id → 中文名：item_scrap_metal_name → 废金属（Item 表）。查不到就用原值。</summary>
    static string LocalizeId(string id)
    {
        if (string.IsNullOrEmpty(id)) return id ?? "";
        var loc = "item_" + id + "_name";
        try
        {
            var s = LocHelper.GetLocalizedItem(loc);
            if (!string.IsNullOrEmpty(s) && s != loc) return s;
        }
        catch { }
        return id;
    }
}
