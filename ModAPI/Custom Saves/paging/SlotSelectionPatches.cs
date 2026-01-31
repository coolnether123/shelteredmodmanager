using System;
using System.Collections.Generic;
using HarmonyLib;
using ModAPI.Saves;
using UnityEngine;
using ModAPI.Hooks.Paging;
using ModAPI.Core;

namespace ModAPI.Hooks
{
    /// <summary>
    /// This static class holds state that must persist across scene loads,
    /// specifically the intended "slot in use" for the SaveManager.
    /// </summary>
    internal static class SaveManagerContext
    {
        public static SaveManager.SaveType IntendedSlotInUse = SaveManager.SaveType.Invalid;
    }

    [HarmonyPatch(typeof(SlotSelectionPanel), "OnShow")]
    internal static class SlotSelectionPanel_OnShow_Patch
    {
        static void Postfix(SlotSelectionPanel __instance)
        {
            PagingManager.Initialize(__instance);
        }
    }

    [HarmonyPatch(typeof(SlotSelectionPanel), "RefreshSaveSlotInfo")]
    internal static class SlotSelectionPanel_RefreshSaveSlotInfo_Patch
    {
        static bool Prefix(SlotSelectionPanel __instance)
        {
            int page = PagingManager.GetPage(__instance);
            if (page == 0)
            {
                var vanillaButtons = __instance.GetComponentsInChildren<SaveSlotButton>(true);
                foreach (var btn in vanillaButtons)
                {
                    if (btn != null && (btn.slotNumber == 3 || btn.slotNumber == 4))
                    {
                        if (!btn.gameObject.activeSelf) btn.gameObject.SetActive(true);
                    }
                }
                
                // On page 0, we just ensure vanilla slots 3 & 4 are active, hide verification icons, and let vanilla run.
                // Cleanup is done in the Postfix below.

                // Hide verification icons when on vanilla page
                SaveVerification.UpdateIcons(__instance);
                
                return true;
            }

            try
            {
                int apiPage = page - 1;
                // Get all saves for this scenario, not just one page.
                var allSaves = ExpandedVanillaSaves.List();
                var savesOnPage = new SaveEntry[3];
                
                // Distribute the saves into the correct slots for the current page.
                foreach(var save in allSaves)
                {
                    int saveSlot = save.absoluteSlot;
                    if (saveSlot > 0)
                    {
                        int saveApiPage = (saveSlot - 4) / 3;
                        if (saveApiPage == apiPage)
                        {
                            int slotIndexOnPage = (saveSlot - 4) % 3;
                            if(slotIndexOnPage >= 0 && slotIndexOnPage < 3)
                            {
                                savesOnPage[slotIndexOnPage] = save;
                            }
                        }
                    }
                }

                var t = Traverse.Create(__instance);
                var m_slotInfo = t.Field("m_slotInfo").GetValue<System.Collections.IList>();

                var buttons = __instance.GetComponentsInChildren<SaveSlotButton>(true);
                foreach (var btn in buttons)
                {
                    if (btn != null && (btn.slotNumber == 3 || btn.slotNumber == 4))
                    {
                        btn.gameObject.SetActive(false);
                    }
                }

                for (int i = 0; i < 3; i++)
                {
                    var slotInfo = m_slotInfo[i];
                    var entry = (i < savesOnPage.Length) ? savesOnPage[i] : null;

                    if (entry != null)
                    {
                        var tSlot = Traverse.Create(slotInfo);
                        MMLog.WriteDebug($"[RefreshSaveSlotInfo] Setting Slot {entry.absoluteSlot} to LOADED. Family='{entry.saveInfo.familyName}'");
                        tSlot.Field("m_state").SetValue(SlotSelectionPanel.SlotState.Loaded);
                        tSlot.Field("m_familyName").SetValue(entry.saveInfo.familyName);
                        tSlot.Field("m_daysSurvived").SetValue(entry.saveInfo.daysSurvived);
                        tSlot.Field("m_diffSetting").SetValue(entry.saveInfo.difficulty);
                        
                        // Set all difficulty fields for proper DifficultyManager loading
                        tSlot.Field("m_rainDiff").SetValue(entry.saveInfo.rainDiff);
                        tSlot.Field("m_resourceDiff").SetValue(entry.saveInfo.resourceDiff);
                        tSlot.Field("m_breachDiff").SetValue(entry.saveInfo.breachDiff);
                        tSlot.Field("m_factionDiff").SetValue(entry.saveInfo.factionDiff);
                        tSlot.Field("m_moodDiff").SetValue(entry.saveInfo.moodDiff);
                        tSlot.Field("m_mapSize").SetValue(entry.saveInfo.mapSize);
                        tSlot.Field("m_fog").SetValue(entry.saveInfo.fog);
                        
                        var rawTime = entry.saveInfo.saveTime ?? entry.updatedAt;
                        string displayTime = rawTime;
                        try
                        {
                            if (DateTime.TryParse(rawTime, out var dt))
                                displayTime = dt.ToLocalTime().ToString("g");
                        }
                        catch { }

                        if (tSlot.Field("m_dateSaved").FieldExists()) tSlot.Field("m_dateSaved").SetValue(displayTime);
                        if (tSlot.Field("m_saveTime").FieldExists()) tSlot.Field("m_saveTime").SetValue(displayTime);
                    }
                    else
                    {
                        MMLog.WriteDebug($"[RefreshSaveSlotInfo] Setting physical slot {i+1} on page {page} to EMPTY");
                        Traverse.Create(slotInfo).Field("m_state").SetValue(SlotSelectionPanel.SlotState.Empty);
                    }
                }

                t.Method("RefreshSlotLabels").GetValue();
                
                // Update Verification Icons
                SaveVerification.UpdateIcons(__instance);
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[RefreshSaveSlotInfo PREFIX] Error during takeover: " + ex);
            }

            return false;
        }

        /// <summary>
        /// Postfix runs after vanilla RefreshSaveSlotInfo on Page 0 to clean ghost data and refresh labels.
        /// </summary>
        static void Postfix(SlotSelectionPanel __instance)
        {
            int page = PagingManager.GetPage(__instance);
            if (page != 0) return;

            try
            {
                var t = Traverse.Create(__instance);
                var slotInfos = t.Field("m_slotInfo").GetValue<System.Collections.IList>();

                // Clear ghost data from empty slots
                if (slotInfos != null)
                {
                    for (int i = 0; i < 3 && i < slotInfos.Count; i++)
                    {
                        var info = slotInfos[i];
                        if (info == null) continue;

                        var tSlot = Traverse.Create(info);
                        var state = tSlot.Field("m_state").GetValue<SlotSelectionPanel.SlotState>();

                        if (state == SlotSelectionPanel.SlotState.Empty)
                        {
                            tSlot.Field("m_familyName").SetValue("");
                            tSlot.Field("m_daysSurvived").SetValue(0);
                            if (tSlot.Field("m_dateSaved").FieldExists()) tSlot.Field("m_dateSaved").SetValue("");
                            if (tSlot.Field("m_saveTime").FieldExists()) tSlot.Field("m_saveTime").SetValue("");
                        }
                    }
                }

                // Let vanilla format the labels correctly
                t.Method("RefreshSlotLabels").GetValue();
            }
            catch (Exception ex)
            {
                MMLog.WriteError($"[RefreshSaveSlotInfo Postfix] Error during Page 0 cleanup: {ex.Message}");
            }
        }

        /// <summary>
        /// Helper to replace the first digit sequence in a string (e.g., "Slot 4" -> "Slot 1")
        /// </summary>
        private static string ReplaceSlotNumber(string text, int newNumber)
        {
            int start = -1, len = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsDigit(text[i])) { start = i; break; }
            }
            if (start < 0) return text;
            for (int i = start; i < text.Length && char.IsDigit(text[i]); i++) len++;
            return text.Substring(0, start) + newNumber.ToString() + text.Substring(start + len);
        }
    }

    [HarmonyPatch(typeof(SlotSelectionPanel), "RefreshSlotLabels")]
    internal static class SlotSelectionPanel_RefreshSlotLabels_Patch
    {
        static void Postfix(SlotSelectionPanel __instance)
        {
            int page = PagingManager.GetPage(__instance);
            if (page <= 0) return;

            try
            {
                int offset = (page - 1) * 3;
                var t = Traverse.Create(__instance);
                var labels = t.Field("m_slotButtonLabels").GetValue<System.Collections.IList>();
                if (labels == null) return;

                for (int i = 0; i < labels.Count && i < 3; i++)
                {
                    var lab = labels[i] as UILabel;
                    if (lab == null || string.IsNullOrEmpty(lab.text)) continue;

                    string updated = ReplaceFirstNumber(lab.text, (i + 4 + offset).ToString());
                    if (updated != lab.text) lab.text = updated;
                }
            }
            catch (Exception ex)
            {
                MMLog.WriteError("RefreshSlotLabels Postfix patch error: " + ex.Message);
            }
        }

        private static string ReplaceFirstNumber(string s, string replacement)
        {
            int start = -1, len = 0;
            for (int i = 0; i < s.Length; i++)
            {
                if (char.IsDigit(s[i])) { start = i; break; }
            }
            if (start < 0) return s;
            for (int i = start; i < s.Length && char.IsDigit(s[i]); i++) len++;
            return s.Substring(0, start) + replacement + s.Substring(start + len);
        }
    }

    [HarmonyPatch(typeof(SlotSelectionPanel), "OnSlotChosen")]
    internal static class SlotSelectionPanel_OnSlotChosen_Patch
    {
        static bool Prefix(SlotSelectionPanel __instance)
        {
            {
                int page = PagingManager.GetPage(__instance);
                
                // For vanilla saves (page 0), check for mod mismatches BEFORE allowing vanilla load
                    // For vanilla saves (page 0), check for mod mismatches BEFORE allowing vanilla load
                if (page == 0)
                {
                    try
                    {
                        var t = Traverse.Create(__instance);
                        int chosenSlotIndex = t.Field("m_selectedSlot").GetValue<int>();
                        
                        // Only check slots 1-3 (indices 0-2)
                        if (chosenSlotIndex >= 0 && chosenSlotIndex < 3)
                        {
                            var vanillaSaveInfo = ModAPI.Saves.SaveRegistryCore.ReadVanillaSaveInfo(chosenSlotIndex + 1);
                            var slotRoot = DirectoryProvider.SlotRoot("Standard", chosenSlotIndex + 1, false);
                            var manPath = System.IO.Path.Combine(slotRoot, "manifest.json");

                            // AUTO-FIX: If vanilla save is missing (vanillaSaveInfo == null) but manifest exists,
                            // it means we have an orphaned manifest from a previous deletion. Clean it up!
                            if (vanillaSaveInfo == null && System.IO.File.Exists(manPath))
                            {
                                MMLog.Write($"[OnSlotChosen] Found orphaned manifest for empty vanilla slot {chosenSlotIndex + 1}. Auto-cleaning.");
                                try { ExpandedVanillaSaves.DeleteBySlot(chosenSlotIndex + 1); } catch {}
                                return true; // Treat as empty slot -> New Game
                            }

                            SlotManifest manifest = null;
                            
                            if (System.IO.File.Exists(manPath))
                            {
                                try 
                                { 
                                    manifest = ModAPI.Saves.SaveRegistryCore.DeserializeSlotManifest(System.IO.File.ReadAllText(manPath)); 
                                } 
                                catch { }
                                
                                if (manifest != null)
                                {
                                    var state = SaveVerification.Verify(manifest);
                                    
                                    if (state != SaveVerification.VerificationState.Match)
                                    {
                                        // Mismatch detected - show dialog WITHOUT starting vanilla load
                                        var entry = new ModAPI.Saves.SaveEntry
                                        {
                                            id = $"vanilla_slot_{chosenSlotIndex + 1}",
                                            absoluteSlot = chosenSlotIndex + 1,
                                            saveInfo = new ModAPI.Saves.SaveInfo
                                            {
                                                familyName = vanillaSaveInfo != null ? vanillaSaveInfo.familyName : manifest.family_name ?? "Unknown",
                                                daysSurvived = vanillaSaveInfo != null ? vanillaSaveInfo.daysSurvived : 0,
                                                saveTime = vanillaSaveInfo != null ? vanillaSaveInfo.saveTime : System.DateTime.Now.ToString()
                                            }
                                        };
                                        
                                        var virtualSaveType = (SaveManager.SaveType)(chosenSlotIndex + 1);
                                        
                                        SaveDetailsWindow.Show(entry, manifest, state, true, () => {
                                            // Load Anyway callback - trigger vanilla load
                                            // Set force load flag to bypass LoadGamePatch check
                                            ModAPI.Core.SaveProtectionPatches.LoadGamePatch._forceLoad = true;
                                            SaveManager.instance.SetSlotToLoad(chosenSlotIndex + 1);
                                        });
                                        
                                        return false; // Block vanilla load until user decides
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MMLog.WriteError($"[OnSlotChosen vanilla check] Error: {ex}");
                    }
                    
                    // No mismatch or error - allow vanilla behavior
                    return true;
                }

                try
                {
                    if (!__instance.m_inputEnabled || (SaveManager.instance != null && SaveManager.instance.isDeleting))
                        return false;

                    var t = Traverse.Create(__instance);
                    // m_chosenSlot is unreliable (-1 for new games). m_selectedSlot seems to hold the actual UI index.
                    int chosenSlotIndex = t.Field("m_selectedSlot").GetValue<int>();
                    if (chosenSlotIndex > 2) return true; // Should not happen on paged view

                    int apiPage = page - 1;

                    // Pass the absolute page number (1 for first custom page)
                    var entry = ExpandedVanillaSaves.FindByUIPosition(chosenSlotIndex + 1, page, 3);
                    
                    var virtualSaveType = (SaveManager.SaveType)(chosenSlotIndex + 1);

                    if (entry == null) // Creating a new game
                    {
                        // page 1 -> slots 4, 5, 6
                        int absoluteSlot = (page - 1) * 3 + chosenSlotIndex + 4;
                        MMLog.Write($"--- Player clicked slot {absoluteSlot} to start a new game ---");

                        var created = ExpandedVanillaSaves.Create(new SaveCreateOptions { name = "New Game", absoluteSlot = absoluteSlot });
                        if (created != null)
                        {
                            PlatformSaveProxy.SetNextSave(virtualSaveType, "Standard", created.id);
                            SaveManager.instance.SetCurrentSlot(chosenSlotIndex + 1);
                        }
                        return true;
                    }
                    else // Loading an existing expanded game
                    {
                        // VERIFICATION
                        var slotRoot = DirectoryProvider.SlotRoot("Standard", entry.absoluteSlot, false);
                        var manPath = System.IO.Path.Combine(slotRoot, "manifest.json");
                        SlotManifest manifest = null;
                        if (System.IO.File.Exists(manPath)) try { manifest = ModAPI.Saves.SaveRegistryCore.DeserializeSlotManifest(System.IO.File.ReadAllText(manPath)); } catch {}
                        
                        var state = SaveVerification.Verify(manifest);

                        // If NOT mismatch, we load normally. 
                        // User requirement: "If the player clicks to play the slot... if the mods don’t match the context window should open... saying load anyway."
                        // Logic: Green (Match) -> Load. Yellow (Ver Diff) is mismatch?
                        // User says: "Green: ID matches AND Version matches... ID matches BUT Version is different (Load Anyway, but warn)... ID missing entirely (Red)"
                        // So only GREEN allows direct load?
                        // "Reload game with... button should be greged out ... if the only diff is version number"
                        
                        if (state != SaveVerification.VerificationState.Match)
                        {
                             // Open Details Window
                             SaveDetailsWindow.Show(entry, manifest, state, true, () => {
                                 // Load Callback
                                 PlatformSaveProxy.SetNextLoad(virtualSaveType, "Standard", entry.id);
                                 
                                 // Transfer difficulty settings just like vanilla does
                                 DifficultyManager.StoreMenuDifficultySettings(
                                     entry.saveInfo.rainDiff, 
                                     entry.saveInfo.resourceDiff, 
                                     entry.saveInfo.breachDiff, 
                                     entry.saveInfo.factionDiff, 
                                     entry.saveInfo.moodDiff, 
                                     entry.saveInfo.mapSize, 
                                     entry.saveInfo.fog);

                                  // Show loader if available
                                  var loadingGraphicLamba = t.Field("m_loadingGraphic").GetValue<GameObject>();
                                  if (loadingGraphicLamba != null) loadingGraphicLamba.SetActive(true);

                                 SaveManager.instance.SetSlotToLoad(chosenSlotIndex + 1);
                             });
                             return false; // Block immediate load
                        }

                        PlatformSaveProxy.SetNextLoad(virtualSaveType, "Standard", entry.id);
                        
                        // Transfer difficulty settings just like vanilla does
                        DifficultyManager.StoreMenuDifficultySettings(
                            entry.saveInfo.rainDiff, 
                            entry.saveInfo.resourceDiff, 
                            entry.saveInfo.breachDiff, 
                            entry.saveInfo.factionDiff, 
                            entry.saveInfo.moodDiff, 
                            entry.saveInfo.mapSize, 
                            entry.saveInfo.fog);

                        // Show loader if available
                        var loadingGraphicObj = t.Field("m_loadingGraphic").GetValue<GameObject>();
                        if (loadingGraphicObj != null) loadingGraphicObj.SetActive(true);

                        SaveManager.instance.SetSlotToLoad(chosenSlotIndex + 1);
                        return false; // STOP vanilla logic to prevent double-firing load
                    }
                }
                catch (Exception ex)
                {
                    MMLog.WriteError("OnSlotChosen Prefix patch error: " + ex);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(SlotSelectionPanel), "OnDeleteMessageBox")]
        internal static class SlotSelectionPanel_OnDeleteMessageBox_Patch
        {
            static bool Prefix(SlotSelectionPanel __instance, int response)
            {
                int page = PagingManager.GetPage(__instance);
                
                // If on vanilla page (0), we still need to intercept confirmed deletes (response == 1)
                // to clean up the 'Slot_X' directory which holds Mod Manifests.
                // If we don't do this, the vanilla save is deleted but the Folder+Manifest remains,
                // causing 'Save Corrupted' warnings on empty slots.
                if (page == 0)
                {
                    if (response == 1)
                    {
                        try
                        {
                            var t = Traverse.Create(__instance);
                            int selectedSlotIndex = t.Field("m_selectedSlot").GetValue<int>(); // 0-2 (Vanilla slots)
                            int absoluteSlot = selectedSlotIndex + 1;
                            
                            MMLog.Write($"[OnDeleteMessageBox] Detected vanilla save deletion for Slot {absoluteSlot}. Cleaning up metadata...");
                            
                            // Delete the Slot_X folder managed by ModAPI
                            ExpandedVanillaSaves.DeleteBySlot(absoluteSlot);
                        }
                        catch (Exception ex)
                        {
                            MMLog.WriteError($"[OnDeleteMessageBox] Error cleaning up vanilla slot metadata: {ex}");
                        }
                    }
                    return true; // ALWAYS return true to let Vanilla logic proceed (deleting actual save file)
                }

                if (response != 1) return false;

                try
                {
                    var t = Traverse.Create(__instance);
                    int selectedSlotIndex = t.Field("m_selectedSlot").GetValue<int>();
                    if (selectedSlotIndex > 2) return true;

                    // We pass mustExist: false here so we can find the entry even if SaveData.xml is missing (for corruption cleanup)
                    var entry = ExpandedVanillaSaves.FindByUIPosition(selectedSlotIndex + 1, page, 3, false);

                    if (entry != null)
                    {
                        MMLog.Write($"[OnDeleteMessageBox] Deleting custom slot {entry.absoluteSlot}...");
                        ExpandedVanillaSaves.DeleteBySlot(entry.absoluteSlot);
                        t.Field("m_infoNeedsRefresh").SetValue(true);
                    }
                    else
                    {
                        MMLog.WriteWarning($"[OnDeleteMessageBox] Could not find entry to delete at physical slot {selectedSlotIndex + 1} page {page}");
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    MMLog.WriteError("OnDeleteMessageBox Prefix patch error: " + ex);
                    return true; // Fallback to vanilla if something catastrophic happens
                }
            }
        }

        [HarmonyPatch(typeof(SlotSelectionPanel), "Update")]
        internal static class SlotSelectionPanel_Update_Patch
        {
            static void Postfix(SlotSelectionPanel __instance)
            {
                if (Input.GetKeyDown(KeyCode.RightArrow))
                {
                    PagingManager.ChangePage(__instance, 1);
                }
                else if (Input.GetKeyDown(KeyCode.LeftArrow))
                {
                    PagingManager.ChangePage(__instance, -1);
                }
            }
        }

        [HarmonyPatch(typeof(SlotSelectionPanel), "Start")]
        internal static class SlotSelectionPanel_Start_Patch
        {
            static void Postfix()
            {
            }
        }

    }
}