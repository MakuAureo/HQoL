using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;

namespace HQoL;

public class HQoLConfig
{
    public HashSet<string> storageException;
    public readonly ConfigEntry<string> storageExceptionConfig;
    public bool sortLoot;
    public readonly ConfigEntry<bool> sortLootConfig;

    public HQoLConfig(ConfigFile cfg)
    {
        cfg.SaveOnConfigSet = false;
        
        storageExceptionConfig = cfg.Bind(
                "General",
                "Dont store list",
                "Shotgun, Knife",
                "What items should not be stored automatically"
                );

        sortLootConfig = cfg.Bind<bool>(
                "General",
                "Allow automatic loot sorting",
                true,
                "If loot can be automatically sorted"
                );

        
        ClearOrphanedEntries(cfg); 
        cfg.Save(); 
        cfg.SaveOnConfigSet = true; 

        storageException = new(storageExceptionConfig.Value.Split(new char[] {','}, System.StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim().ToLower()));
        sortLoot = sortLootConfig.Value;
    }

    static void ClearOrphanedEntries(ConfigFile cfg) 
    { 
        PropertyInfo orphanedEntriesProp = AccessTools.Property(typeof(ConfigFile), "OrphanedEntries"); 
        var orphanedEntries = (Dictionary<ConfigDefinition, string>)orphanedEntriesProp.GetValue(cfg); 
        orphanedEntries.Clear(); 
    }
}
