using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HQoL.Util;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HQoL.Patches;

[HarmonyPatch(typeof(Terminal))]
internal class TerminalPatches
{
    [HarmonyPatch(nameof(Terminal.ParsePlayerSentence))]
    [HarmonyPrefix]
    static void ParsePlayerSentencePrePatch(Terminal __instance)
    {
        TerminalHelper.terminalInput = __instance.screenText.text.Substring(__instance.screenText.text.Length - __instance.textAdded).ToLower();
    }

    [HarmonyPatch(nameof(Terminal.Start))]
    [HarmonyPostfix]
    private static void PostStart(Terminal __instance)
    {
        TerminalHelper.terminal = Object.FindAnyObjectByType<Terminal>();
        __instance.topRightText.enableAutoSizing = true;

        if (!TerminalHelper.internalStateInitialized)
        {
            TerminalHelper.TerminalNodes.InitializeNodes();
            TerminalHelper.TerminalKeywords.InitializeKeywords();
            TerminalHelper.TerminalKeywords.defaultVerb.compatibleNouns[0].result = TerminalHelper.TerminalNodes.sellCommand;
            TerminalHelper.TerminalKeywords.defaultVerb.compatibleNouns[1].result = TerminalHelper.TerminalNodes.depositCommand;
            TerminalHelper.TerminalKeywords.defaultVerb.compatibleNouns[2].result = TerminalHelper.TerminalNodes.checkScrap;
            TerminalHelper.TerminalKeywords.defaultVerb.compatibleNouns[3].result = TerminalHelper.TerminalNodes.confirmOvertime;
            TerminalHelper.internalStateInitialized = true;
        }

        if (!TerminalHelper.addedCustomKeywords)
        {
            TerminalHelper.TerminalKeywords.sellWord.defaultVerb = TerminalHelper.TerminalKeywords.defaultVerb;
            TerminalHelper.TerminalKeywords.depositWord.defaultVerb = TerminalHelper.TerminalKeywords.defaultVerb;
            TerminalHelper.TerminalKeywords.checkScrapWord.defaultVerb = TerminalHelper.TerminalKeywords.defaultVerb;
            TerminalHelper.TerminalKeywords.overtimeWord.defaultVerb = TerminalHelper.TerminalKeywords.defaultVerb;

            int size = __instance.terminalNodes.allKeywords.Count();
            List<TerminalKeyword> keywords = new(__instance.terminalNodes.allKeywords);
            keywords.Add(TerminalHelper.TerminalKeywords.sellWord);
            keywords.Add(TerminalHelper.TerminalKeywords.depositWord);
            keywords.Add(TerminalHelper.TerminalKeywords.checkScrapWord);
            keywords.Add(TerminalHelper.TerminalKeywords.overtimeWord);
            keywords.Add(TerminalHelper.TerminalKeywords.defaultVerb);
            __instance.terminalNodes.allKeywords = keywords.ToArray();

            TerminalHelper.addedCustomKeywords = true;
        }
    }

    [HarmonyPatch(nameof(Terminal.Update))]
    [HarmonyPostfix]
    private static void PostUpdate(Terminal __instance)
    {
        if (__instance.terminalInUse)
        {
            __instance.topRightText.text += $"\n<color=#09DBB5>${Network.HQoLNetwork.Instance.totalStorageValue.Value}";
        }
    }

    [HarmonyPatch(nameof(Terminal.LoadNewNode))]
    [HarmonyPostfix]
    private static void PostLoadNewNode(Terminal __instance)
    {
        if (!TerminalHelper.isSellValid && __instance.currentNode == TerminalHelper.TerminalNodes.sellCommand)
            __instance.currentNode = TerminalHelper.TerminalNodes.sellDeny;

        else if (!TerminalHelper.isDepositValid && __instance.currentNode == TerminalHelper.TerminalNodes.depositCommand)
            __instance.currentNode = TerminalHelper.TerminalNodes.depositDeny;
    }

    [HarmonyPatch(nameof(Terminal.TextPostProcess))]
    [HarmonyPrefix]
    private static void PreTextPostProcess(Terminal __instance, ref string modifiedDisplayText)
    {
        if (modifiedDisplayText.Contains(TerminalHelper.sellConfirmString))
            modifiedDisplayText = modifiedDisplayText.Replace(TerminalHelper.sellConfirmString, TerminalHelper.SellItems());

        else if (modifiedDisplayText.Contains(TerminalHelper.sellCommandString))
            modifiedDisplayText = modifiedDisplayText.Replace(TerminalHelper.sellCommandString, TerminalHelper.FindItemsToSell());

        else if (modifiedDisplayText.Contains(TerminalHelper.depositConfirmString))
            modifiedDisplayText = modifiedDisplayText.Replace(TerminalHelper.depositConfirmString, TerminalHelper.DepositItems());

        else if (modifiedDisplayText.Contains(TerminalHelper.depositCommandString))
            modifiedDisplayText = modifiedDisplayText.Replace(TerminalHelper.depositCommandString, TerminalHelper.FindItemsToDeposit());

        else if (modifiedDisplayText.Contains(TerminalHelper.confirmOvertimeString))
            modifiedDisplayText = modifiedDisplayText.Replace(TerminalHelper.confirmOvertimeString, TerminalHelper.ConfirmOvertime());

        else if (modifiedDisplayText.Contains(TerminalHelper.checkScrapString))
            modifiedDisplayText = modifiedDisplayText.Replace(TerminalHelper.checkScrapString, TerminalHelper.CheckItems());
    }
}

internal static class TerminalHelper
{
    public const string sellConfirmString = "[sellConfirm]";
    public const string sellCommandString = "[sell]";
    public const string confirmOvertimeString = "[confirmOvertime]";
    public const string depositConfirmString = "[depositConfirm]";
    public const string depositCommandString = "[deposit]";
    public const string checkScrapString = "[checkScrap]";

    public static Terminal terminal = null!;
    public static string terminalInput = null!;
    public static bool addedCustomKeywords = false;
    public static bool internalStateInitialized = false;
    public static bool isSellValid = false;
    public static bool isDepositValid = false;

    private static string itemNameToDeposit = "";

    public class TerminalNodes
    {
        public static TerminalNode sellDeny = null!;
        public static TerminalNode sellConfirm = null!;
        public static TerminalNode sellCommand = null!;

        public static TerminalNode confirmOvertime = null!;

        public static TerminalNode depositDeny = null!;
        public static TerminalNode depositConfirm = null!;
        public static TerminalNode depositCommand = null!;

        public static TerminalNode checkScrap = null!;

        public static void InitializeNodes()
        {
            sellDeny = ScriptableObject.CreateInstance<TerminalNode>();
            sellDeny.acceptAnything = false;
            sellDeny.buyItemIndex = -1;
            sellDeny.buyRerouteToMoon = -1;
            sellDeny.buyUnlockable = false;
            sellDeny.clearPreviousText = false;
            sellDeny.creatureFileID = -1;
            sellDeny.creatureName = "";
            sellDeny.displayPlanetInfo = -1;
            sellDeny.displayText = "\nSell canceled.\n\n";
            sellDeny.displayTexture = null;
            sellDeny.displayVideo = null;
            sellDeny.hideFlags = UnityEngine.HideFlags.None;
            sellDeny.isConfirmationNode = false;
            sellDeny.itemCost = 0;
            sellDeny.loadImageSlowly = false;
            sellDeny.maxCharactersToType = 35;
            sellDeny.name = "sellDeny";
            sellDeny.overrideOptions = false;
            sellDeny.persistentImage = false;
            sellDeny.playClip = null;
            sellDeny.playSyncedClip = -1;
            sellDeny.returnFromStorage = false;
            sellDeny.shipUnlockableID = -1;
            sellDeny.storyLogFileID = -1;
            sellDeny.terminalEvent = "";
            sellDeny.terminalOptions = new CompatibleNoun[0] { };

            sellConfirm = ScriptableObject.CreateInstance<TerminalNode>();
            sellConfirm.acceptAnything = false;
            sellConfirm.buyItemIndex = -1;
            sellConfirm.buyRerouteToMoon = -1;
            sellConfirm.buyUnlockable = false;
            sellConfirm.clearPreviousText = false;
            sellConfirm.creatureFileID = -1;
            sellConfirm.creatureName = "";
            sellConfirm.displayPlanetInfo = -1;
            sellConfirm.displayText = TerminalHelper.sellConfirmString;
            sellConfirm.displayTexture = null;
            sellConfirm.displayVideo = null;
            sellConfirm.hideFlags = UnityEngine.HideFlags.None;
            sellConfirm.isConfirmationNode = false;
            sellConfirm.itemCost = 0;
            sellConfirm.loadImageSlowly = false;
            sellConfirm.maxCharactersToType = 35;
            sellConfirm.name = "sellConfirm";
            sellConfirm.overrideOptions = false;
            sellConfirm.persistentImage = false;
            sellConfirm.playClip = null;
            sellConfirm.playSyncedClip = 0;
            sellConfirm.returnFromStorage = false;
            sellConfirm.shipUnlockableID = -1;
            sellConfirm.storyLogFileID = -1;
            sellConfirm.terminalEvent = "";
            sellConfirm.terminalOptions = new CompatibleNoun[0] { };

            sellCommand = ScriptableObject.CreateInstance<TerminalNode>();
            sellCommand.acceptAnything = false;
            sellCommand.buyItemIndex = -1;
            sellCommand.buyRerouteToMoon = -1;
            sellCommand.buyUnlockable = false;
            sellCommand.clearPreviousText = true;
            sellCommand.creatureFileID = -1;
            sellCommand.creatureName = "";
            sellCommand.displayPlanetInfo = -1;
            sellCommand.displayText = TerminalHelper.sellCommandString;
            sellCommand.displayTexture = null;
            sellCommand.displayVideo = null;
            sellCommand.hideFlags = UnityEngine.HideFlags.None;
            sellCommand.isConfirmationNode = true;
            sellCommand.itemCost = 0;
            sellCommand.loadImageSlowly = false;
            sellCommand.maxCharactersToType = 35;
            sellCommand.name = "sellCommand";
            sellCommand.overrideOptions = true;
            sellCommand.persistentImage = false;
            sellCommand.playClip = null;
            sellCommand.playSyncedClip = -1;
            sellCommand.returnFromStorage = false;
            sellCommand.shipUnlockableID = -1;
            sellCommand.storyLogFileID = -1;
            sellCommand.terminalEvent = "";
            sellCommand.terminalOptions = new CompatibleNoun[2]
            {
                new()
                {
                    noun = terminal.terminalNodes.allKeywords[3],
                    result = sellConfirm
                },
                new()
                {
                    noun = terminal.terminalNodes.allKeywords[4],
                    result = sellDeny
                }
            };

            confirmOvertime = ScriptableObject.CreateInstance<TerminalNode>();
            confirmOvertime.acceptAnything = false;
            confirmOvertime.buyItemIndex = -1;
            confirmOvertime.buyRerouteToMoon = -1;
            confirmOvertime.buyUnlockable = false;
            confirmOvertime.clearPreviousText = true;
            confirmOvertime.creatureFileID = -1;
            confirmOvertime.creatureName = "";
            confirmOvertime.displayPlanetInfo = -1;
            confirmOvertime.displayText = confirmOvertimeString;
            confirmOvertime.displayTexture = null;
            confirmOvertime.displayVideo = null;
            confirmOvertime.hideFlags = UnityEngine.HideFlags.None;
            confirmOvertime.isConfirmationNode = false;
            confirmOvertime.itemCost = 0;
            confirmOvertime.loadImageSlowly = false;
            confirmOvertime.maxCharactersToType = 35;
            confirmOvertime.name = "confirmOvertime";
            confirmOvertime.overrideOptions = false;
            confirmOvertime.persistentImage = false;
            confirmOvertime.playClip = null;
            confirmOvertime.playSyncedClip = -1;
            confirmOvertime.returnFromStorage = false;
            confirmOvertime.shipUnlockableID = -1;
            confirmOvertime.storyLogFileID = -1;
            confirmOvertime.terminalEvent = "";
            confirmOvertime.terminalOptions = new CompatibleNoun[0] { };

            depositDeny = ScriptableObject.CreateInstance<TerminalNode>();
            depositDeny.acceptAnything = false;
            depositDeny.buyItemIndex = -1;
            depositDeny.buyRerouteToMoon = -1;
            depositDeny.buyUnlockable = false;
            depositDeny.clearPreviousText = false;
            depositDeny.creatureFileID = -1;
            depositDeny.creatureName = "";
            depositDeny.displayPlanetInfo = -1;
            depositDeny.displayText = "\nItem storing canceled.\n\n";
            depositDeny.displayTexture = null;
            depositDeny.displayVideo = null;
            depositDeny.hideFlags = UnityEngine.HideFlags.None;
            depositDeny.isConfirmationNode = false;
            depositDeny.itemCost = 0;
            depositDeny.loadImageSlowly = false;
            depositDeny.maxCharactersToType = 35;
            depositDeny.name = "depositDeny";
            depositDeny.overrideOptions = false;
            depositDeny.persistentImage = false;
            depositDeny.playClip = null;
            depositDeny.playSyncedClip = -1;
            depositDeny.returnFromStorage = false;
            depositDeny.shipUnlockableID = -1;
            depositDeny.storyLogFileID = -1;
            depositDeny.terminalEvent = "";
            depositDeny.terminalOptions = new CompatibleNoun[0] { };

            depositConfirm = ScriptableObject.CreateInstance<TerminalNode>();
            depositConfirm.acceptAnything = false;
            depositConfirm.buyItemIndex = -1;
            depositConfirm.buyRerouteToMoon = -1;
            depositConfirm.buyUnlockable = false;
            depositConfirm.clearPreviousText = false;
            depositConfirm.creatureFileID = -1;
            depositConfirm.creatureName = "";
            depositConfirm.displayPlanetInfo = -1;
            depositConfirm.displayText = TerminalHelper.depositConfirmString;
            depositConfirm.displayTexture = null;
            depositConfirm.displayVideo = null;
            depositConfirm.hideFlags = UnityEngine.HideFlags.None;
            depositConfirm.isConfirmationNode = false;
            depositConfirm.itemCost = 0;
            depositConfirm.loadImageSlowly = false;
            depositConfirm.maxCharactersToType = 35;
            depositConfirm.name = "depositConfirm";
            depositConfirm.overrideOptions = false;
            depositConfirm.persistentImage = false;
            depositConfirm.playClip = null;
            depositConfirm.playSyncedClip = -1;
            depositConfirm.returnFromStorage = false;
            depositConfirm.shipUnlockableID = -1;
            depositConfirm.storyLogFileID = -1;
            depositConfirm.terminalEvent = "";
            depositConfirm.terminalOptions = new CompatibleNoun[0] { };

            depositCommand = ScriptableObject.CreateInstance<TerminalNode>();
            depositCommand.acceptAnything = false;
            depositCommand.buyItemIndex = -1;
            depositCommand.buyRerouteToMoon = -1;
            depositCommand.buyUnlockable = false;
            depositCommand.clearPreviousText = true;
            depositCommand.creatureFileID = -1;
            depositCommand.creatureName = "";
            depositCommand.displayPlanetInfo = -1;
            depositCommand.displayText = TerminalHelper.depositCommandString;
            depositCommand.displayTexture = null;
            depositCommand.displayVideo = null;
            depositCommand.hideFlags = UnityEngine.HideFlags.None;
            depositCommand.isConfirmationNode = true;
            depositCommand.itemCost = 0;
            depositCommand.loadImageSlowly = false;
            depositCommand.maxCharactersToType = 35;
            depositCommand.name = "depositCommand";
            depositCommand.overrideOptions = true;
            depositCommand.persistentImage = false;
            depositCommand.playClip = null;
            depositCommand.playSyncedClip = -1;
            depositCommand.returnFromStorage = false;
            depositCommand.shipUnlockableID = -1;
            depositCommand.storyLogFileID = -1;
            depositCommand.terminalEvent = "";
            depositCommand.terminalOptions = new CompatibleNoun[2]
            {
                new()
                {
                    noun = terminal.terminalNodes.allKeywords[3],
                    result = depositConfirm
                },
                new()
                {
                    noun = terminal.terminalNodes.allKeywords[4],
                    result = depositDeny
                }
            };

            checkScrap = ScriptableObject.CreateInstance<TerminalNode>();
            checkScrap.acceptAnything = false;
            checkScrap.buyItemIndex = -1;
            checkScrap.buyRerouteToMoon = -1;
            checkScrap.buyUnlockable = false;
            checkScrap.clearPreviousText = true;
            checkScrap.creatureFileID = -1;
            checkScrap.creatureName = "";
            checkScrap.displayPlanetInfo = -1;
            checkScrap.displayText = checkScrapString;
            checkScrap.displayTexture = null;
            checkScrap.displayVideo = null;
            checkScrap.hideFlags = UnityEngine.HideFlags.None;
            checkScrap.isConfirmationNode = false;
            checkScrap.itemCost = 0;
            checkScrap.loadImageSlowly = false;
            checkScrap.maxCharactersToType = 35;
            checkScrap.name = "checkScrap";
            checkScrap.overrideOptions = false;
            checkScrap.persistentImage = false;
            checkScrap.playClip = null;
            checkScrap.playSyncedClip = -1;
            checkScrap.returnFromStorage = false;
            checkScrap.shipUnlockableID = -1;
            checkScrap.storyLogFileID = -1;
            checkScrap.terminalEvent = "";
            checkScrap.terminalOptions = new CompatibleNoun[0] { };
        }
    }

    public class TerminalKeywords
    {
        public static TerminalKeyword sellWord = null!;
        public static TerminalKeyword depositWord = null!;
        public static TerminalKeyword overtimeWord = null!;
        public static TerminalKeyword checkScrapWord = null!;
        public static TerminalKeyword defaultVerb = null!;

        public static void InitializeKeywords()
        {
            sellWord = ScriptableObject.CreateInstance<TerminalKeyword>();
            sellWord.accessTerminalObjects = false;
            sellWord.compatibleNouns = new CompatibleNoun[0] { };
            sellWord.defaultVerb = null;
            sellWord.hideFlags = UnityEngine.HideFlags.None;
            sellWord.isVerb = false;
            sellWord.name = "SellWord";
            sellWord.specialKeywordResult = null;
            sellWord.word = "sell";

            depositWord = ScriptableObject.CreateInstance<TerminalKeyword>();
            depositWord.accessTerminalObjects = false;
            depositWord.compatibleNouns = new CompatibleNoun[0] { };
            depositWord.defaultVerb = null;
            depositWord.hideFlags = UnityEngine.HideFlags.None;
            depositWord.isVerb = false;
            depositWord.name = "DepositWord";
            depositWord.specialKeywordResult = null;
            depositWord.word = "deposit";

            overtimeWord = ScriptableObject.CreateInstance<TerminalKeyword>();
            overtimeWord.accessTerminalObjects = false;
            overtimeWord.compatibleNouns = new CompatibleNoun[0] { };
            overtimeWord.defaultVerb = null;
            overtimeWord.hideFlags = UnityEngine.HideFlags.None;
            overtimeWord.isVerb = false;
            overtimeWord.name = "OvertimeWord";
            overtimeWord.specialKeywordResult = null;
            overtimeWord.word = "overtime";

            checkScrapWord = ScriptableObject.CreateInstance<TerminalKeyword>();
            checkScrapWord.accessTerminalObjects = false;
            checkScrapWord.compatibleNouns = new CompatibleNoun[0] { };
            checkScrapWord.defaultVerb = null;
            checkScrapWord.hideFlags = UnityEngine.HideFlags.None;
            checkScrapWord.isVerb = false;
            checkScrapWord.name = "CheckScrapWord";
            checkScrapWord.specialKeywordResult = null;
            checkScrapWord.word = "check";

            defaultVerb = ScriptableObject.CreateInstance<TerminalKeyword>();
            defaultVerb.accessTerminalObjects = false;
            defaultVerb.compatibleNouns = new CompatibleNoun[4]
            {
                new()
                {
                    noun = sellWord,
                    result = TerminalNodes.sellCommand
                },
                new()
                {
                    noun = depositWord,
                    result = TerminalNodes.depositCommand
                },
                new()
                {
                    noun = checkScrapWord,
                    result = TerminalNodes.checkScrap
                },
                new()
                {
                    noun = overtimeWord,
                    result = TerminalNodes.confirmOvertime
                }
            };
            defaultVerb.defaultVerb = null;
            defaultVerb.hideFlags = UnityEngine.HideFlags.None;
            defaultVerb.isVerb = true;
            defaultVerb.name = "DefaultVerb";
            defaultVerb.specialKeywordResult = null;
            defaultVerb.word = "_";
        }
    }

    public static string FindItemsToSell()
    {
        if (StartOfRound.Instance.inShipPhase)
        {
            isSellValid = false;
            return "Cannot sell while in orbit.\n\n";
        }

        if (Object.FindAnyObjectByType<DepositItemsDesk>() == null)
        {
            isSellValid = false;
            return "Current moon does not have a sell counter.\n\n";
        }

        string[] input = terminalInput.Split(new char[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        bool useOvertime = terminalInput.Contains("-o");

        int value;
        if (input.Count() < 2 || !(Int32.TryParse(input[1], out value) || input[1].Equals("all", StringComparison.OrdinalIgnoreCase) || input[1].Equals("quota", StringComparison.OrdinalIgnoreCase)))
        {
            isSellValid = false;
            terminal.currentNode = TerminalNodes.sellDeny;
            return "Invalid sell command.\n\n";
        }

        if (input[1] == "all")
            HQoL.sellModule.FindAllItems();
        else if (input[1] == "quota")
            HQoL.sellModule.FindItemsWithTotalValue(TimeOfDay.Instance.profitQuota, false);
        else
            HQoL.sellModule.FindItemsWithTotalValue(value, useOvertime);

        value = HQoL.sellModule.sellValue;
        string result = $"Found value to sell: ${value}\n";
        int daysLeft = TimeOfDay.Instance.daysUntilDeadline == 0 ? -1 : TimeOfDay.Instance.daysUntilDeadline;
        int overtime = Math.Max(((int)((float)value * StartOfRound.Instance.companyBuyingRate) - TimeOfDay.Instance.profitQuota) / 5 + 15 * daysLeft, 0);
        result += $"Overtime: ${overtime} (Total: ${(int)((float)value * StartOfRound.Instance.companyBuyingRate) + overtime})\n\n";

        foreach (KeyValuePair<string, int> itemCount in HQoL.sellModule.itemTypesToSell)
            result += $"{itemCount.Key} (x{itemCount.Value})\n";

        result += "\nConfirm sell?\n\n";

        isSellValid = true;
        return result;
    }

    public static string SellItems()
    {
        if (!isSellValid)
            return "Items could not be sold.\n\n";

        int creditsEarned = (int)((float)HQoL.sellModule.sellValue * StartOfRound.Instance.companyBuyingRate);
        Network.HQoLNetwork.Instance.SellItemsServerRpc(creditsEarned, HQoL.sellModule.itemReferenceIndexToSell.ToArray());
        HQoL.sellModule.ClearSellModule();

        isSellValid = false;
        return "Selling...\n\n";
    }

    public static string FindItemsToDeposit()
    {
        if (!StartOfRound.Instance.inShipPhase && StartOfRound.Instance.currentLevelID != 3)
        {
            isDepositValid = false;
            return "Can only deposit items while the ship is in orbit or at company.\n\n";
        }

        string input = terminalInput.Replace("deposit ", String.Empty);

        if (input.Length < 1)
        {
            isDepositValid = false;
            return "Deposit command needs an item name.\n\n";
        }

        GrabbableObject[] allItems = Object.FindObjectsByType<GrabbableObject>(FindObjectsSortMode.None);
        if (input == "all")
        {
            Dictionary<string, int> deposit = new();
            foreach (GrabbableObject item in allItems)
            {
                if (!item.itemProperties.isScrap)
                    continue;

                if (deposit.ContainsKey(item.gameObject.GetComponentInChildren<ScanNodeProperties>().headerText))
                    deposit[item.gameObject.GetComponentInChildren<ScanNodeProperties>().headerText]++;
                else
                    deposit[item.gameObject.GetComponentInChildren<ScanNodeProperties>().headerText] = 1;
            }

            string result = "";
            int total = 0;
            foreach (KeyValuePair<string, int> kvp in deposit)
            {
                total += kvp.Value;
                result += $"{kvp.Key} (x{kvp.Value})\n";
            }
            
            if (total == 0)
            {
                isDepositValid = false;
                return $"No items to deposit.\n\n";
            }

            itemNameToDeposit = "all";
            isDepositValid = true;
            return $"Found {total} items to deposit.\n\n" + result + "\nConfirm?\n\n";
        }

        HashSet<string> allDepositdScrapNames = new();
        foreach (GrabbableObject item in allItems)
        {
            if (!item.itemProperties.isScrap)
                continue;

            string allScrapNames = item.name + '#' + item.itemProperties.name + '#' + item.gameObject.GetComponentInChildren<ScanNodeProperties>().headerText;
            allDepositdScrapNames.Add(allScrapNames);
        }

        bool found = false;

        foreach (string itemNames in allDepositdScrapNames)
        {
            if (!found && itemNames.ToLower().Contains(input))
            {
                itemNameToDeposit = itemNames.Split(new char[] { '#' }, StringSplitOptions.RemoveEmptyEntries)[2];
                found = true;
            }
        }

        if (!found)
        {
            isDepositValid = false;
            return $"Could not find {input} to deposit.\n\n";
        }

        int count = 0;
        foreach (GrabbableObject item in allItems)
        {
            if (!item.itemProperties.isScrap)
                continue;

            if (itemNameToDeposit == item.gameObject.GetComponentInChildren<ScanNodeProperties>().headerText && !item.isHeld)
                count++;
        }
        
        isDepositValid = true;
        return $"Found {itemNameToDeposit} (x{count}).\n\nConfirm?\n\n";
    }

    public static string DepositItems()
    {
        if (!isDepositValid)
            return "Items could not be deposited.\n\n";

        Network.HQoLNetwork.Instance.DepositItemsServerRpc(itemNameToDeposit);
        return "Depositing...\n\n";
    }

    public static string ConfirmOvertime()
    {
        int daysLeft = TimeOfDay.Instance.daysUntilDeadline == 0 ? -1 : TimeOfDay.Instance.daysUntilDeadline;
        int overtime = Math.Max((int)(TimeOfDay.Instance.quotaFulfilled - TimeOfDay.Instance.profitQuota) / 5 + 15 * daysLeft, 0);

        string result = $"Value sold: ${TimeOfDay.Instance.quotaFulfilled}\n";
        result += $"Overtime: ${overtime}\n";
        result += $"Quota: ${TimeOfDay.Instance.profitQuota}\n";
        result += $"Total: ${terminal.groupCredits + overtime}\n\n";

        return result;
    }

    public static string CheckItems()
    {
        int total = 0;
        Dictionary<string, int> itemCounter = new();
        Dictionary<string, int> valueCounter = new();
        foreach (ItemReference itemRef in Network.HQoLNetwork.Instance.netStorage)
        {
            if (itemCounter.ContainsKey(itemRef.itemName.ToString()))
            {
                itemCounter[itemRef.itemName.ToString()]++;
                valueCounter[itemRef.itemName.ToString()] += itemRef.value;
            }
            else
            {
                itemCounter[itemRef.itemName.ToString()] = 1;
                valueCounter[itemRef.itemName.ToString()] = itemRef.value;
            }

            total++;
        }

        string result = $"{total} scrap in storage.\n\n";
        foreach (string itemName in itemCounter.Keys)
        {
            result += $"{itemName} (x{itemCounter[itemName]}: ${valueCounter[itemName]})\n";
        }
        result += "\n";

        return result;
    }
}
