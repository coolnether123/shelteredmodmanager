using System.Collections.Generic;
using System.Linq;
using ShelteredAPI.Saves.Runtime;
using UnityEngine;

namespace ShelteredAPI.Saves.Paging
{
    internal sealed class SlotSelectionVisibleSave
    {
        public SaveSlotButton Button;
        public int UiSlotIndex;
        public int Page;
        public int DisplaySlotNumber;
        public int ManifestSlotNumber;
        public string StorageScenarioId;
        public SaveManager.SaveType TransportSaveType;
        public int TransportSlotNumber;
        public SlotPagingScope Scope;
        public SaveEntry Entry;

        public bool IsVanillaPage
        {
            get { return Page == 0; }
        }
    }

    internal static class SlotSelectionSaveEntryResolver
    {
        public static List<SlotSelectionVisibleSave> Resolve(SlotSelectionPanel panel)
        {
            List<SlotSelectionVisibleSave> result = new List<SlotSelectionVisibleSave>();
            if (panel == null)
                return result;

            int page = PagingManager.GetPage(panel);
            SlotPagingScope scope = SlotPagingScopeResolver.Resolve(panel);
            SaveEntry[] customSaves = page == 0 ? new SaveEntry[0] : scope.ListSaves();

            List<SaveSlotButton> buttons = panel.GetComponentsInChildren<SaveSlotButton>(true)
                .Where(b => b != null && b.gameObject.activeInHierarchy)
                .OrderByDescending(b => b.transform.localPosition.y)
                .ToList();

            for (int i = 0; i < buttons.Count; i++)
            {
                SaveSlotButton button = buttons[i];
                int displaySlot = page == 0 ? i + 1 : scope.GetAbsoluteSlot(page, i, 3);
                SlotSelectionVisibleSave visible = new SlotSelectionVisibleSave
                {
                    Button = button,
                    UiSlotIndex = i,
                    Page = page,
                    DisplaySlotNumber = displaySlot,
                    ManifestSlotNumber = displaySlot,
                    StorageScenarioId = scope.StorageScenarioId,
                    TransportSaveType = page == 0 ? (SaveManager.SaveType)displaySlot : scope.GetTransportSaveType(i),
                    TransportSlotNumber = page == 0 ? displaySlot : scope.GetTransportSlotNumber(i),
                    Scope = scope
                };

                if (page == 0)
                    PopulateVanillaEntry(visible, displaySlot);
                else
                    PopulateCustomEntry(visible, customSaves, displaySlot);

                // Empty slots remain relevant when their live save was deleted or
                // became unreadable but a backup timeline still exists.
                result.Add(visible);
            }

            return result;
        }

        private static void PopulateVanillaEntry(SlotSelectionVisibleSave visible, int vanillaSlotNumber)
        {
            SaveEntry imported = SaveRegistryCore.ImportStandardVanillaSlotIfNeeded(vanillaSlotNumber);
            if (imported != null && System.IO.File.Exists(DirectoryProvider.EntryPath("Standard", vanillaSlotNumber, false)))
            {
                visible.StorageScenarioId = "Standard";
                visible.ManifestSlotNumber = vanillaSlotNumber;
                visible.TransportSaveType = (SaveManager.SaveType)vanillaSlotNumber;
                visible.TransportSlotNumber = vanillaSlotNumber;
                visible.Entry = imported;
                return;
            }

            string timelineKey;
            SaveManager.SaveType saveType;
            if (!ShelteredAPI.Saves.Backups.SaveBackupService.TryGetVanillaTimelineKey(vanillaSlotNumber, out timelineKey, out saveType))
                return;

            VanillaSaveRoute route;
            if (VanillaSaveRouting.TryGetRoute(saveType, out route))
            {
                visible.StorageScenarioId = route.StorageScenarioId;
                visible.ManifestSlotNumber = route.AbsoluteSlot;
                visible.TransportSaveType = route.SaveType;
                visible.TransportSlotNumber = route.VanillaSlotNumber;
                visible.Entry = SaveRegistryCore.ReadVanillaSaveEntry(
                    route.VanillaSlotNumber,
                    route.StorageScenarioId,
                    route.SaveId,
                    vanillaSlotNumber);
                return;
            }

            SaveInfo saveInfo = SaveRegistryCore.ReadVanillaSaveInfo(vanillaSlotNumber);
            if (saveInfo == null)
                return;

            visible.Entry = new SaveEntry
            {
                id = "vanilla_slot_" + vanillaSlotNumber,
                absoluteSlot = vanillaSlotNumber,
                name = "Slot " + vanillaSlotNumber,
                scenarioId = "Standard",
                saveInfo = saveInfo
            };
        }

        private static void PopulateCustomEntry(SlotSelectionVisibleSave visible, SaveEntry[] customSaves, int absoluteSlot)
        {
            if (customSaves == null)
                return;

            for (int i = 0; i < customSaves.Length; i++)
            {
                SaveEntry entry = customSaves[i];
                if (entry != null && entry.absoluteSlot == absoluteSlot)
                {
                    visible.ManifestSlotNumber = absoluteSlot;
                    visible.Entry = entry;
                    return;
                }
            }
        }
    }
}
