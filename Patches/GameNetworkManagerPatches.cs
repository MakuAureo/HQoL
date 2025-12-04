using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using HQoL.Util;

namespace HQoL.Patches;

[HarmonyPatch(typeof(GameNetworkManager))]
internal class GameNetworkManagerPatches
{
    [HarmonyPatch(nameof(GameNetworkManager.Start))]
    [HarmonyPostfix]
    private static void PostStart(GameNetworkManager __instance)
    {
        Network.HQoLNetwork.CreateAndRegisterPrefab();
    }

    [HarmonyPatch(nameof(GameNetworkManager.Disconnect))]
    [HarmonyPrefix]
    private static void PreDisconnect(GameNetworkManager __instance)
    {
        bool isChalFile = false;
        if (StartOfRoundPatches.hasChallengeFile)
            isChalFile = StartOfRoundPatches.isChallengeFileRef(StartOfRound.Instance);

        if (Network.HQoLNetwork.Instance.storageHasBeenModified && __instance.isHostingGame && StartOfRound.Instance.inShipPhase && !isChalFile)
        {
            try
            {
                List<ItemReference> itemRefList = new();
                foreach (ItemReference itemRef in Network.HQoLNetwork.Instance.netStorage)
                    itemRefList.Add(itemRef);

                ES3.Save("HQoL.ScrapList", itemRefList, GameNetworkManager.Instance.currentSaveFileName);
                Network.HQoLNetwork.Instance.storageHasBeenModified = false;
            }
            catch (System.Exception arg)
            {
                HQoL.Logger.LogError($"Error while trying to save game values when disconnecting as host: {arg}");
            }
        }

        Network.HQoLNetwork.DespawnNetworkHandler();
    }

    //Fix the game not saving 0 items if there is no scrap and respawning them upon lobby restart
    //Basically an infinite money glitch :p
    [HarmonyPatch(nameof(GameNetworkManager.SaveItemsInShip))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> TranspileSaveItemsInShip(IEnumerable<CodeInstruction> codes)
    {
        CodeMatcher matcher = 
            new CodeMatcher(codes)
            .MatchForward(false,
                new CodeMatch(OpCodes.Ldloc_1),
                new CodeMatch(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(List<int>), nameof(List<int>.Count))),
                new CodeMatch(OpCodes.Ldc_I4_0),
                new CodeMatch(OpCodes.Bgt));
        
        List<Label> label = matcher.Labels;

        return matcher
            .RemoveInstructions(7)
            .AddLabels(label)
            .InstructionEnumeration();
    }
}
