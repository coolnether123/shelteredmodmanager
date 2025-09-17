using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace ModAPI.Core
{
    // Class to hold mod data to be saved/loaded with the game save
    [Serializable]
    public class ModSaveData
    {
        public List<ModInfo> mods = new List<ModInfo>();

        [Serializable]
        public class ModInfo
        {
            public string id;
            public string version;
        }

        public static ModSaveData FromCurrentMods()
        {
            var data = new ModSaveData();
            foreach (var modEntry in PluginManager.LoadedMods)
            {
                data.mods.Add(new ModInfo
                {
                    id = modEntry.Id,
                    version = modEntry.Version
                });
            }
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
        [HarmonyPatch]
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
                }
                catch (Exception ex)
                {
                    MMLog.WriteError("SaveGamePatch: Error injecting mod data: " + ex.Message);
                }
            }
        }

        // Patch for game loading
        [HarmonyPatch]
        internal static class LoadGamePatch
        {
            // Flag to indicate if we are waiting for user confirmation on mod mismatch
            private static FieldInfo _pendingModMismatchResolutionField;

            // Prefix to read mod data and potentially pause loading
            public static bool Prefix(object __instance) // __instance is SaveManager
            {
                // Ensure the flag field is accessible
                if (_pendingModMismatchResolutionField == null)
                {
                    _pendingModMismatchResolutionField = AccessTools.Field(typeof(SaveManager), "m_pendingModMismatchResolution");
                    if (_pendingModMismatchResolutionField == null)
                    {
                        MMLog.WriteError("LoadGamePatch: Could not find m_pendingModMismatchResolution field in SaveManager. Mod mismatch resolution will not pause load.");
                    }
                }

                // If we are already waiting for user input, keep pausing the load
                if (_pendingModMismatchResolutionField != null && (bool)_pendingModMismatchResolutionField.GetValue(__instance))
                {
                    return false; // Skip original method
                }

                // Access the private m_data field from the SaveManager instance
                SaveData data = AccessTools.Field(typeof(SaveManager), "m_data").GetValue(__instance) as SaveData;
                if (data == null) return true; // Continue with original method if SaveData is null

                // Only process for actual game slots, not GlobalData or Invalid
                // m_currentType holds the SaveType being loaded
                SaveManager.SaveType type = (SaveManager.SaveType)AccessTools.Field(typeof(SaveManager), "m_currentType").GetValue(__instance);
                if (type == SaveManager.SaveType.GlobalData || type == SaveManager.SaveType.Invalid) return true; // Continue

                try
                {
                    MMLog.WriteDebug($"LoadGamePatch: Reading mod data for slot {type} from save.");
                    string modDataJson = null;
                    string modDataKey = "ModAPI_ModData";

                    // Use SaveData's SaveLoad method to retrieve our JSON string
                    if (!data.SaveLoad(modDataKey, ref modDataJson))
                    {
                        MMLog.WriteDebug("LoadGamePatch: No mod data found in save. Assuming unmodded save or old format.");
                        return true; // Continue with original method
                    }

                    if (string.IsNullOrEmpty(modDataJson))
                    {
                        MMLog.WriteDebug("LoadGamePatch: Empty mod data found in save.");
                        return true; // Continue with original method
                    }

                    ModSaveData savedModData = JsonUtility.FromJson<ModSaveData>(modDataJson);
                    if (savedModData == null || savedModData.mods == null)
                    {
                        MMLog.WriteWarning("LoadGamePatch: Failed to parse mod data from save.");
                        return true; // Continue with original method
                    }

                    MMLog.WriteDebug($"LoadGamePatch: Saved mod data: {modDataJson}");

                    // Perform comparison and get mismatch message
                    string mismatchMessage = GetMismatchMessage(savedModData.mods);

                    if (!string.IsNullOrEmpty(mismatchMessage))
                    {
                        MMLog.WriteWarning("Mod Save Mismatch Detected! Displaying alert.");
                        
                        Type messageBoxType = AccessTools.TypeByName("MessageBox");
                        if (messageBoxType != null)
                        {
                            Type messageBoxButtonsType = AccessTools.Inner(messageBoxType, "MessageBoxButtons");
                            object yesNoButtons = Enum.Parse(messageBoxButtonsType, "YesNo_Buttons");
                            Type messageBoxResponseType = AccessTools.Inner(messageBoxType, "MessageBoxResponse");
                            MethodInfo callbackMethod = AccessTools.Method(typeof(SaveProtectionPatches.LoadGamePatch), nameof(SaveProtectionPatches.LoadGamePatch.OnMessageBoxResponse));
                            Delegate callbackDelegate = Delegate.CreateDelegate(messageBoxResponseType, callbackMethod);
                            MethodInfo showMethod = AccessTools.Method(messageBoxType, "Show", new Type[] { messageBoxButtonsType, typeof(string), messageBoxResponseType });

                            if (showMethod != null)
                            {
                                if (_pendingModMismatchResolutionField != null)
                                {
                                    _pendingModMismatchResolutionField.SetValue(__instance, true);
                                }
                                showMethod.Invoke(null, new object[] { yesNoButtons, mismatchMessage, callbackDelegate });
                                return false; // Pause loading
                            }
                            else
                            {
                                MMLog.WriteError("LoadGamePatch: MessageBox.Show method not found with expected signature. Save will load automatically.");
                            }
                        }
                        else
                        {
                            MMLog.WriteError("LoadGamePatch: MessageBox type not found. Save will load automatically.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    MMLog.WriteError("LoadGamePatch: Error during mod data comparison or alert display: " + ex.Message);
                }
                return true; // Continue with original method if no mismatch or error
            }

            // Callback for MessageBox response
            public static void OnMessageBoxResponse(int response)
            {
                // Get SaveManager instance
                SaveManager saveManager = SaveManager.instance;
                if (saveManager == null)
                {
                    MMLog.WriteError("LoadGamePatch.OnMessageBoxResponse: SaveManager instance is null.");
                    return;
                }

                // Clear the pending flag
                if (_pendingModMismatchResolutionField != null)
                {
                    _pendingModMismatchResolutionField.SetValue(saveManager, false);
                }

                if (response == 1) // Yes (Load Anyway)
                {
                    MMLog.WriteDebug("LoadGamePatch: User chose to load save despite mismatch.");
                    // Resume loading by setting state and letting Update() continue
                    // This is a bit of a hack, but should re-enter the state machine
                    AccessTools.Method(typeof(SaveManager), "SetState").Invoke(saveManager, new object[] { Enum.Parse(AccessTools.Inner(typeof(SaveManager), "SaveState"), "LoadingData") });
                }
                else // No (Cancel Load)
                {
                    MMLog.WriteDebug("LoadGamePatch: User chose to cancel load due to mismatch.");
                    // Transition to Idle state and go back to main menu
                    AccessTools.Method(typeof(SaveManager), "SetState").Invoke(saveManager, new object[] { Enum.Parse(AccessTools.Inner(typeof(SaveManager), "SaveState"), "Idle") });
                    // Go back to main menu/slot selection
                    if (UIPanelManager.instance != null)
                    {
                        UIPanelManager.instance.PopPanel(AccessTools.Property(typeof(UIPanelManager), "CurrentPanel").GetValue(UIPanelManager.instance, null) as BasePanel); // Pop current panel (likely SlotSelectionPanel)
                        LoadingScreen.Instance.ShowLoadingScreen("MenuScene"); // Go to main menu scene
                    }
                }
            }

            private static string GetMismatchMessage(List<ModSaveData.ModInfo> savedMods)
            {
                var currentMods = PluginManager.LoadedMods.Select(m => new ModSaveData.ModInfo { id = m.Id, version = m.Version }).ToList();

                var missingMods = savedMods.Where(sm => !currentMods.Any(cm => cm.id == sm.id && cm.version == sm.version)).ToList();
                var newMods = currentMods.Where(cm => !savedMods.Any(sm => sm.id == cm.id && sm.version == sm.version)).ToList();

                if (missingMods.Any() || newMods.Any())
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("Mod Save Mismatch Detected!");
                    sb.AppendLine("---------------------------------");
                    if (missingMods.Any())
                    {
                        sb.AppendLine("Missing mods from save:");
                        foreach (var m in missingMods)
                        {
                            sb.AppendLine($"  - {m.id} v{m.version}");
                        }
                    }
                    if (newMods.Any())
                    {
                        if (missingMods.Any()) sb.AppendLine(); // Add a blank line if both sections exist
                        sb.AppendLine("New mods not in save:");
                        foreach (var m in newMods)
                        {
                            sb.AppendLine($"  - {m.id} v{m.version}");
                        }
                    }
                    sb.AppendLine("---------------------------------");
                    sb.AppendLine("Do you want to load this save anyway?");
                    return sb.ToString();
                }
                return null;
            }
        }
    }
}