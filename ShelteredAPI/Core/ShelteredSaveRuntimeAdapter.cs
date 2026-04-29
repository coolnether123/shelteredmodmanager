using System;
using System.Reflection;
using ModAPI.Core;
using ModAPI.Hooks;
using ShelteredAPI.Saves;

namespace ShelteredAPI.Core
{
    internal sealed class ShelteredSaveRuntimeAdapter : ISaveRuntimeAdapter
    {
        public string GetCurrentSlotPath()
        {
            IModSaveContext context = GetCurrentSaveContext();
            return context != null ? context.SlotPath : null;
        }

        public int ActiveSlotIndex
        {
            get
            {
                IModSaveContext context = GetCurrentSaveContext();
                return context != null ? context.SlotIndex : -1;
            }
        }

        public IModSaveContext GetCurrentSaveContext()
        {
            SaveEntry active = SaveRuntimeState.ActiveCustomSave;
            if (active != null)
                return CreateContext(active);

            SaveManager.SaveType currentType = ResolveCurrentSaveType();
            if (currentType == SaveManager.SaveType.Invalid || currentType == SaveManager.SaveType.GlobalData)
                return null;

            PlatformSaveProxy.Target pending;
            if (SaveRuntimeState.TryGetPendingSave(currentType, out pending) && pending != null)
            {
                SaveEntry pendingEntry = ResolveEntry(pending);
                if (pendingEntry != null)
                    return CreateContext(pendingEntry);
            }

            int vanillaSlot = SaveTypeToSlotIndex(currentType);
            if (vanillaSlot <= 0)
                return null;

            string slotPath = DirectoryProvider.SlotRoot("Standard", vanillaSlot, false);
            return new ModSaveContext(slotPath, vanillaSlot, "Standard", currentType.ToString(), null);
        }

        public void EnsureRuntimeReady()
        {
            try { SaveManager_Injection_Patch.Inject(SaveManager.instance); }
            catch { }
        }

        public void ResetRuntimeState()
        {
            PlatformSaveProxy.ResetStatus();
        }

        public string GetQuitHeartbeatDetail()
        {
            try
            {
                SaveManager manager = SaveManager.instance;
                if (manager == null)
                    return "SaveManager.instance=null";

                return "isSaving=" + manager.isSaving + ", isLoading=" + manager.isLoading;
            }
            catch (Exception ex)
            {
                return "SaveManager read failed: " + ex.Message;
            }
        }

        private static IModSaveContext CreateContext(SaveEntry entry)
        {
            if (entry == null)
                return null;

            string scopeId = string.IsNullOrEmpty(entry.scenarioId) ? "Standard" : entry.scenarioId;
            string slotPath = DirectoryProvider.SlotRoot(scopeId, entry.absoluteSlot, false);
            return new ModSaveContext(slotPath, entry.absoluteSlot, scopeId, entry.id, entry);
        }

        private static SaveEntry ResolveEntry(PlatformSaveProxy.Target target)
        {
            if (target == null || string.IsNullOrEmpty(target.saveId))
                return null;

            string scopeId = string.IsNullOrEmpty(target.scenarioId) ? "Standard" : target.scenarioId;
            return ExpandedVanillaSaves.IsStandardScenario(scopeId)
                ? ExpandedVanillaSaves.Get(target.saveId)
                : ScenarioSaves.Get(scopeId, target.saveId);
        }

        private static SaveManager.SaveType ResolveCurrentSaveType()
        {
            try
            {
                SaveManager manager = SaveManager.instance;
                if (manager == null)
                    return SaveManager.SaveType.Invalid;

                SaveManager.SaveType slotInUse = ReadSaveTypeField(manager, "m_slotInUse");
                if (slotInUse != SaveManager.SaveType.Invalid)
                    return slotInUse;

                return ReadSaveTypeField(manager, "m_currentType");
            }
            catch
            {
                return SaveManager.SaveType.Invalid;
            }
        }

        private static SaveManager.SaveType ReadSaveTypeField(SaveManager manager, string fieldName)
        {
            if (manager == null)
                return SaveManager.SaveType.Invalid;

            FieldInfo field = manager.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                return SaveManager.SaveType.Invalid;

            object raw = field.GetValue(manager);
            if (raw is SaveManager.SaveType)
                return (SaveManager.SaveType)raw;

            try
            {
                return (SaveManager.SaveType)raw;
            }
            catch
            {
                return SaveManager.SaveType.Invalid;
            }
        }

        private static int SaveTypeToSlotIndex(SaveManager.SaveType saveType)
        {
            int numeric = (int)saveType;
            return numeric >= 1 && numeric <= 3 ? numeric : -1;
        }
    }
}
