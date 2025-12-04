using System.Reflection;
using HarmonyLib;

namespace HQoL.Patches;

[HarmonyPatch(typeof(ItemDropship))]
internal class ItemDropshipPatches
{
    //Reflection for v56+ exclusive code
    private static AccessTools.FieldRef<ItemDropship, int> refPreOrderedVehicle = null!;
    private static bool hasCruiser = false;

    private static int preOrderedVehicle = -1;
    private static int preOrderedItemCount = 0;

    [HarmonyPatch(nameof(ItemDropship.Start))]
    [HarmonyPostfix]
    private static void PostStart(ItemDropship __instance)
    {
        FieldInfo? refOrderedVehicleFromTerminal = AccessTools.Field(typeof(ItemDropship), nameof(ItemDropship.terminalScript.orderedVehicleFromTerminal));
        if (refOrderedVehicleFromTerminal != null)
        {
            refPreOrderedVehicle = AccessTools.FieldRefAccess<ItemDropship, int>(refOrderedVehicleFromTerminal);
            hasCruiser = true;
        }

        __instance.shipAnimator.speed = 1f;

        if (StartOfRound.Instance.currentLevelID != 3)
            return;

        __instance.playersFirstOrder = false;
    }

    [HarmonyPatch(nameof(ItemDropship.LandShipClientRpc))]
    [HarmonyPrefix]
    private static void PreLandShipClientRpc(ItemDropship __instance)
    {
        if (StartOfRound.Instance.currentLevelID != 3)
            return;

        __instance.shipAnimator.speed = 5f;
    }

    [HarmonyPatch(nameof(ItemDropship.OpenShipDoorsOnServer))]
    [HarmonyPostfix]
    private static void PostOpenShipDoorsOnServer(ItemDropship __instance)
    {
        if (StartOfRound.Instance.currentLevelID != 3)
            return;

        __instance.ShipLeaveClientRpc();
    }

    [HarmonyPatch(nameof(ItemDropship.Update))]
    [HarmonyPostfix]
    private static void PostUpdate(ItemDropship __instance)
    {
        if (StartOfRound.Instance.currentLevelID != 3)
            return;

        int orderVehicle = -1;
        if (hasCruiser)
            orderVehicle = refPreOrderedVehicle(__instance);

        if (!__instance.deliveringOrder)
        {
            if (preOrderedItemCount != __instance.terminalScript.numberOfItemsInDropship || preOrderedVehicle < orderVehicle)
                __instance.shipTimer = 34f;
        }

        preOrderedItemCount = __instance.terminalScript.numberOfItemsInDropship;
        preOrderedVehicle = orderVehicle;
    }
}

[HarmonyPatch(typeof(ItemDropship))]
internal class ItemDropshipLandVehiclePatch
{
    private static MethodBase refDeliverVehicleClientRpc = null!;

    private static bool Prepare()
    {
        refDeliverVehicleClientRpc = AccessTools.Method(typeof(ItemDropship), nameof(ItemDropship.DeliverVehicleClientRpc));
        if (refDeliverVehicleClientRpc == null)
        {
            HQoL.Logger.LogInfo("Method DeliverVehicleClientRpc in ItemDropship not found, skipping...");
            return false;
        }

        return true;
    }

    private static MethodBase TargetMethod()
    {
        return refDeliverVehicleClientRpc;
    }

    private static void Prefix(ItemDropship __instance)
    {
        if (StartOfRound.Instance.currentLevelID != 3)
            return;

        __instance.shipAnimator.speed = 1f;
    }
}
