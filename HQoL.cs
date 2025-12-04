using System;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace HQoL;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class HQoL : BaseUnityPlugin
{
    public static HQoL Instance { get; private set; } = null!;
    internal new static ManualLogSource Logger { get; private set; } = null!;
    internal static Harmony? Harmony { get; set; }

    internal static Util.SellModule sellModule = null!;
    internal static HQoLConfig modConfig = null!;

    //Netcode multipathing copied from https://github.com/ZehsTeam/Lethal-Company-SellMyScrap
#if LC_VERSION_73
    const string UnityVersion = "2022.3.62";
#elif LC_VERSION_72
    const string UnityVersion = "2022.3.9";
#else
    const string UnityVersion = "2022.3";
#endif

    private void Awake()
    {
        Logger = base.Logger;
        Instance = this;

        if (!Application.unityVersion.StartsWith(UnityVersion, StringComparison.OrdinalIgnoreCase))
        {
            Logger.LogInfo($"Skipping {MyPluginInfo.PLUGIN_GUID}, no patches will be loaded.");
            return;
        }

        sellModule = new();
        modConfig = new(Config);
        Patch();
        NetcodePatch();

        Logger.LogInfo($"{MyPluginInfo.PLUGIN_GUID} v{MyPluginInfo.PLUGIN_VERSION} has loaded!");
    }

    internal static void NetcodePatch()
    {
        var types = Assembly.GetExecutingAssembly().GetTypes();
        foreach (var type in types)
        {
            var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            foreach (var method in methods)
            {
                var attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                if (attributes.Length > 0)
                {
                    try
                    {
                        method.Invoke(null, null);
                    }
                    catch (Exception e)
                    {
                        Logger.LogWarning($"Netcode patch failed, but it's likely intended: {e}");
                    }
                }
            }
        }
    }

    internal static void Patch()
    {
        Harmony ??= new Harmony(MyPluginInfo.PLUGIN_GUID);

        Logger.LogDebug("Patching...");

        Harmony.PatchAll();

        Logger.LogDebug("Finished patching!");
    }

    internal static void Unpatch()
    {
        Logger.LogDebug("Unpatching...");

        Harmony?.UnpatchSelf();

        Logger.LogDebug("Finished unpatching!");
    }
}
