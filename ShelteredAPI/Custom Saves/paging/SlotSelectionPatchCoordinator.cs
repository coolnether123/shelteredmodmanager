using System;
using System.IO;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Hooks.Paging;
using ModAPI.Saves;
using UnityEngine;

namespace ModAPI.Hooks
{
    internal static class SlotSelectionPatchCoordinator
    {
        internal static void Initialize(SlotSelectionPanel panel)
        {
            PagingManager.Initialize(panel);
        }

        internal static bool RefreshSaveSlotInfoPrefix(SlotSelectionPanel panel)
        {
            int page = PagingManager.GetPage(panel);
            if (page == 0)
            {
                var vanillaButtons = panel.GetComponentsInChildren<SaveSlotButton>(true);
                foreach (var btn in vanillaButtons)
                {
                    if (btn != null && (btn.slotNumber == 3 || btn.slotNumber == 4) && !btn.gameObject.activeSelf)
                        btn.gameObject.SetActive(true);
                }

                SaveVerification.UpdateIcons(panel);
                return true;
            }

            try
            {
                int apiPage = page - 1;
                var allSaves = ExpandedVanillaSaves.List();
                var savesOnPage = new SaveEntry[3];

                foreach (var save in allSaves)
                {
                    int saveSlot = save.absoluteSlot;
                    if (saveSlot <= 0)
                        continue;

                    int saveApiPage = (saveSlot - 4) / 3;
                    if (saveApiPage != apiPage)
                        continue;

                    int slotIndexOnPage = (saveSlot - 4) % 3;
                    if (slotIndexOnPage >= 0 && slotIndexOnPage < 3)
                        savesOnPage[slotIndexOnPage] = save;
                }

                var t = Traverse.Create(panel);
                var slotInfoList = t.Field("m_slotInfo").GetValue<System.Collections.IList>();

                var buttons = panel.GetComponentsInChildren<SaveSlotButton>(true);
                foreach (var btn in buttons)
                {
                    if (btn != null && (btn.slotNumber == 3 || btn.slotNumber == 4))
                        btn.gameObject.SetActive(false);
                }

                for (int i = 0; i < 3; i++)
                {
                    var slotInfo = slotInfoList[i];
                    var entry = i < savesOnPage.Length ? savesOnPage[i] : null;

                    if (entry != null)
                    {
                        var tSlot = Traverse.Create(slotInfo);
                        MMLog.WriteDebug("[RefreshSaveSlotInfo] Setting Slot " + entry.absoluteSlot + " to LOADED. Family='" + entry.saveInfo.familyName + "'");
                        tSlot.Field("m_state").SetValue(SlotSelectionPanel.SlotState.Loaded);
                        tSlot.Field("m_familyName").SetValue(entry.saveInfo.familyName);
                        tSlot.Field("m_daysSurvived").SetValue(entry.saveInfo.daysSurvived);
                        tSlot.Field("m_diffSetting").SetValue(entry.saveInfo.difficulty);
                        tSlot.Field("m_rainDiff").SetValue(entry.saveInfo.rainDiff);
                        tSlot.Field("m_resourceDiff").SetValue(entry.saveInfo.resourceDiff);
                        tSlot.Field("m_breachDiff").SetValue(entry.saveInfo.breachDiff);
                        tSlot.Field("m_factionDiff").SetValue(entry.saveInfo.factionDiff);
                        tSlot.Field("m_moodDiff").SetValue(entry.saveInfo.moodDiff);
                        tSlot.Field("m_mapSize").SetValue(entry.saveInfo.mapSize);
                        tSlot.Field("m_fog").SetValue(entry.saveInfo.fog);

                        string rawTime = entry.saveInfo.saveTime ?? entry.updatedAt;
                        string displayTime = FormatDisplayTime(rawTime);

                        if (tSlot.Field("m_dateSaved").FieldExists()) tSlot.Field("m_dateSaved").SetValue(displayTime);
                        if (tSlot.Field("m_saveTime").FieldExists()) tSlot.Field("m_saveTime").SetValue(displayTime);
                    }
                    else
                    {
                        MMLog.WriteDebug("[RefreshSaveSlotInfo] Setting physical slot " + (i + 1) + " on page " + page + " to EMPTY");
                        Traverse.Create(slotInfo).Field("m_state").SetValue(SlotSelectionPanel.SlotState.Empty);
                    }
                }

                t.Method("RefreshSlotLabels").GetValue();
                SaveVerification.UpdateIcons(panel);
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[RefreshSaveSlotInfo PREFIX] Error during takeover: " + ex);
            }

            return false;
        }

        internal static void RefreshSaveSlotInfoPostfix(SlotSelectionPanel panel)
        {
            if (PagingManager.GetPage(panel) != 0)
                return;

            try
            {
                var t = Traverse.Create(panel);
                var slotInfos = t.Field("m_slotInfo").GetValue<System.Collections.IList>();

                if (slotInfos != null)
                {
                    for (int i = 0; i < 3 && i < slotInfos.Count; i++)
                    {
                        var info = slotInfos[i];
                        if (info == null)
                            continue;

                        var tSlot = Traverse.Create(info);
                        var state = tSlot.Field("m_state").GetValue<SlotSelectionPanel.SlotState>();
                        if (state != SlotSelectionPanel.SlotState.Empty)
                            continue;

                        tSlot.Field("m_familyName").SetValue(string.Empty);
                        tSlot.Field("m_daysSurvived").SetValue(0);
                        if (tSlot.Field("m_dateSaved").FieldExists()) tSlot.Field("m_dateSaved").SetValue(string.Empty);
                        if (tSlot.Field("m_saveTime").FieldExists()) tSlot.Field("m_saveTime").SetValue(string.Empty);
                    }
                }

                t.Method("RefreshSlotLabels").GetValue();
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[RefreshSaveSlotInfo Postfix] Error during Page 0 cleanup: " + ex.Message);
            }
        }

        internal static void RefreshSlotLabelsPostfix(SlotSelectionPanel panel)
        {
            int page = PagingManager.GetPage(panel);
            if (page <= 0)
                return;

            try
            {
                int offset = (page - 1) * 3;
                var t = Traverse.Create(panel);
                var labels = t.Field("m_slotButtonLabels").GetValue<System.Collections.IList>();
                if (labels == null)
                    return;

                for (int i = 0; i < labels.Count && i < 3; i++)
                {
                    var lab = labels[i] as UILabel;
                    if (lab == null || string.IsNullOrEmpty(lab.text))
                        continue;

                    string updated = ReplaceFirstNumber(lab.text, (i + 4 + offset).ToString());
                    if (updated != lab.text)
                        lab.text = updated;
                }
            }
            catch (Exception ex)
            {
                MMLog.WriteError("RefreshSlotLabels Postfix patch error: " + ex.Message);
            }
        }

        internal static bool OnSlotChosenPrefix(SlotSelectionPanel panel)
        {
            int page = PagingManager.GetPage(panel);
            return page == 0 ? HandleVanillaSlotChosen(panel) : HandleCustomSlotChosen(panel, page);
        }

        internal static bool OnDeleteMessageBoxPrefix(SlotSelectionPanel panel, int response)
        {
            int page = PagingManager.GetPage(panel);
            if (page == 0)
            {
                if (response == 1)
                {
                    try
                    {
                        var t = Traverse.Create(panel);
                        int selectedSlotIndex = t.Field("m_selectedSlot").GetValue<int>();
                        int absoluteSlot = selectedSlotIndex + 1;

                        MMLog.WriteDebug("[OnDeleteMessageBox] Detected vanilla save deletion for Slot " + absoluteSlot + ". Cleaning up metadata...");
                        SaveDeleteRouter.DeleteAbsoluteSlot(absoluteSlot, "OnDeleteMessageBox.VanillaCleanup");
                    }
                    catch (Exception ex)
                    {
                        MMLog.WriteError("[OnDeleteMessageBox] Error cleaning up vanilla slot metadata: " + ex);
                    }
                }

                return true;
            }

            if (response != 1)
                return false;

            try
            {
                var t = Traverse.Create(panel);
                int selectedSlotIndex = t.Field("m_selectedSlot").GetValue<int>();
                if (selectedSlotIndex > 2)
                    return true;

                var entry = ExpandedVanillaSaves.FindByUIPosition(selectedSlotIndex + 1, page, 3, false);
                if (entry != null)
                {
                    MMLog.WriteDebug("[OnDeleteMessageBox] Deleting custom slot " + entry.absoluteSlot + "...");
                    SaveDeleteRouter.DeleteAbsoluteSlot(entry.absoluteSlot, "OnDeleteMessageBox.CustomDelete");
                    t.Field("m_infoNeedsRefresh").SetValue(true);
                }
                else
                {
                    MMLog.WriteWarning("[OnDeleteMessageBox] Could not find entry to delete at physical slot " + (selectedSlotIndex + 1) + " page " + page);
                }

                return false;
            }
            catch (Exception ex)
            {
                MMLog.WriteError("OnDeleteMessageBox Prefix patch error: " + ex);
                return true;
            }
        }

        internal static void UpdatePostfix(SlotSelectionPanel panel)
        {
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                PagingManager.ChangePage(panel, 1);
            }
            else if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                PagingManager.ChangePage(panel, -1);
            }
        }

        private static bool HandleVanillaSlotChosen(SlotSelectionPanel panel)
        {
            try
            {
                var t = Traverse.Create(panel);
                int chosenSlotIndex = t.Field("m_selectedSlot").GetValue<int>();
                if (chosenSlotIndex < 0 || chosenSlotIndex >= 3)
                    return true;

                var vanillaSaveInfo = SaveRegistryCore.ReadVanillaSaveInfo(chosenSlotIndex + 1);
                var manifestPath = Path.Combine(DirectoryProvider.SlotRoot("Standard", chosenSlotIndex + 1, false), "manifest.json");

                if (vanillaSaveInfo == null && File.Exists(manifestPath))
                {
                    MMLog.WriteDebug("[OnSlotChosen] Found orphaned manifest for empty vanilla slot " + (chosenSlotIndex + 1) + ". Auto-cleaning.");
                    SaveDeleteRouter.DeleteAbsoluteSlot(chosenSlotIndex + 1, "OnSlotChosen.OrphanedManifestCleanup");
                    return true;
                }

                SlotManifest manifest = ReadManifest(manifestPath);
                if (manifest == null)
                    return true;

                var state = SaveVerification.Verify(manifest);
                if (state == SaveVerification.VerificationState.Match)
                    return true;

                var entry = new SaveEntry
                {
                    id = "vanilla_slot_" + (chosenSlotIndex + 1),
                    absoluteSlot = chosenSlotIndex + 1,
                    saveInfo = new SaveInfo
                    {
                        familyName = vanillaSaveInfo != null ? vanillaSaveInfo.familyName : manifest.family_name ?? "Unknown",
                        daysSurvived = vanillaSaveInfo != null ? vanillaSaveInfo.daysSurvived : 0,
                        saveTime = vanillaSaveInfo != null ? vanillaSaveInfo.saveTime : DateTime.Now.ToString()
                    }
                };

                SaveDetailsWindow.Show(entry, manifest, state, true, delegate
                {
                    SaveProtectionPatches.LoadGamePatch._forceLoad = true;
                    SaveManager.instance.SetSlotToLoad(chosenSlotIndex + 1);
                });

                return false;
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[OnSlotChosen vanilla check] Error: " + ex);
                return true;
            }
        }

        private static bool HandleCustomSlotChosen(SlotSelectionPanel panel, int page)
        {
            try
            {
                if (!panel.m_inputEnabled || (SaveManager.instance != null && SaveManager.instance.isDeleting))
                    return false;

                var t = Traverse.Create(panel);
                int chosenSlotIndex = t.Field("m_selectedSlot").GetValue<int>();
                if (chosenSlotIndex > 2)
                    return true;

                var entry = ExpandedVanillaSaves.FindByUIPosition(chosenSlotIndex + 1, page, 3);
                var virtualSaveType = (SaveManager.SaveType)(chosenSlotIndex + 1);

                if (entry == null)
                {
                    int absoluteSlot = (page - 1) * 3 + chosenSlotIndex + 4;
                    MMLog.WriteDebug("--- Player clicked slot " + absoluteSlot + " to start a new game ---");

                    var created = ExpandedVanillaSaves.Create(new SaveCreateOptions { name = "New Game", absoluteSlot = absoluteSlot });
                    if (created != null)
                    {
                        PlatformSaveProxy.SetNextSave(virtualSaveType, "Standard", created.id);
                        SaveManager.instance.SetCurrentSlot(chosenSlotIndex + 1);
                    }

                    return true;
                }

                SlotManifest manifest = ReadManifest(Path.Combine(DirectoryProvider.SlotRoot("Standard", entry.absoluteSlot, false), "manifest.json"));
                var state = SaveVerification.Verify(manifest);
                if (state != SaveVerification.VerificationState.Match)
                {
                    SaveDetailsWindow.Show(entry, manifest, state, true, delegate
                    {
                        QueueCustomLoad(t, chosenSlotIndex, virtualSaveType, entry);
                    });
                    return false;
                }

                QueueCustomLoad(t, chosenSlotIndex, virtualSaveType, entry);
                return false;
            }
            catch (Exception ex)
            {
                MMLog.WriteError("OnSlotChosen Prefix patch error: " + ex);
                return true;
            }
        }

        private static void QueueCustomLoad(Traverse panelTraverse, int chosenSlotIndex, SaveManager.SaveType virtualSaveType, SaveEntry entry)
        {
            PlatformSaveProxy.SetNextLoad(virtualSaveType, "Standard", entry.id);
            ApplyDifficultySettings(entry.saveInfo);

            var loadingGraphic = panelTraverse.Field("m_loadingGraphic").GetValue<GameObject>();
            if (loadingGraphic != null)
                loadingGraphic.SetActive(true);

            SaveManager.instance.SetSlotToLoad(chosenSlotIndex + 1);
        }

        private static void ApplyDifficultySettings(SaveInfo saveInfo)
        {
            if (saveInfo == null)
                return;

            DifficultyManager.StoreMenuDifficultySettings(
                saveInfo.rainDiff,
                saveInfo.resourceDiff,
                saveInfo.breachDiff,
                saveInfo.factionDiff,
                saveInfo.moodDiff,
                saveInfo.mapSize,
                saveInfo.fog);
        }

        private static SlotManifest ReadManifest(string manifestPath)
        {
            if (string.IsNullOrEmpty(manifestPath) || !File.Exists(manifestPath))
                return null;

            try
            {
                return SaveRegistryCore.DeserializeSlotManifest(File.ReadAllText(manifestPath));
            }
            catch
            {
                return null;
            }
        }

        private static string FormatDisplayTime(string rawTime)
        {
            if (string.IsNullOrEmpty(rawTime))
                return string.Empty;

            try
            {
                bool hasExplicitOffset =
                    rawTime.IndexOf('Z') >= 0 ||
                    rawTime.IndexOf('+') >= 0 ||
                    rawTime.LastIndexOf('-') > 9;

                DateTimeOffset dto;
                if (hasExplicitOffset && DateTimeOffset.TryParse(rawTime, out dto))
                    return dto.ToLocalTime().ToString("g");

                DateTime dt;
                if (DateTime.TryParse(rawTime, out dt))
                {
                    if (dt.Kind == DateTimeKind.Utc)
                        return dt.ToLocalTime().ToString("g");
                    return dt.ToString("g");
                }
            }
            catch
            {
            }

            return rawTime;
        }

        private static string ReplaceFirstNumber(string value, string replacement)
        {
            int start = -1;
            int len = 0;
            for (int i = 0; i < value.Length; i++)
            {
                if (char.IsDigit(value[i]))
                {
                    start = i;
                    break;
                }
            }

            if (start < 0)
                return value;

            for (int i = start; i < value.Length && char.IsDigit(value[i]); i++)
                len++;

            return value.Substring(0, start) + replacement + value.Substring(start + len);
        }
    }
}
