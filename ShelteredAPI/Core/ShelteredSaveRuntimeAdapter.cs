using System;
using System.Reflection;
using ModAPI.Core;
using ShelteredAPI.Saves;
using ShelteredAPI.Saves.Runtime;

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
            SaveManager.SaveType operationType;
            SaveEntry operationEntry;
            if (SaveRuntimeState.TryGetCurrentSaveOperation(out operationType, out operationEntry))
                return CreateContext(operationEntry);

            SaveManager.SaveType currentType = ResolveCurrentSaveType();
            PlatformSaveProxy.Target pending;
            if (currentType != SaveManager.SaveType.Invalid
                && currentType != SaveManager.SaveType.GlobalData
                && SaveRuntimeState.TryGetPendingSave(currentType, out pending)
                && pending != null)
            {
                SaveEntry pendingEntry = ResolveEntry(pending);
                if (pendingEntry != null)
                    return CreateContext(pendingEntry);
            }

            SaveEntry active = SaveRuntimeState.ActiveCustomSave;
            if (active != null
                && (currentType == SaveManager.SaveType.Invalid
                    || SaveRuntimeState.HasActiveCustomSessionFor(currentType)))
            {
                return CreateContext(active);
            }

            if (currentType == SaveManager.SaveType.Invalid || currentType == SaveManager.SaveType.GlobalData)
                return null;

            IModSaveContext vanillaContext;
            return TryCreateVanillaSaveContext(currentType, out vanillaContext) ? vanillaContext : null;
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

            string scopeId = SaveStorageRouter.NormalizeScenarioId(target.scenarioId);
            return SaveStorageRouter.Get(scopeId, target.saveId);
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

        private static bool TryCreateVanillaSaveContext(SaveManager.SaveType saveType, out IModSaveContext context)
        {
            context = null;

            VanillaSaveRoute route;
            if (!VanillaSaveRouting.TryGetRoute(saveType, out route))
                return false;

            context = CreateVanillaSaveContext(route);
            return true;
        }

        private static IModSaveContext CreateVanillaSaveContext(VanillaSaveRoute route)
        {
            string slotPath = DirectoryProvider.SlotRoot(route.StorageScenarioId, route.AbsoluteSlot, false);
            return new ModSaveContext(slotPath, route.AbsoluteSlot, route.StorageScenarioId, route.SaveId, null);
        }
    }
}
