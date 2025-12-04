using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HQoL.Patches;

[HarmonyPatch]
internal class SprayPaintItemPatches
{
    private static PropertyInfo transformInfo = null!;
    private static FieldInfo isWeedKillerSprayBottleInfo = null!;

    private static MethodBase refLoadItemSaveData = null!;

    private static bool Prepare()
    {
        //Reflection to ensure the game doesn't throw in v50-
        Type sprayPaintItem = AccessTools.TypeByName("SprayPaintItem");
        if (sprayPaintItem == null)
        {
            HQoL.Logger.LogInfo("Class SprayPaintItem not found, skipping...");
            return false;
        }

        transformInfo = AccessTools.Property(sprayPaintItem, "transform");
        isWeedKillerSprayBottleInfo = AccessTools.Field(sprayPaintItem, nameof(SprayPaintItem.isWeedKillerSprayBottle));
        if (isWeedKillerSprayBottleInfo == null)
        {
            HQoL.Logger.LogInfo("Property isWeedKillerSprayBottleInfo in SprayPaintItem not found, skipping...");
            return false;
        }

        refLoadItemSaveData = AccessTools.Method(sprayPaintItem, nameof(SprayPaintItem.LoadItemSaveData));
        if (refLoadItemSaveData == null)
        {
            HQoL.Logger.LogInfo("Method LoadItemSaveData in SprayPaintItem not found, skipping...");
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
        bool isWeedKillerSprayBottle = (bool)isWeedKillerSprayBottleInfo.GetValue(__instance);
        Transform transform = (Transform)transformInfo.GetValue(__instance);

        if (isWeedKillerSprayBottle)
        {
            Vector3 pos = transform.position;
            pos += new Vector3(-3f - (float)saveData/50f, 0f, -2f);
            transform.position = pos;
        }
    }
}
