using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BestDeal;

/// <summary>
/// 真价判定（价格探测）：游戏只在"物品被摆给当前顾客看"的时候才算最终价（实测：背包里读 55，
/// 摆上柜台后变 64/37）。所以这里把候选物品逐件放上柜台 → 读走游戏算好的价 → 立刻收回原库存
/// （一件一件、每步校验）。只借价格，不改归属：源库存取物品自己的 parentInventory，收回后校验，
/// 收回失败立即停手并告警。
/// 柜台 = ShopInventories.BackCounter：游戏自己的 MoveItemToBackCounter 就是往这里放，
/// 顾客议价读的也是它。
/// </summary>
public static class PriceProbe
{
    private static readonly Dictionary<GameItem, long> Prices = new();   // 物品 → 游戏算的最终价
    private static readonly List<GameItem> Queue = new();
    private static Action _completed;             // 一轮探完的回调（编排层用它触发重算）
    private static GameItem _cur;
    private static GameInventory _src, _dst;
    private static float _nextAt;
    private static int _total;                    // 本轮要探的件数（算进度用）
    private static List<string> _varietyBefore;   // 探测前顾客的多样性记录（探测后把新增项删掉，别污染判定）

    public static bool Running => _cur != null || Queue.Count > 0;

    /// <summary>探测进度（HUD 每帧直接画；为空表示没在探测）。</summary>
    public static string Status
    {
        get
        {
            if (!Running) return null;
            int done = Math.Max(0, _total - Queue.Count - (_cur != null ? 1 : 0));
            return $"价格探测中 {done}/{_total}：物品会短暂摆上柜台，读完价自动收回";
        }
    }

    /// <summary>游戏给这件物品算出的最终价（没探测过返回 0）。</summary>
    public static long PriceFor(GameItem gi)
    {
        try { return Prices.TryGetValue(gi, out var v) ? v : 0; }
        catch { return 0; }
    }

    /// <summary>
    /// 给当前顾客安排一轮探测（每件约 0.25 秒），全部读完时回调 onCompleted。
    /// 找不到柜台就返回 false（什么也不动）。
    /// </summary>
    public static bool Start(StoreClient c, IEnumerable<GameItem> items, Action onCompleted)
    {
        var dst = ShopInventories.BackCounter;
        if (dst == null)
        {
            BestDealMod.Log.Warning("价格探测：没找到后柜台（backInvinvElement），本轮跳过");
            return false;
        }
        Prices.Clear();
        Queue.Clear();
        _cur = null;
        _completed = onCompleted;
        _varietyBefore = SnapshotVariety(c);
        foreach (var gi in items.Take(16)) Queue.Add(gi);
        _total = Queue.Count;
        _dst = dst;
        _nextAt = Time.unscaledTime;
        return true;
    }

    public static void Stop(StoreClient c)
    {
        if (_cur != null) ReturnCurrent();          // 别把东西留在柜台上
        Queue.Clear();
        _total = 0;
        _completed = null;
        RestoreVariety(c);
    }

    /// <summary>由编排层每帧驱动：一步一件。</summary>
    public static void Step(StoreClient c)
    {
        if (Time.unscaledTime < _nextAt) return;
        if (_dst == null) { _dst = ShopInventories.BackCounter; if (_dst == null) { Queue.Clear(); _total = 0; return; } }

        if (_cur == null)
        {
            if (Queue.Count == 0) return;
            var gi = Queue[0];
            Queue.RemoveAt(0);
            try { _src = gi.parentInventory; } catch { _src = null; }
            if (_src == null) return;
            _cur = gi;
            try { _src.Expel(gi); } catch (Exception e) { BestDealMod.Log.Warning($"价格探测：取出 {NameOf(gi)} 失败：{e.Message}"); _cur = null; return; }
            try { _dst.UncheckedAccept(gi); }
            catch (Exception e)
            {
                BestDealMod.Log.Warning($"价格探测：上柜台失败，已放回：{e.Message}");
                try { _src.UncheckedAccept(gi); } catch { }
                _cur = null;
                return;
            }
            _nextAt = Time.unscaledTime + 0.25f;    // 留一点时间给游戏算价
            return;
        }

        long price = 0;
        try { price = _cur.GetNegociatedValue(); } catch { }
        if (price > 0) { try { Prices[_cur] = price; } catch { } }
        if (_cur != null) ReturnCurrent();
        _nextAt = Time.unscaledTime + 0.1f;
        if (Queue.Count == 0)
        {
            RestoreVariety(c);                              // 一轮结束：清掉探测造成的"多样性记录"
            var done = _completed;
            _completed = null;
            done?.Invoke();                                 // 用游戏真价重算 HUD
        }
    }

    /// <summary>把当前这件从柜台收回原库存，并校验真的回去了。</summary>
    static void ReturnCurrent()
    {
        var item = _cur;
        _cur = null;
        if (item == null) return;
        try
        {
            var where = item.parentInventory;
            if (where == null) return;
            if (ReferenceEquals(where, _src)) return;             // 本来就在原位
            if (_dst == null || !ReferenceEquals(where, _dst)) return;   // 被顾客买走/挪走了：别去抢
            where.Expel(item);
            if (_src != null) _src.UncheckedAccept(item);
            var back = item.parentInventory;
            if (!ReferenceEquals(back, _src))
                BestDealMod.Log.Warning($"价格探测：{NameOf(item)} 没能放回原处，快检查柜台！");
        }
        catch (Exception e)
        {
            BestDealMod.Log.Warning($"价格探测：收回 {NameOf(item)} 出错：{e.Message}");
        }
    }

    static string NameOf(GameItem gi) { try { return gi.name ?? "?"; } catch { return "?"; } }

    /// <summary>探测前的多样性记录。</summary>
    static List<string> SnapshotVariety(StoreClient c)
    {
        try { return c?.varietyTrackingList != null ? new List<string>(c.varietyTrackingList) : null; }
        catch { return null; }
    }

    /// <summary>探测会让顾客"见过"这些物品，把新增的多样性记录删掉，免得影响"要求不重复"的判定。</summary>
    static void RestoreVariety(StoreClient c)
    {
        if (c == null || _varietyBefore == null) return;
        try
        {
            var list = c.varietyTrackingList;
            if (list != null)
            {
                foreach (var x in new List<string>(list))
                    if (!_varietyBefore.Contains(x)) list.Remove(x);
            }
        }
        catch { }
        _varietyBefore = null;
    }
}
