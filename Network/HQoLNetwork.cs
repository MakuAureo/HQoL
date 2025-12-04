using System.Collections.Generic;
using System.Linq;
using HQoL.Util;
using Unity.Netcode;
using UnityEngine;
using static Unity.Netcode.XXHash;
using Object = UnityEngine.Object;

namespace HQoL.Network;

internal class HQoLNetwork : NetworkBehaviour
{
    private static GameObject prefab = null!;
    public static HQoLNetwork Instance { get; private set; } = null!;

    public NetworkList<ItemReference> netStorage = new NetworkList<ItemReference>();
    public NetworkVariable<int> totalStorageValue = new(0);
    public bool storageHasBeenModified = false;

    public static void CreateAndRegisterPrefab()
    {
        if (prefab != null)
            return;

        prefab = new GameObject(MyPluginInfo.PLUGIN_GUID + " Prefab");
        prefab.hideFlags |= HideFlags.HideAndDontSave;
        NetworkObject networkObject = prefab.AddComponent<NetworkObject>();
        networkObject.GlobalObjectIdHash = prefab.name.Hash32();
        prefab.AddComponent<HQoLNetwork>();
        NetworkManager.Singleton.AddNetworkPrefab(prefab);

        HQoL.Logger.LogInfo("Network prefab created and registered");
    }

    public static void SpawnNetworkHandler()
    {
        if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)
        {
            Object.Instantiate(prefab).GetComponent<NetworkObject>().Spawn();
            HQoL.Logger.LogInfo("Network handler spawned");
        }
    }

    public static void DespawnNetworkHandler()
    {
        if (Instance != null && Instance.gameObject.GetComponent<NetworkObject>().IsSpawned && (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost))
        {
            Instance.gameObject.GetComponent<NetworkObject>().Despawn();
            HQoL.Logger.LogInfo("Network handler despawned");
        }
    }

    private void Awake()
    {
        Instance = this;
    }

    //All add/clear item methods are only ever called on server so it doesn't need an RPC
    public void AddItem(GrabbableObject item)
    {
        netStorage.Add(new() { itemName = item.gameObject.GetComponentInChildren<ScanNodeProperties>().headerText, value = item.scrapValue });
        totalStorageValue.Value += item.scrapValue;
        storageHasBeenModified = true;
    }

    public void AddItems(GrabbableObject[] items)
    {
        foreach (GrabbableObject item in items) AddItem(item);
    }

    public void AddItems(List<ItemReference> itemRefs)
    {
        foreach (ItemReference itemRef in itemRefs)
        {
            netStorage.Add(itemRef);
            totalStorageValue.Value += itemRef.value;
        }
    }

    public void ClearItems()
    {
        netStorage.Clear();
        totalStorageValue.Value = 0;
    }

    [ServerRpc(RequireOwnership = false)]
    public void SellItemsServerRpc(int creditsEarned, int[] indicesToSell)
    {
        List<int> sellList = new List<int>(indicesToSell).Distinct().OrderByDescending(i => i).ToList();
        foreach (int index in sellList)
        {
            totalStorageValue.Value -= netStorage[index].value;
            netStorage.RemoveAt(index);
            storageHasBeenModified = true;
        }

        UpdateQuotaAndDisplayCreditsEarningClientRpc(creditsEarned);
    }

    [ClientRpc]
    public void UpdateQuotaAndDisplayCreditsEarningClientRpc(int creditsEarned)
    {
        StartOfRound.Instance.gameStats.scrapValueCollected += creditsEarned;
        Patches.TerminalHelper.terminal.groupCredits += creditsEarned;
        TimeOfDay.Instance.quotaFulfilled += creditsEarned;
        TimeOfDay.Instance.UpdateProfitQuotaCurrentTime();
        HUDManager.Instance.moneyRewardsListText.text = "";
        HUDManager.Instance.moneyRewardsTotalText.text = $"TOTAL: ${creditsEarned}";
        HUDManager.Instance.moneyRewardsAnimator.SetTrigger("showRewards");
        HUDManager.Instance.rewardsScrollbar.value = 1f;
    }

    [ServerRpc(RequireOwnership = false)]
    public void DepositItemsServerRpc(string itemName)
    {
        List<GrabbableObject> allScrap = new(Object.FindObjectsByType<GrabbableObject>(FindObjectsSortMode.None));

        if (itemName == "all")
        {
            allScrap.RemoveAll(scrapObj =>
                    scrapObj.isHeld ||
                    !scrapObj.itemProperties.isScrap ||
                    HQoL.modConfig.storageException.Contains(scrapObj.name.ToLower()) || //internal obj name
                    HQoL.modConfig.storageException.Contains(scrapObj.itemProperties.name.ToLower()) || //internal scrap name
                    HQoL.modConfig.storageException.Contains(scrapObj.gameObject.GetComponentInChildren<ScanNodeProperties>().headerText.ToLower()) || //scan name
                    (
                     TimeOfDay.Instance.daysUntilDeadline != 0 &&
                     !StartOfRound.Instance.shipInnerRoomBounds.bounds.Contains(scrapObj.transform.position)
                    ));
        }
        else
        {
            allScrap.RemoveAll(scrapObj =>
                    scrapObj.isHeld ||
                    !scrapObj.itemProperties.isScrap ||
                    scrapObj.gameObject.GetComponentInChildren<ScanNodeProperties>().headerText != itemName ||
                    (
                     TimeOfDay.Instance.daysUntilDeadline != 0 &&
                     !StartOfRound.Instance.shipInnerRoomBounds.bounds.Contains(scrapObj.transform.position)
                    ));
        }

        AddItems(allScrap.ToArray());
        allScrap.ForEach(scrapObj => scrapObj.NetworkObject.Despawn());

        storageHasBeenModified = true;
    }
}
