using HarmonyLib;
using UnityEngine;

namespace BestDeal;

// 原生滑块不显示数值；只补充 BestDeal 这一项的标签，不改变应用/保存行为。
[HarmonyPatch]
internal static class HeatSettingDisplay
{
    const string Key = "max_stolen_heat";
    static string _modId;

    [HarmonyPatch(typeof(ModConfigWindow), "Open")]
    [HarmonyPostfix]
    static void AfterOpen(LoadedMod mod, CustomUIWindow __result)
    {
        if (mod == null || mod.Manifest.ID != _modId || __result == null) return;
        var slider = __result.Get(Key);
        if (slider == null) return;
        UpdateLabel(__result);
        slider.OnSlider(_ => UpdateLabel(__result));
    }

    [HarmonyPatch(typeof(ModConfigWindow), "ResetWidgets")]
    [HarmonyPostfix]
    static void AfterReset(CustomUIWindow window, ModConfig config)
    {
        // 恢复默认通过无通知的 SetFloat 更新控件，需要单独同步数值。
        if (config.ModId == _modId) UpdateLabel(window);
    }

    internal static void Init(string modId) => _modId = modId;

    static void UpdateLabel(CustomUIWindow window)
    {
        if (window == null || !window.IsAlive) return;
        var slider = window.Get(Key);
        var label = window.Get("label_" + Key);
        if (slider == null || label == null) return;
        label.SetText($"{L10n.Text("SettingStolenHeat")}: {Mathf.RoundToInt(slider.GetFloat())} "
            + $"<link=\"modcfg:{_modId}:{Key}\"><sprite name=\"info\" tint=1></link>");
    }
}
