using System;
using System.Collections.Generic;
using HQoL.Network;
using Unity.Collections;
using Unity.Netcode;

namespace HQoL.Util;

internal class SellModule
{
    public int sellValue;
    public List<int> itemReferenceIndexToSell;
    public Dictionary<string, int> itemTypesToSell;

    public SellModule()
    {
        itemTypesToSell = new();
        itemReferenceIndexToSell = new();
    }

    public bool FindItemsWithTotalValue(int value, bool useOvertime)
    {
        int neededSum;
        int sell = value;

        //Binary search for perfect sell with overtime
        int left = 0;
        int right = 10*value;
        int quota = TimeOfDay.Instance.profitQuota;
        int daysLeft = TimeOfDay.Instance.daysUntilDeadline == 0 ? -1 : TimeOfDay.Instance.daysUntilDeadline;
        float rate = StartOfRound.Instance.companyBuyingRate;
        while (useOvertime)
        {
            int pre_sell = sell;

            sell = (left + right)/2;
            int overtime = Math.Max(((int)((float)sell * rate) - quota) / 5 + 15 * daysLeft, 0);
            if ((int)((float)sell * rate) + overtime >= value)
                right = sell;
            else
                left = sell;

            if ((int)((float)sell*rate) + overtime == value)
                break;

            //Edge case in which perfect sell isn't possible, sell 1 over
            if (pre_sell == sell)
            {
                sell++;
                break;
            }
        }

        neededSum = sell;

        int maxSum = 0;
        foreach (ItemReference itemRef in Network.HQoLNetwork.Instance.netStorage)
            maxSum += itemRef.value;

        if (neededSum > maxSum)
            return FindAllItems();
        else if (neededSum < 1)
            return false;

        bool[,] possibleSumsMatrix = new bool[maxSum + 1, Network.HQoLNetwork.Instance.netStorage.Count + 1];
        for (int r = 0; r < maxSum + 1; r++)
            for (int c = 0; c < Network.HQoLNetwork.Instance.netStorage.Count + 1; c++)
                possibleSumsMatrix[r, c] = false;
        possibleSumsMatrix[0, 0] = true;

        for (int sum = 0; sum < maxSum + 1; sum++)
        {
            for (int itemIdx = 0; itemIdx < Network.HQoLNetwork.Instance.netStorage.Count; itemIdx++)
            {
                if (sum >= Network.HQoLNetwork.Instance.netStorage[itemIdx].value)
                    possibleSumsMatrix[sum, itemIdx + 1] = possibleSumsMatrix[sum, itemIdx] || possibleSumsMatrix[sum - Network.HQoLNetwork.Instance.netStorage[itemIdx].value, itemIdx];
                else
                    possibleSumsMatrix[sum, itemIdx + 1] = possibleSumsMatrix[sum, itemIdx];

                if (sum >= neededSum && possibleSumsMatrix[sum, Network.HQoLNetwork.Instance.netStorage.Count])
                    goto BracktrackSavingFoundItems;
            }
        }

    BracktrackSavingFoundItems:
        itemReferenceIndexToSell.Clear();
        itemTypesToSell.Clear();
        int sumLookUp = neededSum;
        int reqItems = Network.HQoLNetwork.Instance.netStorage.Count;
        while (!possibleSumsMatrix[sumLookUp, reqItems])
            sumLookUp += 1;

        sellValue = sumLookUp;
        while (sumLookUp != 0)
        {
            while (possibleSumsMatrix[sumLookUp, reqItems - 1])
                reqItems -= 1;

            reqItems -= 1;
            sumLookUp -= Network.HQoLNetwork.Instance.netStorage[reqItems].value;
            itemReferenceIndexToSell.Add(reqItems);
            if (itemTypesToSell.ContainsKey(Network.HQoLNetwork.Instance.netStorage[reqItems].itemName.ToString()))
                itemTypesToSell[Network.HQoLNetwork.Instance.netStorage[reqItems].itemName.ToString()]++;
            else
                itemTypesToSell[Network.HQoLNetwork.Instance.netStorage[reqItems].itemName.ToString()] = 1;
        }

        return true;
    }

    public bool FindAllItems()
    {
        sellValue = HQoLNetwork.Instance.totalStorageValue.Value;
        itemReferenceIndexToSell.Clear();
        itemTypesToSell.Clear();
        for (int i = 0; i < Network.HQoLNetwork.Instance.netStorage.Count; i++)
        {
            itemReferenceIndexToSell.Add(i);
            if (itemTypesToSell.ContainsKey(Network.HQoLNetwork.Instance.netStorage[i].itemName.ToString()))
                itemTypesToSell[Network.HQoLNetwork.Instance.netStorage[i].itemName.ToString()]++;
            else
                itemTypesToSell[Network.HQoLNetwork.Instance.netStorage[i].itemName.ToString()] = 1;
        }

        return true;
    }

    public void ClearSellModule()
    {
        sellValue = 0;
        itemTypesToSell.Clear();
        itemReferenceIndexToSell.Clear();
    }
}

public struct ItemReference : INetworkSerializable, IEquatable<ItemReference>
{
    public FixedString32Bytes itemName;
    public int value;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref itemName);
        serializer.SerializeValue(ref value);
    }

    public bool Equals(ItemReference other)
    {
        return itemName == other.itemName && value == other.value;
    }
}
