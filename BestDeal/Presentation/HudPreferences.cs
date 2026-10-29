using System;
using UnityEngine;

namespace BestDeal;

/// <summary>仅保存 HUD 偏好，独立于游戏存档，使用官方 mod 文件接口。</summary>
[Serializable]
internal class HudPreferences
{
    const string FileName = "hud.json";
    public bool collapsed;
    public float x = 16, y = -68;
    string _saved;

    internal static HudPreferences Load(string modId)
    {
        try
        {
            if (ModHelper.ModDataFileExists(modId, FileName))
            {
                var result = new HudPreferences();
                JsonUtility.FromJsonOverwrite(ModHelper.ReadModDataFileText(modId, FileName), result);
                if (!float.IsNaN(result.x) && !float.IsInfinity(result.x)
                    && !float.IsNaN(result.y) && !float.IsInfinity(result.y)) return result;
            }
        }
        catch (Exception e) { BestDealMod.Log.Warning($"HUD 偏好读取失败：{e.Message}"); }
        return new HudPreferences();
    }

    internal void Save(string modId)
    {
        var json = JsonUtility.ToJson(this);
        if (json == _saved) return;
        try
        {
            ModHelper.WriteModDataFileText(modId, FileName, json);
            _saved = json;
        }
        catch (Exception e) { BestDealMod.Log.Warning($"HUD 偏好保存失败：{e.Message}"); }
    }
}
