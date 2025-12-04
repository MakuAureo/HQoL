using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HQoL.Patches;

[HarmonyPatch]
internal class ShotgunItemPatches
{
    private static PropertyInfo transformInfo = null!;

    private static MethodBase refLoadItemSaveData = null!;

    private static bool Prepare()
    {
        //Reflection to ensure the game doesn't throw in v40
        Type shotgunItem = AccessTools.TypeByName("ShotgunItem");
        if (shotgunItem == null)
        {
            HQoL.Logger.LogInfo("Class ShotgunItem not found, skipping...");
            return false;
        }

        transformInfo = AccessTools.Property(shotgunItem, "transform");

        refLoadItemSaveData = AccessTools.Method(shotgunItem, nameof(ShotgunItem.LoadItemSaveData));
        if (refLoadItemSaveData == null)
        {
            HQoL.Logger.LogInfo("Method LoadItemSaveData in ShotgunItem not found, skipping...");
            return false;
        }

        return true;
    }

    private static MethodBase TargetMethod()
    {
        return refLoadItemSaveData;
    }

    private static void Prefix(object __instance, int saveData)
    {
        Transform modTransform = (Transform)transformInfo.GetValue(__instance);
        Vector3 pos = modTransform.position;
        pos += new Vector3(1f - (float)saveData/2f, 0f, 0f);
        modTransform.position = pos;
    }
}
