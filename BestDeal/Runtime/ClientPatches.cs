using HarmonyLib;

namespace BestDeal;

// Harmony 补丁由游戏模组加载器自动应用（扫描 [HarmonyPatch] 类），不要自己 PatchAll
[HarmonyPatch(typeof(StoreClient), nameof(StoreClient.StartMainDialogue))]
public static class StoreClient_StartMainDialogue_Patch
{
    static void Postfix(StoreClient __instance) => ClientSession.OnClientArrived(__instance);
}

[HarmonyPatch(typeof(StoreClient), nameof(StoreClient.CompleteBusiness))]
public static class StoreClient_CompleteBusiness_Patch
{
    static void Postfix(StoreClient __instance) => ClientSession.OnBusinessDone(__instance);
}
