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
            // Log custom saves
            ExpandedVanillaSaves.Debug_ListAllSaves();

            // Log vanilla saves
            MMLog.Write("--- Debug Listing Vanilla Saves ---");
            try
            {
                var t = Traverse.Create(__instance);
                var m_slotInfo = t.Field("m_slotInfo").GetValue<System.Collections.IList>();
                for (int i = 0; i < 3; i++)
                {
                    var slotInfo = m_slotInfo[i];
                    var state = Traverse.Create(slotInfo).Field("m_state").GetValue<SlotSelectionPanel.SlotState>();
                    if (state == SlotSelectionPanel.SlotState.Loaded)
                    {
                        var familyName = Traverse.Create(slotInfo).Field("m_familyName").GetValue<string>();
                        var daysSurvived = Traverse.Create(slotInfo).Field("m_daysSurvived").GetValue<int>();
                        var dateSaved = Traverse.Create(slotInfo).Field("m_dateSaved").GetValue<string>();
                        MMLog.Write($"  - Slot: {i + 1}, State: {state}, Family: '{familyName}', Days: {daysSurvived}, Updated: {dateSaved}");
                    }
                    else
                    {
                        MMLog.Write($"  - Slot: {i + 1}, State: {state}");
                    }
                }
            }
            catch (Exception ex)
            {
                MMLog.WriteError("Error during vanilla save debug logging: " + ex);
            }
            MMLog.Write("--- End of List ---");

            PagingManager.Initialize(__instance);
            MMLog.WriteDebug($"[SlotSelectionPanel_OnShow_Patch] Panel shown. PagingManager initialized for instance: {__instance.name}");
        }
    }

    [HarmonyPatch(typeof(SlotSelectionPanel), "RefreshSaveSlotInfo")]
    internal static class SlotSelectionPanel_RefreshSaveSlotInfo_Patch
    {
        static bool Prefix(SlotSelectionPanel __instance)
        {
            int page = PagingManager.GetPage(__instance);
            MMLog.WriteDebug($"[RefreshSaveSlotInfo] Entering Prefix. Current page: {page}");
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

                MMLog.WriteDebug($"[RefreshSaveSlotInfo] apiPage: {apiPage}, found {savesOnPage.Length} saves for this page.");

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

                    MMLog.WriteDebug($"[RefreshSaveSlotInfo] Processing slot {i}. Entry: {(entry != null ? entry.id : "NULL")}");

                    if (entry != null)
                    {
                        Traverse.Create(slotInfo).Field("m_state").SetValue(SlotSelectionPanel.SlotState.Loaded);
                        Traverse.Create(slotInfo).Field("m_familyName").SetValue(entry.saveInfo.familyName);
                        Traverse.Create(slotInfo).Field("m_daysSurvived").SetValue(entry.saveInfo.daysSurvived);
                        Traverse.Create(slotInfo).Field("m_diffSetting").SetValue(entry.saveInfo.difficulty);
                        var rawTime = entry.saveInfo.saveTime ?? entry.updatedAt;
                        string displayTime = rawTime;
                        try
                        {
                            if (DateTime.TryParse(rawTime, out var dt))
                                displayTime = dt.ToLocalTime().ToString("g");
                        }
                        catch { }

                        var tSlot = Traverse.Create(slotInfo);
                        if (tSlot.Field("m_dateSaved").FieldExists()) tSlot.Field("m_dateSaved").SetValue(displayTime);
                        if (tSlot.Field("m_saveTime").FieldExists()) tSlot.Field("m_saveTime").SetValue(displayTime);

                        MMLog.WriteDebug($"[RefreshSaveSlotInfo] Slot {i} state: Loaded. Family: {entry.saveInfo.familyName}, Days: {entry.saveInfo.daysSurvived}, Time: {displayTime}");
                    }
                    else
                    {
                        Traverse.Create(slotInfo).Field("m_state").SetValue(SlotSelectionPanel.SlotState.Empty);
                        MMLog.WriteDebug($"[RefreshSaveSlotInfo] Slot {i} state: Empty.");
                    }
                }

                t.Method("RefreshSlotLabels").GetValue();
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[RefreshSaveSlotInfo PREFIX] Error during takeover: " + ex);
            }

            return false;
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
                if (page == 0) return true;

                try
                {
                    var t = Traverse.Create(__instance);
                    // m_chosenSlot is unreliable (-1 for new games). m_selectedSlot seems to hold the actual UI index.
                    int chosenSlotIndex = t.Field("m_selectedSlot").GetValue<int>();
                    if (chosenSlotIndex > 2) return true; // Should not happen on paged view

                    int apiPage = page - 1;
                    MMLog.WriteDebug($"[SlotSelectionPanel_OnSlotChosen_Patch] chosenSlotIndex: {chosenSlotIndex}, apiPage: {apiPage}");

                    var allSaves = ExpandedVanillaSaves.List();
                    var savesOnPage = new SaveEntry[3];
                    foreach (var save in allSaves)
                    {
                        int saveSlot = save.absoluteSlot;
                        if (saveSlot > 0)
                        {
                            int saveApiPage = (saveSlot - 4) / 3;
                            if (saveApiPage == apiPage)
                            {
                                int slotIndexOnPage = (saveSlot - 4) % 3;
                                if (slotIndexOnPage >= 0 && slotIndexOnPage < 3)
                                {
                                    savesOnPage[slotIndexOnPage] = save;
                                }
                            }
                        }
                    }
                    var entry = (chosenSlotIndex < savesOnPage.Length) ? savesOnPage[chosenSlotIndex] : null;

                    var virtualSaveType = (SaveManager.SaveType)(chosenSlotIndex + 1);
                    MMLog.WriteDebug($"[SlotSelectionPanel_OnSlotChosen_Patch] virtualSaveType: {virtualSaveType}");

                    if (entry == null) // Creating a new game
                    {
                        int absoluteSlot = (apiPage * 3) + chosenSlotIndex + 4;
                        MMLog.Write($"--- Player clicked slot {absoluteSlot} to start a new game ---");

                        var created = ExpandedVanillaSaves.Create(new SaveCreateOptions { name = "New Game", absoluteSlot = absoluteSlot });
                        if (created != null)
                        {
                            MMLog.WriteDebug($"[SlotSelectionPanel_OnSlotChosen_Patch] Calling SetNextSave with type={virtualSaveType}, scenarioId=Standard, saveId={created.id}");
                            PlatformSaveProxy.SetNextSave(virtualSaveType, "Standard", created.id);
                            SaveManager.instance.SetCurrentSlot(chosenSlotIndex + 1);
                        }
                        return true;
                    }
                    else // Loading an existing expanded game
                    {
                        MMLog.WriteDebug($"[SlotSelectionPanel_OnSlotChosen_Patch] Loading existing game with id: {entry.id}");
                        MMLog.WriteDebug($"[SlotSelectionPanel_OnSlotChosen_Patch] Calling SetNextLoad with type={virtualSaveType}, scenarioId=Standard, saveId={entry.id}");
                        PlatformSaveProxy.SetNextLoad(virtualSaveType, "Standard", entry.id);
                        SaveManager.instance.SetSlotToLoad(chosenSlotIndex + 1); // Fix typo: SaveManager.instance
                        return true;
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
                if (page == 0) return true;

                if (response != 1) return false;

                try
                {
                    var t = Traverse.Create(__instance);
                    int selectedSlotIndex = t.Field("m_selectedSlot").GetValue<int>();
                    if (selectedSlotIndex > 2) return true;

                    var entry = ExpandedVanillaSaves.FindByUIPosition(selectedSlotIndex + 1, page - 1, 3);

                    if (entry != null)
                    {
                        ExpandedVanillaSaves.Delete(entry.id);
                        t.Field("m_infoNeedsRefresh").SetValue(true);
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    MMLog.WriteError("OnDeleteMessageBox Prefix patch error: " + ex);
                    return true;
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
                MMLog.WriteDebug("SlotSelectionPanel.Start postfix fired, ModAPI patches are active.");
            }
        }

    }
}