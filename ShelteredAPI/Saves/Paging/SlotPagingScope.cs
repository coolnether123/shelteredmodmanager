using System.IO;
using ModAPI.Core;
using ShelteredAPI.Saves;
using ShelteredAPI.Saves.Runtime;

namespace ShelteredAPI.Saves.Paging
{
    internal sealed class SlotPagingScope
    {
        private const string StandardScenarioId = "Standard";
        private readonly SaveRegistryCore _registry;

        public SlotPagingScope(string storageScenarioId, SaveManager.SaveType transportSaveType, int firstExpandedSlot, string directLaunchScene)
        {
            StorageScenarioId = string.IsNullOrEmpty(storageScenarioId) ? StandardScenarioId : storageScenarioId;
            TransportSaveType = transportSaveType;
            FirstExpandedSlot = firstExpandedSlot <= 0 ? 1 : firstExpandedSlot;
            DirectLaunchScene = directLaunchScene;
            IsStandard = ExpandedVanillaSaves.IsStandardScenario(StorageScenarioId);
            _registry = IsStandard ? (SaveRegistryCore)ExpandedVanillaSaves.Instance : ScenarioSaves.GetTrustedRegistry(StorageScenarioId);
        }

        public string StorageScenarioId { get; private set; }
        public SaveManager.SaveType TransportSaveType { get; private set; }
        public int FirstExpandedSlot { get; private set; }
        public bool IsStandard { get; private set; }
        public string DirectLaunchScene { get; private set; }

        public SaveEntry[] ListSaves()
        {
            return _registry.ListSaves();
        }

        public int CountSaves()
        {
            return _registry.CountSaves();
        }

        public int GetMaxSlot()
        {
            return _registry.GetMaxSlot();
        }

        public SaveEntry GetBySlot(int absoluteSlot)
        {
            return absoluteSlot > 0 ? _registry.GetSaveBySlot(absoluteSlot) : null;
        }

        public SaveEntry Create(SaveCreateOptions options)
        {
            return _registry.CreateSave(options);
        }

        public bool DeleteBySlot(int absoluteSlot)
        {
            return absoluteSlot > 0 && _registry.DeleteBySlot(absoluteSlot);
        }

        public bool EntryFileExists(SaveEntry entry)
        {
            return entry != null && File.Exists(DirectoryProvider.EntryPath(StorageScenarioId, entry.absoluteSlot));
        }

        public int GetAbsoluteSlot(int page, int zeroBasedSlotIndex, int pageSize)
        {
            return FirstExpandedSlot + ((page - 1) * pageSize) + zeroBasedSlotIndex;
        }

        public SaveEntry FindByUIPosition(int physicalSlot, int page, int pageSize, bool mustExist)
        {
            if (page <= 0 || physicalSlot <= 0)
                return null;

            int absoluteSlot = GetAbsoluteSlot(page, physicalSlot - 1, pageSize);
            SaveEntry entry = GetBySlot(absoluteSlot);
            if (entry == null)
            {
                MMLog.WriteDebug("[SlotPagingScope] No entry found for scenario=" + StorageScenarioId
                    + " slot=" + absoluteSlot + " page=" + page + " physical=" + physicalSlot + ".");
                return null;
            }

            bool exists = EntryFileExists(entry);
            MMLog.WriteDebug("[SlotPagingScope] Entry lookup scenario=" + StorageScenarioId
                + " slot=" + absoluteSlot + " exists=" + exists + ".");
            return !mustExist || exists ? entry : null;
        }

        public SaveManager.SaveType GetTransportSaveType(int zeroBasedSlotIndex)
        {
            return IsStandard ? (SaveManager.SaveType)(zeroBasedSlotIndex + 1) : TransportSaveType;
        }

        public int GetTransportSlotNumber(int zeroBasedSlotIndex)
        {
            if (IsStandard)
                return zeroBasedSlotIndex + 1;

            VanillaSaveRoute route;
            return VanillaSaveRouting.TryGetRoute(TransportSaveType, out route)
                ? route.VanillaSlotNumber
                : 1;
        }

        public SlotManifest ReadManifest(int absoluteSlot)
        {
            return SaveRegistryCore.ReadSlotManifest(StorageScenarioId, absoluteSlot);
        }

    }
}
