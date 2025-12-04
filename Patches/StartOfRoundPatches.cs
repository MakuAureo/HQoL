using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HQoL.Util;
using Unity.Netcode;
using UnityEngine;

namespace HQoL.Patches;

[HarmonyPatch(typeof(StartOfRound))]
internal class StartOfRoundPatches
{
    //Reflection for v47+ exclusive code
    public static AccessTools.FieldRef<StartOfRound, bool> isChallengeFileRef = null!;
    public static bool hasChallengeFile = false;

    [HarmonyPatch(nameof(StartOfRound.Awake))]
    [HarmonyPrefix]
    private static void PreAwake(StartOfRound __instance)
    {
        Network.HQoLNetwork.SpawnNetworkHandler();
    }

    [HarmonyPatch(nameof(StartOfRound.Start))]
    [HarmonyPostfix]
    private static void PostStart(StartOfRound __instance)
    {
        FieldInfo? refIsChallengeFile = AccessTools.Field(typeof(StartOfRound), nameof(StartOfRound.isChallengeFile));
        if (refIsChallengeFile != null)
        {
            isChallengeFileRef = AccessTools.FieldRefAccess<StartOfRound, bool>(refIsChallengeFile);
            hasChallengeFile = true;
        }
        else
        {
            hasChallengeFile = false;
            HQoL.Logger.LogInfo("Variable isChallengeFile not found, skipping...");
        }
    }

    [HarmonyPatch(nameof(StartOfRound.SetTimeAndPlanetToSavedSettings))]
    [HarmonyPostfix]
    private static void PostSetTimeAndPlanetToSavedSettings(StartOfRound __instance)
    {
        List<ItemReference> itemRefList = ES3.Load("HQoL.ScrapList", GameNetworkManager.Instance.currentSaveFileName, new List<ItemReference>());
        Network.HQoLNetwork.Instance.AddItems(itemRefList);
    }

    [HarmonyPatch(nameof(StartOfRound.PlayFirstDayShipAnimation))]
    [HarmonyPrefix]
    private static void PrePlayFirstDayShipAnimation(StartOfRound __instance)
    {
        __instance.shipIntroSpeechSFX = __instance.disableSpeakerSFX;
    }

    [HarmonyPatch(nameof(StartOfRound.PassTimeToNextDay))]
    [HarmonyPostfix]
    private static void PostPassTimeToNextDay(StartOfRound __instance)
    {
        bool isChalFile = false;
        if (hasChallengeFile)
            isChallengeFileRef(__instance);

        if (!GameNetworkManager.Instance.isHostingGame || isChalFile)
            return;

        if (TimeOfDay.Instance.daysUntilDeadline == 0 && (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer))
        {
            List<GrabbableObject> allScrap = new(Object.FindObjectsByType<GrabbableObject>(FindObjectsSortMode.None));
            allScrap.RemoveAll(scrapObj =>
                    scrapObj.isHeld ||
                    !scrapObj.itemProperties.isScrap ||
                    HQoL.modConfig.storageException.Contains(scrapObj.name.ToLower()) || //internal obj name
                    HQoL.modConfig.storageException.Contains(scrapObj.itemProperties.name.ToLower()) || //internal scrap name
                    HQoL.modConfig.storageException.Contains(scrapObj.gameObject.GetComponentInChildren<ScanNodeProperties>().headerText.ToLower())); //scan name
            Network.HQoLNetwork.Instance.AddItems(allScrap.ToArray());
            allScrap.ForEach(scrapObj => scrapObj.NetworkObject.Despawn());
        }

        if (Network.HQoLNetwork.Instance.storageHasBeenModified)
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
    }

    [HarmonyPatch(nameof(StartOfRound.LoadShipGrabbableItems))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> TranspileLoadShipGrabbableItems(IEnumerable<CodeInstruction> codes)
    {
        CodeInstruction[] callMoveItemsToSpecialStartPosition =
        {
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Ldloc_1),
            new CodeInstruction(OpCodes.Ldloc_S, 9),
            new CodeInstruction(OpCodes.Ldelem_I4),
            new CodeInstruction(OpCodes.Ldloc_2),
            new CodeInstruction(OpCodes.Ldloc_S, 9),
            new CodeInstruction(OpCodes.Ldelema, typeof(Vector3)),
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(StartOfRoundHelper), nameof(StartOfRoundHelper.MoveItemsToSpecialStartPosition)))
        };

        CodeMatcher matcher = new CodeMatcher(codes);
        matcher.End().MatchBack(false, new CodeMatch(OpCodes.Ldloc_2)).Advance(-9);
        int endPos = matcher.Pos;

        matcher.Start()
            .MatchForward(false,
                new CodeMatch(OpCodes.Ldarg_0),
                new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(StartOfRound), nameof(StartOfRound.shipBounds))),
                new CodeMatch(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(Collider), nameof(Collider.bounds))),
                new CodeMatch(OpCodes.Stloc_S));
        int startPos = matcher.Pos;

        return matcher.RemoveInstructionsInRange(startPos, endPos)
            .Insert(callMoveItemsToSpecialStartPosition)
            .InstructionEnumeration();
    }
}

public static class StartOfRoundHelper
{
    public const int jetpackID = 4;
    public const int keyID = 5;
    public const int shovelID = 10;
    public const int shotgunID = 59;
    public const int knifeID = 68;

    public static void MoveItemsToSpecialStartPosition(StartOfRound instance, int currItemID, ref Vector3 currItemPosition)
    {
        if (currItemID == jetpackID)
        {
            currItemPosition.x = 5f;
            currItemPosition.y = instance.playerSpawnPositions[1].position.y + 0.5f;
            currItemPosition.z = -16.5f;
            return;
        }

        if (currItemID == keyID)
        {
            currItemPosition.x = -4f;
            currItemPosition.y = instance.playerSpawnPositions[1].position.y + 0.5f;
            currItemPosition.z = -13f;
            return;
        }

        if (currItemID == shovelID)
        {
            currItemPosition.x = 3.5f;
            currItemPosition.y = instance.playerSpawnPositions[1].position.y + 0.5f;
            currItemPosition.z = -14.5f;
            return;
        }

        if (currItemID == shotgunID)
        {
            currItemPosition.x = 0f;
            currItemPosition.y = instance.playerSpawnPositions[1].position.y + 0.5f;
            currItemPosition.z = -13f;
            return;
        }

        if (currItemID == knifeID)
        {
            currItemPosition.x = -1f;
            currItemPosition.y = instance.playerSpawnPositions[1].position.y + 0.5f;
            currItemPosition.z = -13f;
            return;
        }

        if (instance.allItemsList.itemsList[currItemID].isScrap)
        {
            currItemPosition.x = -4.5f;
            currItemPosition.y = instance.playerSpawnPositions[1].position.y + 0.5f;
            currItemPosition.z = -15f;
            return;
        }

        currItemPosition = instance.playerSpawnPositions[1].position;
        currItemPosition.x += (float)currItemID / 15f;
        currItemPosition.z += 2f;
        currItemPosition.y += 0.5f;
    }
}
