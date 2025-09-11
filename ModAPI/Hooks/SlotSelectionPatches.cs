using System;
using System.Collections.Generic;
using HarmonyLib;
using ModAPI.Saves;
using UnityEngine;
using ModAPI.Hooks.Paging;

namespace ModAPI.Hooks
{
    [HarmonyPatch(typeof(SlotSelectionPanel), "OnShow")]
    internal static class SlotSelectionPanel_OnShow_Patch
    {
        static void Postfix(SlotSelectionPanel __instance)
        {
            PagingManager.Initialize(__instance);
        }
    }

    /// <summary>
    /// This is the definitive fix. By using a Prefix patch, we can completely prevent the
    /// original game logic from running on our expanded save pages. This stops the game from
    /// overwriting our UI data with stale vanilla information in a subsequent frame.
    /// </summary>
    [HarmonyPatch(typeof(SlotSelectionPanel), "RefreshSaveSlotInfo")]
    internal static class SlotSelectionPanel_RefreshSaveSlotInfo_Patch
    {
        /// <summary>
        /// Runs BEFORE the original RefreshSaveSlotInfo method.
        /// </summary>
        /// <returns>True = run original method, False = skip original method.</returns>
        static bool Prefix(SlotSelectionPanel __instance)
        {
            int page = PagingManager.GetPage(__instance);
            MMLog.WriteDebug($"[RefreshSaveSlotInfo PREFIX] Intercepting refresh. Current UI Page: {page + 1}.");

            // Page 0 is the VANILLA page. We let the original game method run untouched.
            if (page == 0)
            {
                MMLog.WriteDebug("[RefreshSaveSlotInfo PREFIX] Page is 1 (Vanilla). Allowing original method to execute.");
                // Ensure vanilla-specific scenario buttons are visible.
                var vanillaButtons = __instance.GetComponentsInChildren<SaveSlotButton>(true);
                foreach (var btn in vanillaButtons)
                {
                    if (btn != null && (btn.slotNumber == 3 || btn.slotNumber == 4))
                    {
                        if (!btn.gameObject.activeSelf) btn.gameObject.SetActive(true);
                    }
                }
                return true; // <<< Let the original method run
            }

            // If we are here, we are on an EXPANDED page (UI Page 2+). We take full control.
            MMLog.WriteDebug($"[RefreshSaveSlotInfo PREFIX] Page is expanded. Taking full control of refresh logic.");
            try
            {
                int apiPage = page - 1;
                var savesOnPage = ExpandedVanillaSaves.List(apiPage, 3);
                MMLog.WriteDebug($"[RefreshSaveSlotInfo PREFIX] Fetched {savesOnPage.Length} saves for API page {apiPage}.");

                var t = Traverse.Create(__instance);
                var m_slotInfo = t.Field("m_slotInfo").GetValue<System.Collections.IList>();
                if (m_slotInfo == null)
                {
                    MMLog.WriteError("[RefreshSaveSlotInfo PREFIX] m_slotInfo is null. Aborting.");
                    return false;
                }

                // Hide vanilla scenario buttons.
                var buttons = __instance.GetComponentsInChildren<SaveSlotButton>(true);
                foreach (var btn in buttons)
                {
                    if (btn != null && (btn.slotNumber == 3 || btn.slotNumber == 4))
                    {
                        btn.gameObject.SetActive(false);
                    }
                }

                MMLog.WriteDebug("[RefreshSaveSlotInfo PREFIX] Overwriting the 3 visible UI slots with expanded save data.");
                for (int i = 0; i < 3; i++)
                {
                    var slotInfo = m_slotInfo[i];
                    var entry = (i < savesOnPage.Length) ? savesOnPage[i] : null;

                    if (entry != null)
                    {
                        Traverse.Create(slotInfo).Field("m_state").SetValue(SlotSelectionPanel.SlotState.Loaded);
                        Traverse.Create(slotInfo).Field("m_familyName").SetValue(entry.saveInfo.familyName);
                        Traverse.Create(slotInfo).Field("m_daysSurvived").SetValue(entry.saveInfo.daysSurvived);
                        Traverse.Create(slotInfo).Field("m_diffSetting").SetValue(entry.saveInfo.difficulty);
                        Traverse.Create(slotInfo).Field("m_saveTime").SetValue(entry.saveInfo.saveTime ?? entry.updatedAt);
                    }
                    else
                    {
                        Traverse.Create(slotInfo).Field("m_state").SetValue(SlotSelectionPanel.SlotState.Empty);
                    }
                }

                // We MUST call RefreshSlotLabels ourselves because we are skipping the original method.
                MMLog.WriteDebug("[RefreshSaveSlotInfo PREFIX] Calling RefreshSlotLabels to draw our changes.");
                t.Method("RefreshSlotLabels").GetValue();
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[RefreshSaveSlotInfo PREFIX] Error during takeover: " + ex);
            }

            return false; // <<< IMPORTANT: Block the original method from running and overwriting our changes.
        }
    }

    [HarmonyPatch(typeof(SlotSelectionPanel), "RefreshSlotLabels")]
    internal static class SlotSelectionPanel_RefreshSlotLabels_Patch
    {
        static void Postfix(SlotSelectionPanel __instance)
        {
            int page = PagingManager.GetPage(__instance);
            if (page <= 0) return; // Only modify labels on expanded pages.

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

                    // Replace "Slot X" with the correct number for the page, e.g., "Slot 4".
                    string updated = ReplaceFirstNumber(lab.text, (i + 4 + offset).ToString());
                    if (updated != lab.text)
                    {
                        lab.text = updated;
                    }
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
            int page = PagingManager.GetPage(__instance);
            if (page == 0)
            {
                MMLog.WriteDebug("[OnSlotChosen] Prefix: Page is 1 (Vanilla). Letting original game logic run.");
                return true;
            }

            try
            {
                var t = Traverse.Create(__instance);
                int chosenSlotIndex = t.Field("m_chosenSlot").GetValue<int>(); // 0, 1, or 2
                if (chosenSlotIndex > 2) return true;

                MMLog.WriteDebug($"[OnSlotChosen] Prefix: User chose UI slot {chosenSlotIndex + 1} on expanded page {page + 1}.");

                int apiPage = page - 1;
                var entry = ExpandedVanillaSaves.FindByUIPosition(chosenSlotIndex + 1, apiPage, 3);

                var proxy = SaveManager.instance.platformSave as PlatformSaveProxy;
                if (proxy == null)
                {
                    MMLog.WriteError("[OnSlotChosen] PlatformSaveProxy not found! Cannot handle custom save.");
                    return true;
                }

                var virtualSaveType = (SaveManager.SaveType)(chosenSlotIndex + 1);

                if (entry == null) // Creating a new game
                {
                    MMLog.WriteDebug("[OnSlotChosen] Action: Creating a new expanded vanilla save.");
                    var created = ExpandedVanillaSaves.Create(new SaveCreateOptions { name = "New Game" });
                    if (created != null)
                    {
                        proxy.SetNextSave(virtualSaveType, "Standard", created.id);
                        MMLog.WriteDebug($"[OnSlotChosen] Outcome: Primed proxy to save new game with ID {created.id}. Passing to vanilla for customization screen.");
                    }
                    return true;
                }
                else // Loading an existing expanded game
                {
                    MMLog.WriteDebug($"[OnSlotChosen] Action: Loading existing expanded save '{entry.name}' with ID {entry.id}.");
                    proxy.SetNextLoad(virtualSaveType, "Standard", entry.id);
                    SaveManager.instance.SetSlotToLoad(chosenSlotIndex + 1);
                    MMLog.WriteDebug("[OnSlotChosen] Outcome: Primed proxy and called SaveManager.SetSlotToLoad. Blocking original method.");
                    return false;
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
                    MMLog.WriteDebug($"[OnDeleteMessageBox] Deleting expanded save '{entry.name}' (ID: {entry.id})");
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