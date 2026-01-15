using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;
using ModAPI.Saves;
using ModAPI.Hooks.Paging;

namespace ModAPI.Core
{
    // Class to hold mod data to be saved/loaded with the game save
    [Serializable]
    public class ModSaveData
    {
        public ModInfo[] mods;

        [Serializable]
        public class ModInfo
        {
            public string id;
            public string version;
        }

        public static ModSaveData FromCurrentMods()
        {
            var data = new ModSaveData();
            var list = new List<ModInfo>();
            foreach (var modEntry in PluginManager.LoadedMods)
            {
                list.Add(new ModInfo
                {
                    id = modEntry.Id,
                    version = modEntry.Version
                });
            }
            data.mods = list.ToArray();
            return data;
        }
    }

    // Harmony patches for save/load operations
    internal static class SaveProtectionPatches
    {
        private static HarmonyLib.Harmony _harmony;

        public static void ApplyPatches(HarmonyLib.Harmony harmonyInstance)
        {
            _harmony = harmonyInstance;

            Type saveManagerType = AccessTools.TypeByName("SaveManager");
            if (saveManagerType == null)
            {
                MMLog.WriteError("SaveProtectionPatches: SaveManager type not found. Save protection patches skipped.");
                return;
            }

            // Patch for game saving (StartSave)
            try
            {
                _harmony.Patch(AccessTools.Method(saveManagerType, "StartSave", new Type[] { typeof(SaveManager.SaveType) }),
                               postfix: new HarmonyMethod(typeof(SaveProtectionPatches.SaveGamePatch), nameof(SaveProtectionPatches.SaveGamePatch.Postfix)));
                MMLog.WriteDebug("SaveProtectionPatches: Applied StartSave patch.");
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("SaveProtectionPatches.StartSave", "Failed to apply StartSave patch: " + ex.Message);
            }

            // Patch for game loading (Update_LoadData)
            try
            {
                _harmony.Patch(AccessTools.Method(saveManagerType, "Update_LoadData"),
                               prefix: new HarmonyMethod(typeof(SaveProtectionPatches.LoadGamePatch), nameof(SaveProtectionPatches.LoadGamePatch.Prefix)));
                MMLog.WriteDebug("SaveProtectionPatches: Applied Update_LoadData Prefix patch.");
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("SaveProtectionPatches.Update_LoadData", "Failed to apply Update_LoadData Prefix patch: " + ex.Message);
            }
        }

        // Patch for game saving
        // Note: This class uses manual patching in ApplyPatches(), not Harmony attributes
        internal static class SaveGamePatch
        {
            // Postfix to inject mod data after game saves its own data
            public static void Postfix(object __instance, SaveManager.SaveType type) // __instance is SaveManager
            {
                // Only inject for actual game slots, not GlobalData or Invalid
                if (type == SaveManager.SaveType.GlobalData || type == SaveManager.SaveType.Invalid) return;

                try
                {
                    // Access the private m_data field from the SaveManager instance
                    SaveData data = AccessTools.Field(typeof(SaveManager), "m_data").GetValue(__instance) as SaveData;
                    if (data == null) return;

                    MMLog.WriteDebug($"SaveGamePatch: Injecting mod data for slot {type} into save.");
                    ModSaveData modData = ModSaveData.FromCurrentMods();
                    string json = JsonUtility.ToJson(modData);
                    
                    // Use SaveData's SaveLoad method to store our JSON string
                    string modDataKey = "ModAPI_ModData";
                    data.SaveLoad(modDataKey, ref json);
                    MMLog.WriteDebug($"SaveGamePatch: Injected mod data: {json}");

                    // NEW: Update the external manifest.json so the Manager and Restart diagnostics are accurate
                    try
                    {
                        var info = new ModAPI.Saves.SaveInfo
                        {
                            familyName = data.info != null ? data.info.m_familyName : "Unknown",
                            daysSurvived = data.info != null ? data.info.m_daysSurvived : 0,
                            saveTime = data.info != null ? data.info.m_saveTime : DateTime.Now.ToString()
                        };
                        ModAPI.Saves.ExpandedVanillaSaves.UpdateManifest((int)type, info);
                    }
                    catch (Exception ex)
                    {
                        MMLog.WriteError("SaveGamePatch: Failed to update external manifest during StartSave: " + ex.Message);
                    }
                }
                catch (Exception ex)
                {
                    MMLog.WriteError("SaveGamePatch: Error injecting mod data: " + ex.Message);
                }
            }
        }

        // Patch for game loading
        // Note: This class uses manual patching in ApplyPatches(), not Harmony attributes
        internal static class LoadGamePatch
        {
            // State tracking for the load interruption
            private static bool _isWaitingForUser = false;
            internal static bool _forceLoad = false; // Internal so OnSlotChosen can set it

            // Prefix to read mod data and potentially pause loading
            public static bool Prefix(object __instance) // __instance is SaveManager
            {
                // If we are waiting for user input, pause the load
                if (_isWaitingForUser) return false;

                // If user confirmed load, proceed without checks
                if (_forceLoad)
                {
                    _forceLoad = false; // Reset for next time
                    return true;
                }

                // Access the private m_data field from the SaveManager instance
                SaveData data = AccessTools.Field(typeof(SaveManager), "m_data").GetValue(__instance) as SaveData;
                if (data == null) return true; // Continue with original method if SaveData is null

                // Only process for actual game slots
                SaveManager.SaveType type = (SaveManager.SaveType)AccessTools.Field(typeof(SaveManager), "m_currentType").GetValue(__instance);
                if (type == SaveManager.SaveType.GlobalData || type == SaveManager.SaveType.Invalid) return true;

                try
                {
                    MMLog.WriteDebug($"[LoadGamePatch] Checking mod data for slot {type}.");

                    List<ModSaveData.ModInfo> savedMods = null;

                    // 1. Try reading from SaveData (internal save file)
                    string modDataJson = null;
                    string modDataKey = "ModAPI_ModData";
                    if (data.SaveLoad(modDataKey, ref modDataJson) && !string.IsNullOrEmpty(modDataJson))
                    {
                        var parsed = JsonUtility.FromJson<ModSaveData>(modDataJson);
                        if (parsed != null && parsed.mods != null)
                        {
                            savedMods = new List<ModSaveData.ModInfo>(parsed.mods);
                            MMLog.WriteDebug($"[LoadGamePatch] Found embedded mod data. Count: {savedMods.Count}");
                        }
                    }

                    // 2. Fallback: Try reading from external manifest.json
                    if (savedMods == null)
                    {
                        try
                        {
                            // Convert SaveType to slot index
                            int slotIndex = (int)type; 
                             
                            var manifest = ModAPI.Saves.SaveRegistryCore.ReadSlotManifest("Standard", slotIndex);
                            if (manifest != null && manifest.lastLoadedMods != null)
                            {
                                savedMods = manifest.lastLoadedMods.Select(m => new ModSaveData.ModInfo { id = m.modId, version = m.version }).ToList();
                                MMLog.WriteDebug($"[LoadGamePatch] Found external manifest data. Count: {savedMods.Count}");
                            }
                        }
                        catch (Exception ex)
                        {
                            MMLog.WriteDebug("[LoadGamePatch] Failed to check external manifest: " + ex.Message);
                        }
                    }

                    // If still no data, imply unmodded save.
                    if (savedMods == null)
                    {
                        MMLog.WriteDebug("[LoadGamePatch] No mod data found anywhere. Assuming clean/vanilla save.");
                        return true; 
                    }

                    // 3. Compare with active mods
                    var manifestForUI = new ModAPI.Saves.SlotManifest 
                    { 
                        lastLoadedMods = savedMods.Select(m => new ModAPI.Saves.LoadedModInfo { modId = m.id, version = m.version }).ToArray() 
                    };

                    var currentState = SaveVerification.Verify(manifestForUI);

                    if (currentState != SaveVerification.VerificationState.Match)
                    {
                        MMLog.WriteWarning($"[LoadGamePatch] Mismatch detected ({currentState}) for slot {type}. Pausing load to show UI.");
                        
                        _isWaitingForUser = true;

                        // Hide loading screen immediately (vanilla code may have already shown it)
                        try
                        {
                            if (LoadingScreen.Instance != null && LoadingScreen.Instance.gameObject.activeInHierarchy)
                            {
                                LoadingScreen.Instance.gameObject.SetActive(false);
                                MMLog.WriteDebug("[LoadGamePatch] Hid LoadingScreen before showing details dialog.");
                            }
                        }
                        catch (Exception ex) { MMLog.WriteError("Error pre-hiding loading screen: " + ex); }

                        // Create dummy entry for UI (needs family name)
                        var entry = new ModAPI.Saves.SaveEntry
                        {
                            id = "temp_load_entry",
                            absoluteSlot = (int)type,
                            saveInfo = new ModAPI.Saves.SaveInfo 
                            { 
                                familyName = data.info != null ? data.info.m_familyName : "Unknown",
                                daysSurvived = data.info != null ? data.info.m_daysSurvived : 0,
                                saveTime = data.info != null ? data.info.m_saveTime : DateTime.Now.ToString()
                            }
                        };

                        // Open UI
                        ModAPI.Hooks.Paging.SaveDetailsWindow.Show(entry, manifestForUI, currentState, true, 
                        () => 
                        {
                            MMLog.WriteDebug("[LoadGamePatch] User accepted load.");
                            _isWaitingForUser = false;
                            _forceLoad = true;
                        },
                        () =>
                        {
                            MMLog.WriteDebug("[LoadGamePatch] User cancelled load.");
                            _isWaitingForUser = false;
                            
                            // Reset state to Idle using Reflection
                            try 
                            {
                                var saveStateEnum = typeof(SaveManager).GetNestedType("SaveState", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                                if (saveStateEnum != null)
                                {
                                    var idleState = Enum.Parse(saveStateEnum, "Idle");
                                    var setStateMethod = AccessTools.Method(typeof(SaveManager), "SetState");
                                    if (setStateMethod != null)
                                    {
                                        setStateMethod.Invoke(SaveManager.instance, new object[] { idleState });
                                    }
                                }
                            }
                            catch (Exception ex) { MMLog.WriteError("Error resetting SaveManager state: " + ex); }

                            // Hide Loading Screen / Spinner
                            try
                            {
                                if (LoadingScreen.Instance != null && LoadingScreen.Instance.gameObject.activeInHierarchy)
                                {
                                    LoadingScreen.Instance.gameObject.SetActive(false);
                                    MMLog.WriteDebug("[LoadGamePatch] Disabled LoadingScreen instance.");
                                }
                            }
                            catch (Exception ex) { MMLog.WriteError("Error hiding loading screen: " + ex); }
                        });

                        return false; // Pause loading
                    }
                }
                catch (Exception ex)
                {
                    MMLog.WriteError("LoadGamePatch: Error during verification: " + ex.ToString());
                }

                return true; // Continue with original method
            }
        }
    }
}