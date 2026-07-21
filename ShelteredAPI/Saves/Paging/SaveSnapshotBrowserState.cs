using System;
using System.Collections.Generic;
using ShelteredAPI.Saves.Backups;

namespace ShelteredAPI.Saves.Paging
{
    internal sealed class SaveSnapshotBrowserSession
    {
        public string TimelineKey;
        public SaveEntry SourceEntry;
        public bool SourceIsVanilla;
        public SaveManager.SaveType SourceVanillaSaveType;
        public int SourceSlotNumber;
        public SaveManager.SaveType SourceTransportSaveType;
        public int SourceTransportSlotNumber;
        public int ReturnPage;
        public SaveBackupSnapshotSortOrder SortOrder = SaveBackupSnapshotSortOrder.NewestFirst;
        public List<SaveBackupSnapshotInfo> Snapshots = new List<SaveBackupSnapshotInfo>();

        public int Count
        {
            get { return Snapshots != null ? Snapshots.Count : 0; }
        }

        public int PageCount
        {
            get { return Math.Max(1, (Count + 2) / 3); }
        }

        public void Reload()
        {
            Snapshots = SaveBackupService.ListSnapshots(TimelineKey, SortOrder);
        }

        public SaveBackupSnapshotInfo GetSnapshotAt(int page, int uiSlotIndex)
        {
            int index = (page * 3) + uiSlotIndex;
            return index >= 0 && index < Count ? Snapshots[index] : null;
        }

        public string SortLabel
        {
            get { return SortOrder == SaveBackupSnapshotSortOrder.NewestFirst ? "Newest first" : "Oldest first"; }
        }

        public string ArchiveTitle
        {
            get
            {
                int slot = SourceSlotNumber > 0
                    ? SourceSlotNumber
                    : (SourceEntry != null ? SourceEntry.absoluteSlot : 0);
                return slot > 0 ? "Save Slot " + slot + " Archive" : "Save Archive";
            }
        }
    }

    internal static class SaveSnapshotBrowserState
    {
        private static readonly Dictionary<SlotSelectionPanel, SaveSnapshotBrowserSession> Sessions =
            new Dictionary<SlotSelectionPanel, SaveSnapshotBrowserSession>();

        public static bool IsActive(SlotSelectionPanel panel)
        {
            SaveSnapshotBrowserSession session;
            return panel != null && Sessions.TryGetValue(panel, out session) && session != null;
        }

        public static bool TryGet(SlotSelectionPanel panel, out SaveSnapshotBrowserSession session)
        {
            session = null;
            return panel != null && Sessions.TryGetValue(panel, out session) && session != null;
        }

        public static void Enter(
            SlotSelectionPanel panel,
            string timelineKey,
            SaveEntry sourceEntry,
            bool sourceIsVanilla,
            SaveManager.SaveType sourceVanillaSaveType,
            int sourceSlotNumber,
            SaveManager.SaveType sourceTransportSaveType,
            int sourceTransportSlotNumber)
        {
            if (panel == null || string.IsNullOrEmpty(timelineKey))
                return;

            SaveSnapshotBrowserSession session = new SaveSnapshotBrowserSession
            {
                TimelineKey = timelineKey,
                SourceEntry = sourceEntry,
                SourceIsVanilla = sourceIsVanilla,
                SourceVanillaSaveType = sourceVanillaSaveType,
                SourceSlotNumber = sourceSlotNumber,
                SourceTransportSaveType = sourceTransportSaveType,
                SourceTransportSlotNumber = sourceTransportSlotNumber,
                ReturnPage = PagingManager.GetPage(panel)
            };
            session.Reload();
            Sessions[panel] = session;
            PagingManager.SetPageDirect(panel, 0);
        }

        public static void Exit(SlotSelectionPanel panel)
        {
            if (panel == null)
                return;

            SaveSnapshotBrowserSession session;
            int returnPage = Sessions.TryGetValue(panel, out session) && session != null ? session.ReturnPage : 0;
            Sessions.Remove(panel);
            PagingManager.SetPageDirect(panel, returnPage);
        }

        public static void ToggleSort(SlotSelectionPanel panel)
        {
            SaveSnapshotBrowserSession session;
            if (!TryGet(panel, out session))
                return;

            session.SortOrder = session.SortOrder == SaveBackupSnapshotSortOrder.NewestFirst
                ? SaveBackupSnapshotSortOrder.OldestFirst
                : SaveBackupSnapshotSortOrder.NewestFirst;
            session.Reload();
            PagingManager.SetPageDirect(panel, 0);
        }

        public static SaveBackupSnapshotInfo GetSnapshotAt(SlotSelectionPanel panel, int uiSlotIndex)
        {
            SaveSnapshotBrowserSession session;
            return TryGet(panel, out session)
                ? session.GetSnapshotAt(PagingManager.GetPage(panel), uiSlotIndex)
                : null;
        }
    }
}
