using HarmonyLib;

namespace HQoL.Patches;

[HarmonyPatch(typeof(RoundManager))]
internal class RoundManagerPatches
{
    [HarmonyPatch(nameof(RoundManager.DespawnPropsAtEndOfRound))]
    [HarmonyPostfix]
    private static void PostDespawnPropsAtEndOfRound(RoundManager __instance)
    {
        if (StartOfRound.Instance.allPlayersDead && __instance.IsServer)
            Network.HQoLNetwork.Instance.ClearItems();
    }
}
