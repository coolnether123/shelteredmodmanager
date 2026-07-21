using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using ModAPI.Core;
using ShelteredAPI.Core;
using ShelteredAPI.Saves;
using ShelteredAPI.Saves.Backups;
using ShelteredAPI.Saves.Runtime;
using UnityEngine;
namespace ShelteredAPI.Saves.Paging{
    internal static class SlotSelectionPatchCoordinator
    {
        private sealed class PendingDeleteIntent
        {
            public SlotSelectionPanel Panel;
            public int Page;
            public int UiSlotIndex;
            public int AbsoluteSlot;
            public string ScenarioId;
            public string EntryId;
            public SaveBackupSnapshotInfo Snapshot;
        }

        private static readonly Dictionary<SlotSelectionPanel, PendingDeleteIntent> PendingDeleteIntents =
            new Dictionary<SlotSelectionPanel, PendingDeleteIntent>();
        private static readonly Dictionary<SlotSelectionPanel, PendingDeleteIntent> StagedDeleteIntents =
            new Dictionary<SlotSelectionPanel, PendingDeleteIntent>();

        internal static void Initialize(SlotSelectionPanel panel)
        {
            ClearDeleteIntent(panel);
            PagingManager.Initialize(panel);
        }

        internal static bool RefreshSaveSlotInfoPrefix(SlotSelectionPanel panel)
        {
            if (SaveSnapshotBrowserState.IsActive(panel))
                return RefreshSnapshotSaveSlotInfoPrefix(panel);

            int page = PagingManager.GetPage(panel);
            if (page == 0)
            {
                RefreshVanillaSaveSlotInfo(panel);
                return false;
            }

            try
            {
                int apiPage = page - 1;
                SlotPagingScope scope = SlotPagingScopeResolver.Resolve(panel);
                var allSaves = scope.ListSaves();
                var savesOnPage = new SaveEntry[3];

                foreach (var save in allSaves)
                {
                    int saveSlot = save.absoluteSlot;
                    if (saveSlot <= 0)
                        continue;

                    if (saveSlot < scope.FirstExpandedSlot)
                        continue;

                    int saveApiPage = (saveSlot - scope.FirstExpandedSlot) / 3;
                    if (saveApiPage != apiPage)
                        continue;

                    int slotIndexOnPage = (saveSlot - scope.FirstExpandedSlot) % 3;
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
                UpdateSaveSlotAuxiliaryControls(panel);
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[RefreshSaveSlotInfo PREFIX] Error during takeover: " + ex);
            }

            return false;
        }

        internal static void RefreshSaveSlotInfoPostfix(SlotSelectionPanel panel)
        {
        }

        internal static void RefreshSlotLabelsPostfix(SlotSelectionPanel panel)
        {
            if (SaveSnapshotBrowserState.IsActive(panel))
            {
                RefreshSnapshotSlotLabels(panel);
                return;
            }

            int page = PagingManager.GetPage(panel);
            if (page <= 0)
                return;

            try
            {
                int offset = (page - 1) * 3;
                SlotPagingScope scope = SlotPagingScopeResolver.Resolve(panel);
                var t = Traverse.Create(panel);
                var labels = t.Field("m_slotButtonLabels").GetValue<System.Collections.IList>();
                if (labels == null)
                    return;

                for (int i = 0; i < labels.Count && i < 3; i++)
                {
                    var lab = labels[i] as UILabel;
                    if (lab == null || string.IsNullOrEmpty(lab.text))
                        continue;

                    string updated = ReplaceFirstNumber(lab.text, (scope.FirstExpandedSlot + offset + i).ToString());
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
            if (SaveSnapshotBrowserState.IsActive(panel))
                return HandleSnapshotSlotChosen(panel);

            int page = PagingManager.GetPage(panel);
            return page == 0 ? HandleVanillaSlotChosen(panel) : HandleCustomSlotChosen(panel, page);
        }

        internal static void SaveSlotButtonOnClickPrefix(SaveSlotButton button)
        {
            if (button == null || UICamera.currentTouchID != -2)
                return;

            try
            {
                SlotSelectionPanel panel = Traverse.Create(button).Field("m_slotSelectionPanel").GetValue<SlotSelectionPanel>();
                if (panel == null)
                    return;

                int uiSlotIndex = button.slotNumber;
                if (uiSlotIndex < 0)
                    return;

                StagedDeleteIntents[panel] = CaptureDeleteIntent(panel, uiSlotIndex);
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[OnDeleteMessageBox] Could not capture save-slot delete intent: " + ex.Message);
            }
        }

        internal static void PromptDeleteCurrentSlotPrefix(SlotSelectionPanel panel)
        {
            if (panel == null)
                return;

            try
            {
                PendingDeleteIntent intent;
                if (StagedDeleteIntents.TryGetValue(panel, out intent) && intent != null)
                {
                    StagedDeleteIntents.Remove(panel);
                    PendingDeleteIntents[panel] = intent;
                    return;
                }

                int selectedSlotIndex = Traverse.Create(panel).Field("m_selectedSlot").GetValue<int>();
                if (selectedSlotIndex < 0)
                    return;

                PendingDeleteIntents[panel] = CaptureDeleteIntent(panel, selectedSlotIndex);
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[PromptDeleteCurrentSlot] Could not bind save-slot delete intent: " + ex.Message);
            }
        }

        internal static bool OnCancelPrefix(SlotSelectionPanel panel)
        {
            ClearDeleteIntent(panel);

            if (!SaveSnapshotBrowserState.IsActive(panel))
                return true;

            SaveSnapshotBrowserState.Exit(panel);
            panel.RefreshSaveSlotInfo();
            PagingManager.Update(panel);
            return false;
        }

        internal static bool OnDeleteMessageBoxPrefix(SlotSelectionPanel panel, int response)
        {
            if (SaveSnapshotBrowserState.IsActive(panel))
                return HandleSnapshotDeleteMessageBox(panel, response);

            int page = PagingManager.GetPage(panel);
            if (page == 0)
            {
                if (response == 1)
                {
                    try
                    {
                        var t = Traverse.Create(panel);
                        int selectedSlotIndex = t.Field("m_selectedSlot").GetValue<int>();
                        string consistencyError;
                        if (!ValidateDeleteIntent(panel, selectedSlotIndex, (SaveEntry)null, out consistencyError))
                        {
                            ShowDeleteConsistencyFailure(consistencyError);
                            return false;
                        }
                        int absoluteSlot = selectedSlotIndex + 1;

                        MMLog.WriteDebug("[OnDeleteMessageBox] Detected vanilla save deletion for Slot " + absoluteSlot + ". Cleaning up metadata...");
                        string deleteError;
                        if (!SaveDeleteRouter.DeleteAbsoluteSlot(
                            absoluteSlot,
                            "OnDeleteMessageBox.VanillaCleanup",
                            out deleteError))
                        {
                            ShowDeletePreservationFailure(deleteError);
                            return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        ClearDeleteIntent(panel);
                        MMLog.WriteError("[OnDeleteMessageBox] Error cleaning up vanilla slot metadata: " + ex);
                        ShowDeletePreservationFailure(ex.Message);
                        return false;
                    }
                }
                else
                {
                    ClearDeleteIntent(panel);
                }

                return true;
            }

            if (response != 1)
            {
                ClearDeleteIntent(panel);
                return false;
            }

            try
            {
                var t = Traverse.Create(panel);
                int selectedSlotIndex = t.Field("m_selectedSlot").GetValue<int>();
                if (selectedSlotIndex > 2)
                {
                    ClearDeleteIntent(panel);
                    return true;
                }

                SlotPagingScope scope = SlotPagingScopeResolver.Resolve(panel);
                var entry = scope.FindByUIPosition(selectedSlotIndex + 1, page, 3, false);
                if (entry != null)
                {
                    string consistencyError;
                    if (!ValidateDeleteIntent(panel, selectedSlotIndex, entry, out consistencyError))
                    {
                        ShowDeleteConsistencyFailure(consistencyError);
                        return false;
                    }

                    MMLog.WriteDebug("[OnDeleteMessageBox] Deleting custom slot " + entry.absoluteSlot
                        + " scenario=" + scope.StorageScenarioId + "...");
                    string deleteError;
                    if (!SaveDeleteRouter.DeleteAbsoluteSlot(
                        scope.StorageScenarioId,
                        entry.absoluteSlot,
                        "OnDeleteMessageBox.CustomDelete",
                        out deleteError))
                    {
                        ShowDeletePreservationFailure(deleteError);
                        return false;
                    }
                    t.Field("m_infoNeedsRefresh").SetValue(true);
                }
                else
                {
                    ClearDeleteIntent(panel);
                    MMLog.WriteWarning("[OnDeleteMessageBox] Could not find entry to delete at physical slot " + (selectedSlotIndex + 1) + " page " + page);
                }

                return false;
            }
            catch (Exception ex)
            {
                ClearDeleteIntent(panel);
                MMLog.WriteError("OnDeleteMessageBox Prefix patch error: " + ex);
                return true;
            }
        }

        private static void ShowDeletePreservationFailure(string error)
        {
            MessageBox.Show(
                MessageBoxButtons.Okay_Button,
                "The save was not deleted because a recovery snapshot could not be created.\n"
                    + (string.IsNullOrEmpty(error) ? "Unknown error" : error),
                null,
                null,
                null,
                false);
        }

        private static void ShowDeleteConsistencyFailure(string error)
        {
            string message = "The confirmed row does not match the panel's internal selection, and no save was deleted.";
            if (!string.IsNullOrEmpty(error))
                message += "\n" + error;

            MMLog.WriteError("[OnDeleteMessageBox] " + message.Replace('\n', ' '));
            MessageBox.Show(
                MessageBoxButtons.Okay_Button,
                message,
                null,
                null,
                null,
                false);
        }

        private static bool HandleSnapshotDeleteMessageBox(SlotSelectionPanel panel, int response)
        {
            if (response != 1)
            {
                ClearDeleteIntent(panel);
                return false;
            }

            try
            {
                var t = Traverse.Create(panel);
                int selectedSlotIndex = t.Field("m_selectedSlot").GetValue<int>();
                if (selectedSlotIndex < 0 || selectedSlotIndex > 2)
                {
                    ClearDeleteIntent(panel);
                    return false;
                }

                SaveBackupSnapshotInfo snapshot = SaveSnapshotBrowserState.GetSnapshotAt(panel, selectedSlotIndex);
                string consistencyError;
                if (!ValidateDeleteIntent(panel, selectedSlotIndex, snapshot, out consistencyError))
                {
                    ShowDeleteConsistencyFailure(consistencyError);
                    return false;
                }

                if (snapshot == null)
                {
                    MMLog.WriteWarning("[SnapshotBrowser] Could not find snapshot to delete at physical slot "
                        + (selectedSlotIndex + 1) + " page " + PagingManager.GetPage(panel) + ".");
                    return false;
                }

                string error;
                if (!SaveBackupService.DeleteSnapshot(snapshot, out error))
                {
                    MessageBox.Show(MessageBoxButtons.Okay_Button,
                        "Failed to delete backup snapshot:\n" + (error ?? "Unknown error"),
                        null,
                        null,
                        null,
                        false);
                    return false;
                }

                MMLog.WriteInfo("[SnapshotBrowser] Deleted snapshot " + snapshot.Ref.SnapshotId
                    + " from timeline " + snapshot.Ref.TimelineKey + ".");
                RefreshSnapshotBrowserAfterDelete(panel);
            }
            catch (Exception ex)
            {
                ClearDeleteIntent(panel);
                MMLog.WriteError("[SnapshotBrowser] Snapshot delete error: " + ex);
                MessageBox.Show(MessageBoxButtons.Okay_Button,
                    "Failed to delete backup snapshot:\n" + ex.Message,
                    null,
                    null,
                    null,
                    false);
            }

            return false;
        }

        private static PendingDeleteIntent CaptureDeleteIntent(SlotSelectionPanel panel, int uiSlotIndex)
        {
            PendingDeleteIntent intent = new PendingDeleteIntent
            {
                Panel = panel,
                Page = PagingManager.GetPage(panel),
                UiSlotIndex = uiSlotIndex,
                ScenarioId = "Standard",
                AbsoluteSlot = uiSlotIndex + 1
            };

            if (SaveSnapshotBrowserState.IsActive(panel))
            {
                intent.Snapshot = SaveSnapshotBrowserState.GetSnapshotAt(panel, uiSlotIndex);
                if (intent.Snapshot != null)
                {
                    intent.ScenarioId = intent.Snapshot.ScenarioId;
                    intent.AbsoluteSlot = intent.Snapshot.Entry != null ? intent.Snapshot.Entry.absoluteSlot : intent.AbsoluteSlot;
                    intent.EntryId = intent.Snapshot.Entry != null ? intent.Snapshot.Entry.id : null;
                }
            }
            else if (intent.Page > 0)
            {
                SlotPagingScope scope = SlotPagingScopeResolver.Resolve(panel);
                SaveEntry entry = scope.FindByUIPosition(uiSlotIndex + 1, intent.Page, 3, false);
                intent.ScenarioId = scope.StorageScenarioId;
                intent.AbsoluteSlot = entry != null ? entry.absoluteSlot : scope.GetAbsoluteSlot(intent.Page, uiSlotIndex, 3);
                intent.EntryId = entry != null ? entry.id : null;
            }

            return intent;
        }

        private static bool ValidateDeleteIntent(
            SlotSelectionPanel panel,
            int selectedSlotIndex,
            SaveEntry entry,
            out string error)
        {
            return ValidateDeleteIntent(panel, selectedSlotIndex, entry, null, out error);
        }

        private static bool ValidateDeleteIntent(
            SlotSelectionPanel panel,
            int selectedSlotIndex,
            SaveBackupSnapshotInfo snapshot,
            out string error)
        {
            return ValidateDeleteIntent(panel, selectedSlotIndex, null, snapshot, out error);
        }

        private static bool ValidateDeleteIntent(
            SlotSelectionPanel panel,
            int selectedSlotIndex,
            SaveEntry entry,
            SaveBackupSnapshotInfo snapshot,
            out string error)
        {
            error = null;
            PendingDeleteIntent intent;
            if (panel == null || !PendingDeleteIntents.TryGetValue(panel, out intent) || intent == null)
                return true;

            PendingDeleteIntents.Remove(panel);
            int page = PagingManager.GetPage(panel);
            if (intent.Page != page)
            {
                error = "Delete was requested on page " + intent.Page + " but confirmed on page " + page + ".";
                return false;
            }

            if (intent.UiSlotIndex != selectedSlotIndex)
            {
                error = "Delete was requested for row " + (intent.UiSlotIndex + 1)
                    + " but panel state selected row " + (selectedSlotIndex + 1) + ".";
                return false;
            }

            if (entry != null)
            {
                string scenarioId = string.IsNullOrEmpty(intent.ScenarioId) ? "Standard" : intent.ScenarioId;
                string entryScenarioId = string.IsNullOrEmpty(entry.scenarioId) ? scenarioId : entry.scenarioId;
                if (!string.Equals(scenarioId, entryScenarioId, StringComparison.OrdinalIgnoreCase)
                    || intent.AbsoluteSlot != entry.absoluteSlot
                    || (!string.IsNullOrEmpty(intent.EntryId) && !string.Equals(intent.EntryId, entry.id, StringComparison.Ordinal)))
                {
                    error = "Delete target changed from scenario=" + scenarioId + " slot=" + intent.AbsoluteSlot
                        + " saveId=" + (intent.EntryId ?? "<none>")
                        + " to scenario=" + entryScenarioId + " slot=" + entry.absoluteSlot
                        + " saveId=" + (entry.id ?? "<none>") + ".";
                    return false;
                }
            }

            if (snapshot != null && intent.Snapshot != null)
            {
                string intentSnapshotId = intent.Snapshot.Ref != null ? intent.Snapshot.Ref.SnapshotId : "<none>";
                string snapshotId = snapshot.Ref != null ? snapshot.Ref.SnapshotId : "<none>";
                if (!string.Equals(intentSnapshotId, snapshotId, StringComparison.Ordinal))
                {
                    error = "Delete target changed from snapshot=" + intentSnapshotId + " to snapshot=" + snapshotId + ".";
                    return false;
                }
            }

            return true;
        }

        private static void ClearDeleteIntent(SlotSelectionPanel panel)
        {
            if (panel == null)
                return;

            StagedDeleteIntents.Remove(panel);
            PendingDeleteIntents.Remove(panel);
        }

        private static void RefreshSnapshotBrowserAfterDelete(SlotSelectionPanel panel)
        {
            SaveSnapshotBrowserSession session;
            if (!SaveSnapshotBrowserState.TryGet(panel, out session))
                return;

            session.Reload();
            if (session.Count <= 0)
            {
                SaveSnapshotBrowserState.Exit(panel);
                Traverse.Create(panel).Field("m_infoNeedsRefresh").SetValue(true);
                panel.RefreshSaveSlotInfo();
                PagingManager.Update(panel);
                return;
            }

            int maxPage = Math.Max(0, session.PageCount - 1);
            if (PagingManager.GetPage(panel) > maxPage)
                PagingManager.SetPageDirect(panel, maxPage);

            Traverse.Create(panel).Field("m_infoNeedsRefresh").SetValue(true);
            panel.RefreshSaveSlotInfo();
            PagingManager.Update(panel);
        }

        internal static void UpdatePostfix(SlotSelectionPanel panel)
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.RightArrow))
            {
                PagingManager.ChangePage(panel, 1);
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.LeftArrow))
            {
                PagingManager.ChangePage(panel, -1);
            }
        }

        internal static void RestoreSnapshotAfterVerification(SlotSelectionPanel panel, SaveBackupSnapshotInfo snapshot)
        {
            if (panel == null || snapshot == null)
                return;

            Action continueRestore = delegate { RestoreAndLoadSnapshot(panel, snapshot); };
            int futureCount = SaveBackupService.CountSnapshotsAfter(snapshot);
            if (SnapshotLoadWarningDialog.ShouldShow(futureCount))
            {
                SnapshotLoadWarningDialog.Show(snapshot.Entry, futureCount, continueRestore, null);
                return;
            }

            continueRestore();
        }

        private static bool RefreshSnapshotSaveSlotInfoPrefix(SlotSelectionPanel panel)
        {
            try
            {
                SaveSnapshotBrowserSession session;
                if (!SaveSnapshotBrowserState.TryGet(panel, out session))
                    return true;

                int page = PagingManager.GetPage(panel);
                var t = Traverse.Create(panel);
                var slotInfoList = t.Field("m_slotInfo").GetValue<System.Collections.IList>();

                var buttons = panel.GetComponentsInChildren<SaveSlotButton>(true);
                foreach (var btn in buttons)
                {
                    if (btn != null && (btn.slotNumber == 3 || btn.slotNumber == 4))
                        btn.gameObject.SetActive(false);
                }

                for (int i = 0; i < 3 && i < slotInfoList.Count; i++)
                {
                    var slotInfo = slotInfoList[i];
                    SaveBackupSnapshotInfo snapshot = session.GetSnapshotAt(page, i);
                    if (snapshot != null && snapshot.Entry != null)
                    {
                        ApplySlotInfo(slotInfo, snapshot.Entry);
                    }
                    else
                    {
                        Traverse.Create(slotInfo).Field("m_state").SetValue(SlotSelectionPanel.SlotState.Empty);
                    }
                }

                t.Method("RefreshSlotLabels").GetValue();
                UpdateSaveSlotAuxiliaryControls(panel);
                PagingManager.Update(panel);
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[SnapshotBrowser] Error refreshing snapshot slots: " + ex);
            }

            return false;
        }

        private static void UpdateSaveSlotAuxiliaryControls(SlotSelectionPanel panel)
        {
            if (SaveSnapshotBrowserState.IsActive(panel))
            {
                SaveVerification.UpdateIcons(panel);
                SaveSnapshotSlotControls.UpdateButtons(panel);
                return;
            }

            List<SlotSelectionVisibleSave> visibleSaves = SlotSelectionSaveEntryResolver.Resolve(panel);
            SaveVerification.UpdateIcons(panel, visibleSaves);
            SaveSnapshotSlotControls.UpdateButtons(panel, visibleSaves);
        }

        private static void RefreshVanillaSaveSlotInfo(SlotSelectionPanel panel)
        {
            try
            {
                SaveRegistryCore.ImportStandardVanillaSlotsIfNeeded();

                var panelTraverse = Traverse.Create(panel);
                var slotInfoList = panelTraverse.Field("m_slotInfo").GetValue<System.Collections.IList>();
                if (slotInfoList == null)
                    return;

                slotInfoList.Clear();
                panelTraverse.Field("m_slotBeingLoaded").SetValue(-1);

                SetVanillaScenarioButtonsVisible(panel, false);

                for (int slotNumber = 1; slotNumber <= 3; slotNumber++)
                {
                    object slotInfo = CreateSlotInfo();
                    PopulateVanillaSlotInfo(slotInfo, slotNumber);
                    slotInfoList.Add(slotInfo);
                }

                panelTraverse.Method("RefreshSlotLabels").GetValue();
                ClearInactiveVanillaLabels(panel);
                UpdateSaveSlotAuxiliaryControls(panel);
                PagingManager.Update(panel);
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[RefreshSaveSlotInfo Page0] Error during direct vanilla refresh: " + ex);
            }
        }

        private static object CreateSlotInfo()
        {
            Type slotInfoType = AccessTools.Inner(typeof(SlotSelectionPanel), "SlotInfo");
            return Activator.CreateInstance(slotInfoType, true);
        }

        private static void PopulateVanillaSlotInfo(object slotInfo, int slotNumber)
        {
            var slotTraverse = Traverse.Create(slotInfo);

            SaveEntry imported = SaveRegistryCore.ImportStandardVanillaSlotIfNeeded(slotNumber);
            if (imported != null && File.Exists(DirectoryProvider.EntryPath("Standard", slotNumber, false)))
            {
                ApplySlotInfo(slotInfo, imported);
                return;
            }

            bool exists = false;
            bool corrupted = false;
            if (SaveManager.instance != null)
                SaveManager.instance.DoesSaveExist(slotNumber, out exists, out corrupted);
            else
                exists = File.Exists(SaveRegistryCore.GetVanillaSavePath(slotNumber));

            if (!exists)
            {
                ClearSlotInfo(slotTraverse, SlotSelectionPanel.SlotState.Empty);
                return;
            }

            if (corrupted)
            {
                ClearSlotInfo(slotTraverse, SlotSelectionPanel.SlotState.Corrupt);
                return;
            }

            SaveInfo saveInfo = SaveRegistryCore.ReadVanillaSaveInfo(slotNumber);
            if (saveInfo == null)
            {
                ClearSlotInfo(slotTraverse, SlotSelectionPanel.SlotState.Corrupt);
                return;
            }

            slotTraverse.Field("m_state").SetValue(SlotSelectionPanel.SlotState.Loaded);
            slotTraverse.Field("m_familyName").SetValue(saveInfo.familyName ?? string.Empty);
            slotTraverse.Field("m_daysSurvived").SetValue(saveInfo.daysSurvived);
            slotTraverse.Field("m_diffSetting").SetValue(saveInfo.difficulty);
            slotTraverse.Field("m_rainDiff").SetValue(saveInfo.rainDiff);
            slotTraverse.Field("m_resourceDiff").SetValue(saveInfo.resourceDiff);
            slotTraverse.Field("m_breachDiff").SetValue(saveInfo.breachDiff);
            slotTraverse.Field("m_factionDiff").SetValue(saveInfo.factionDiff);
            slotTraverse.Field("m_moodDiff").SetValue(saveInfo.moodDiff);
            slotTraverse.Field("m_mapSize").SetValue(saveInfo.mapSize);
            slotTraverse.Field("m_fog").SetValue(saveInfo.fog);
            if (slotTraverse.Field("m_dateSaved").FieldExists()) slotTraverse.Field("m_dateSaved").SetValue(FormatDisplayTime(saveInfo.saveTime));
            if (slotTraverse.Field("m_saveTime").FieldExists()) slotTraverse.Field("m_saveTime").SetValue(FormatDisplayTime(saveInfo.saveTime));
        }

        private static void ClearSlotInfo(Traverse slotTraverse, SlotSelectionPanel.SlotState state)
        {
            slotTraverse.Field("m_state").SetValue(state);
            slotTraverse.Field("m_familyName").SetValue(string.Empty);
            slotTraverse.Field("m_daysSurvived").SetValue(0);
            if (slotTraverse.Field("m_dateSaved").FieldExists()) slotTraverse.Field("m_dateSaved").SetValue(string.Empty);
            if (slotTraverse.Field("m_saveTime").FieldExists()) slotTraverse.Field("m_saveTime").SetValue(string.Empty);
        }

        private static void SetVanillaScenarioButtonsVisible(SlotSelectionPanel panel, bool visible)
        {
            SaveSlotButton[] buttons = panel.GetComponentsInChildren<SaveSlotButton>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                SaveSlotButton button = buttons[i];
                if (button != null && (button.slotNumber == 3 || button.slotNumber == 4))
                    button.gameObject.SetActive(visible);
            }
        }

        private static void ClearInactiveVanillaLabels(SlotSelectionPanel panel)
        {
            var panelTraverse = Traverse.Create(panel);
            ClearLabels(panelTraverse.Field("m_slotButtonLabels").GetValue<System.Collections.IList>(), 3);
            ClearLabels(panelTraverse.Field("m_slotDescLabels").GetValue<System.Collections.IList>(), 3);
        }

        private static void ClearLabels(System.Collections.IList labels, int firstIndex)
        {
            if (labels == null)
                return;

            for (int i = firstIndex; i < labels.Count; i++)
            {
                UILabel label = labels[i] as UILabel;
                if (label != null)
                    label.text = string.Empty;
            }
        }

        private static void ApplySlotInfo(object slotInfo, SaveEntry entry)
        {
            SaveInfo info = entry != null ? entry.saveInfo : null;
            var tSlot = Traverse.Create(slotInfo);
            tSlot.Field("m_state").SetValue(SlotSelectionPanel.SlotState.Loaded);
            tSlot.Field("m_familyName").SetValue(info != null ? info.familyName : "Unknown");
            tSlot.Field("m_daysSurvived").SetValue(info != null ? info.daysSurvived : 0);
            tSlot.Field("m_diffSetting").SetValue(info != null ? info.difficulty : 1);
            tSlot.Field("m_rainDiff").SetValue(info != null ? info.rainDiff : 1);
            tSlot.Field("m_resourceDiff").SetValue(info != null ? info.resourceDiff : 1);
            tSlot.Field("m_breachDiff").SetValue(info != null ? info.breachDiff : 1);
            tSlot.Field("m_factionDiff").SetValue(info != null ? info.factionDiff : 1);
            tSlot.Field("m_moodDiff").SetValue(info != null ? info.moodDiff : 1);
            tSlot.Field("m_mapSize").SetValue(info != null ? info.mapSize : 0);
            tSlot.Field("m_fog").SetValue(info != null && info.fog);

            string rawTime = info != null && !string.IsNullOrEmpty(info.saveTime) ? info.saveTime : entry.updatedAt;
            string displayTime = FormatDisplayTime(rawTime);
            if (tSlot.Field("m_dateSaved").FieldExists()) tSlot.Field("m_dateSaved").SetValue(displayTime);
            if (tSlot.Field("m_saveTime").FieldExists()) tSlot.Field("m_saveTime").SetValue(displayTime);
        }

        private static void RefreshSnapshotSlotLabels(SlotSelectionPanel panel)
        {
            try
            {
                SaveSnapshotBrowserSession session;
                if (!SaveSnapshotBrowserState.TryGet(panel, out session))
                    return;

                int page = PagingManager.GetPage(panel);
                var labels = Traverse.Create(panel).Field("m_slotButtonLabels").GetValue<System.Collections.IList>();
                if (labels == null)
                    return;

                for (int i = 0; i < labels.Count && i < 3; i++)
                {
                    UILabel label = labels[i] as UILabel;
                    if (label == null)
                        continue;

                    SaveBackupSnapshotInfo snapshot = session.GetSnapshotAt(page, i);
                    int ordinal = (page * 3) + i + 1;
                    if (snapshot == null || snapshot.Entry == null)
                    {
                        label.text = "Snapshot: Empty";
                        continue;
                    }

                    string rawTime = snapshot.Entry.saveInfo != null && !string.IsNullOrEmpty(snapshot.Entry.saveInfo.saveTime)
                        ? snapshot.Entry.saveInfo.saveTime
                        : snapshot.Entry.updatedAt;
                    label.text = "Snapshot " + ordinal + ":\n" + FormatDisplayTime(rawTime);
                }
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[SnapshotBrowser] Error refreshing snapshot labels: " + ex.Message);
            }
        }

        private static bool HandleSnapshotSlotChosen(SlotSelectionPanel panel)
        {
            try
            {
                if (!panel.m_inputEnabled || (SaveManager.instance != null && SaveManager.instance.isDeleting))
                    return false;

                int chosenSlotIndex = Traverse.Create(panel).Field("m_selectedSlot").GetValue<int>();
                if (chosenSlotIndex < 0 || chosenSlotIndex > 2)
                    return false;

                SaveBackupSnapshotInfo snapshot = SaveSnapshotBrowserState.GetSnapshotAt(panel, chosenSlotIndex);
                if (snapshot == null)
                    return false;

                Action continueLoad = delegate { ConfirmSnapshotLoad(panel, snapshot); };
                int futureCount = SaveBackupService.CountSnapshotsAfter(snapshot);
                if (SnapshotLoadWarningDialog.ShouldShow(futureCount))
                {
                    SnapshotLoadWarningDialog.Show(snapshot.Entry, futureCount, continueLoad, null);
                    return false;
                }

                continueLoad();
                return false;
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[SnapshotBrowser] Snapshot slot chosen error: " + ex);
                return false;
            }
        }

        private static void ConfirmSnapshotLoad(SlotSelectionPanel panel, SaveBackupSnapshotInfo snapshot)
        {
            SlotManifest manifest = snapshot != null ? snapshot.SlotManifest : null;
            SaveVerification.VerificationState state = manifest != null
                ? SaveVerification.Verify(manifest)
                : SaveVerification.VerificationState.Match;

            if (manifest != null && state != SaveVerification.VerificationState.Match)
            {
                SaveDetailsWindow.Show(snapshot.Entry, manifest, state, true, delegate
                {
                    RestoreAndLoadSnapshot(panel, snapshot);
                });
                return;
            }

            RestoreAndLoadSnapshot(panel, snapshot);
        }

        private static void RestoreAndLoadSnapshot(SlotSelectionPanel panel, SaveBackupSnapshotInfo snapshot)
        {
            string error;
            if (!SaveBackupService.RestoreSnapshot(snapshot, out error))
            {
                MessageBox.Show(MessageBoxButtons.Okay_Button,
                    "Failed to restore backup snapshot:\n" + (error ?? "Unknown error"),
                    null,
                    null,
                    null,
                    false);
                return;
            }

            QueueSnapshotLoad(panel, snapshot);
        }

        private static void QueueSnapshotLoad(SlotSelectionPanel panel, SaveBackupSnapshotInfo snapshot)
        {
            SaveSnapshotBrowserSession session;
            if (!SaveSnapshotBrowserState.TryGet(panel, out session))
                return;

            if (snapshot.Entry != null && snapshot.Entry.saveInfo != null)
                ApplyDifficultySettings(snapshot.Entry.saveInfo);

            int slotToLoad;
            if (session.SourceIsVanilla || snapshot.IsVanilla)
            {
                SaveProtectionPatches.LoadGamePatch._forceLoad = true;
                slotToLoad = SlotPagingScope.SaveTypeToSlotNumber(snapshot.SaveType != SaveManager.SaveType.Invalid
                    ? snapshot.SaveType
                    : session.SourceVanillaSaveType);
            }
            else
            {
                PlatformSaveProxy.SetNextLoad(
                    session.SourceTransportSaveType,
                    snapshot.ScenarioId,
                    snapshot.SaveId);
                SaveProtectionPatches.LoadGamePatch._forceLoad = true;
                slotToLoad = session.SourceTransportSlotNumber;
            }

            try
            {
                var t = Traverse.Create(panel);
                var loadingGraphic = t.Field("m_loadingGraphic").GetValue<GameObject>();
                if (loadingGraphic != null)
                    loadingGraphic.SetActive(true);
            }
            catch
            {
            }

            panel.m_inputEnabled = false;
            SaveManager.instance.SetSlotToLoad(slotToLoad);
        }

        private static bool HandleVanillaSlotChosen(SlotSelectionPanel panel)
        {
            try
            {
                var t = Traverse.Create(panel);
                int chosenSlotIndex = t.Field("m_selectedSlot").GetValue<int>();
                if (chosenSlotIndex < 0 || chosenSlotIndex >= 3)
                    return true;

                int vanillaSlotNumber = chosenSlotIndex + 1;
                VanillaMirrorComparisonResult comparison = SaveRegistryCore.CompareStandardVanillaMirror(vanillaSlotNumber);
                if (comparison.Status == VanillaMirrorComparisonStatus.MissingMirror)
                {
                    SaveEntry imported = SaveRegistryCore.WriteStandardVanillaMirrorFromVanilla(
                        comparison,
                        false,
                        "missing-mirror-load");
                    if (imported == null)
                        return true;

                    QueueVerifiedVanillaMirrorLoad(panel, t, chosenSlotIndex, imported);
                    return false;
                }

                if (comparison.Status == VanillaMirrorComparisonStatus.InSync)
                {
                    SaveRegistryCore.EnsureStandardVanillaMirrorManifest(comparison);
                    SaveEntry entry = SaveRegistryCore.ImportStandardVanillaSlotIfNeeded(vanillaSlotNumber);
                    if (entry == null)
                        return true;

                    QueueVerifiedVanillaMirrorLoad(panel, t, chosenSlotIndex, entry);
                    return false;
                }

                if (comparison.Status == VanillaMirrorComparisonStatus.Diverged)
                {
                    ShowVanillaMirrorDivergencePrompt(panel, t, chosenSlotIndex, comparison);
                    return false;
                }

                if (comparison.Status == VanillaMirrorComparisonStatus.MissingVanilla
                    && File.Exists(DirectoryProvider.EntryPath("Standard", vanillaSlotNumber, false)))
                {
                    SaveEntry mirrorEntry = SaveRegistryCore.ImportStandardVanillaSlotIfNeeded(vanillaSlotNumber);
                    if (mirrorEntry == null)
                        return true;

                    VanillaMirrorConflictDialog.ShowMissingVanilla(delegate
                    {
                        QueueVerifiedVanillaMirrorLoad(panel, t, chosenSlotIndex, mirrorEntry);
                    }, null);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[OnSlotChosen vanilla check] Error: " + ex);
                return true;
            }
        }

        private static void ShowVanillaMirrorDivergencePrompt(
            SlotSelectionPanel panel,
            Traverse panelTraverse,
            int chosenSlotIndex,
            VanillaMirrorComparisonResult comparison)
        {
            VanillaMirrorConflictDialog.Show(
                delegate
                {
                    SaveEntry entry = SaveRegistryCore.WriteStandardVanillaMirrorFromVanilla(
                        comparison,
                        true,
                        "load-vanilla-state");
                    if (entry != null)
                        QueueVerifiedVanillaMirrorLoad(panel, panelTraverse, chosenSlotIndex, entry);
                },
                delegate
                {
                    if (!SaveBackupService.BackupVanillaBeforeOverwrite(comparison.SaveType))
                    {
                        MessageBox.Show(
                            MessageBoxButtons.Okay_Button,
                            "The vanilla save could not be backed up, so it was not overwritten.",
                            null,
                            null,
                            null,
                            false);
                        return;
                    }

                    SaveEntry entry = SaveRegistryCore.ImportStandardVanillaSlotIfNeeded(comparison.SlotNumber);
                    if (entry != null)
                        QueueVerifiedVanillaMirrorLoad(panel, panelTraverse, chosenSlotIndex, entry);
                },
                null);
        }

        private static void QueueVerifiedVanillaMirrorLoad(
            SlotSelectionPanel panel,
            Traverse panelTraverse,
            int chosenSlotIndex,
            SaveEntry entry)
        {
            if (entry == null)
                return;

            SlotManifest manifest = SaveRegistryCore.ReadSlotManifest("Standard", entry.absoluteSlot);
            var state = SaveVerification.Verify(manifest);
            Action queue = delegate
            {
                QueueStandardVanillaMirrorLoad(panelTraverse, chosenSlotIndex, SlotPagingScopeResolver.Resolve(panel), (SaveManager.SaveType)entry.absoluteSlot, entry);
            };

            if (manifest != null && state != SaveVerification.VerificationState.Match)
            {
                SaveDetailsWindow.Show(entry, manifest, state, true, queue);
                return;
            }

            queue();
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

                SlotPagingScope scope = SlotPagingScopeResolver.Resolve(panel);
                var entry = scope.FindByUIPosition(chosenSlotIndex + 1, page, 3, true);
                var virtualSaveType = scope.GetTransportSaveType(chosenSlotIndex);

                if (entry == null)
                {
                    int absoluteSlot = scope.GetAbsoluteSlot(page, chosenSlotIndex, 3);
                    MMLog.WriteDebug("--- Player clicked slot " + absoluteSlot + " to start a new game for scenario "
                        + scope.StorageScenarioId + " ---");

                    var created = scope.Create(new SaveCreateOptions { name = "New Game", absoluteSlot = absoluteSlot });
                    if (created != null)
                    {
                        PlatformSaveProxy.SetNextSave(virtualSaveType, scope.StorageScenarioId, created.id);
                        SaveManager.instance.SetCurrentSlot(scope.GetTransportSlotNumber(chosenSlotIndex));
                    }

                    if (scope.IsStandard)
                        return true;

                    BeginDirectScenarioNewGame(scope);
                    return false;
                }

                SlotManifest manifest = scope.ReadManifest(entry.absoluteSlot);
                var state = SaveVerification.Verify(manifest);
                if (state != SaveVerification.VerificationState.Match)
                {
                    SaveDetailsWindow.Show(entry, manifest, state, true, delegate
                    {
                        QueueCustomLoad(t, chosenSlotIndex, scope, virtualSaveType, entry);
                    });
                    return false;
                }

                QueueCustomLoad(t, chosenSlotIndex, scope, virtualSaveType, entry);
                return false;
            }
            catch (Exception ex)
            {
                MMLog.WriteError("OnSlotChosen Prefix patch error: " + ex);
                return true;
            }
        }

        private static void QueueStandardVanillaMirrorLoad(Traverse panelTraverse, int chosenSlotIndex, SlotPagingScope scope, SaveManager.SaveType virtualSaveType, SaveEntry entry)
        {
            VanillaSaveRoute route;
            if (VanillaSaveRouting.TryGetRoute(virtualSaveType, out route))
                SaveRuntimeState.MarkPendingMirroredVanillaLoad(virtualSaveType, route);

            QueueCustomLoad(panelTraverse, chosenSlotIndex, scope, virtualSaveType, entry);
        }

        private static void QueueCustomLoad(Traverse panelTraverse, int chosenSlotIndex, SlotPagingScope scope, SaveManager.SaveType virtualSaveType, SaveEntry entry)
        {
            PlatformSaveProxy.SetNextLoad(virtualSaveType, scope.StorageScenarioId, entry.id);
            ApplyDifficultySettings(entry.saveInfo);

            var loadingGraphic = panelTraverse.Field("m_loadingGraphic").GetValue<GameObject>();
            if (loadingGraphic != null)
                loadingGraphic.SetActive(true);

            SaveProtectionPatches.LoadGamePatch._forceLoad = true;
            SaveManager.instance.SetSlotToLoad(scope.GetTransportSlotNumber(chosenSlotIndex));
        }

        private static void BeginDirectScenarioNewGame(SlotPagingScope scope)
        {
            DifficultyManager.StoreMenuDifficultySettings(1, 1, 1, 1, 1, 0, false);
            if (!string.IsNullOrEmpty(scope.DirectLaunchScene) && LoadingScreen.Instance != null)
            {
                LoadingScreen.Instance.ShowLoadingScreen(scope.DirectLaunchScene);
                MMLog.WriteInfo("[SlotSelectionPatchCoordinator] Direct scenario new game launched. scenarioId="
                    + scope.StorageScenarioId + " scene=" + scope.DirectLaunchScene + ".");
            }
            else
            {
                MMLog.WriteWarning("[SlotSelectionPatchCoordinator] Could not launch direct scenario new game. scenarioId="
                    + scope.StorageScenarioId + " scene=" + (scope.DirectLaunchScene ?? "<none>") + ".");
            }
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
