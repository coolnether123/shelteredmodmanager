using ShelteredAPI.Saves;
using System.Collections.Generic;

namespace ShelteredAPI.Saves.Runtime
{
    internal class PlatformSaveProxy : PlatformSave_Base
    {
        internal class Target { public string scenarioId; public string saveId; }

        public static readonly object _nextLoadLock = new object();
        public static readonly object _nextSaveLock = new object();
        
        public static readonly Dictionary<SaveManager.SaveType, Target> NextLoad = new Dictionary<SaveManager.SaveType, Target>();
        public static readonly Dictionary<SaveManager.SaveType, Target> NextSave = new Dictionary<SaveManager.SaveType, Target>();
        public static SaveEntry ActiveCustomSave;

        private readonly PlatformSave_Base _inner;
        private readonly PlatformSaveOperationService _saveService;
        private readonly PlatformLoadOperationService _loadService;

        public PlatformSaveProxy(PlatformSave_Base inner)
        {
            _inner = inner;
            _saveService = new PlatformSaveOperationService(inner);
            _loadService = new PlatformLoadOperationService(inner);
        }

        public static bool NextSaveTargetExists()
        {
            return SaveRuntimeState.HasAnyPendingSave();
        }

        public static KeyValuePair<SaveManager.SaveType, Target> GetNextSaveTargetAndClear()
        {
            return SaveRuntimeState.GetNextSaveTargetAndClear();
        }

        public override bool IsSaving() => _inner.IsSaving();
        public override bool IsLoading() => _inner.IsLoading();
        public override bool IsDeleting() => _inner.IsDeleting();
        public override bool WasSaveError() => _inner.WasSaveError();
        public override void DoesSaveExist(SaveManager.SaveType type, out bool exists, out bool corrupted) => _inner.DoesSaveExist(type, out exists, out corrupted);
        public override void PlatformInit() => _inner.PlatformInit();
        public override void PlatformUpdate() => _inner.PlatformUpdate();
        public override bool PlatformDelete(SaveManager.SaveType type)
        {
            bool routedResult;
            if (SaveDeleteRouter.TryDeleteBySaveType(type, out routedResult))
            {
                return routedResult;
            }

            return _inner.PlatformDelete(type);
        }


        public override bool PlatformSave(SaveManager.SaveType type, byte[] data)
        {
            return _saveService.Save(type, data);
        }

        public override bool PlatformLoad(SaveManager.SaveType type)
        {
            return _loadService.Load(type);
        }

        public override bool PlatformGetLoadedData(out byte[] data)
        {
            return _loadService.GetLoadedData(out data);
        }

        public static void SetNextLoad(SaveManager.SaveType type, string scenarioId, string saveId)
        {
            
            // Safety: Ensure proxy is injected before we register a pending load
            try { SaveManager_Injection_Patch.Inject(SaveManager.instance); } catch { }
            SaveRuntimeState.SetPendingLoad(type, scenarioId, saveId);
        }

        public static void SetNextSave(SaveManager.SaveType type, string scenarioId, string saveId)
        {
            // Safety: match SetNextLoad so pending new-game saves are routed before the loading scene starts.
            try { SaveManager_Injection_Patch.Inject(SaveManager.instance); } catch { }
            SaveRuntimeState.SetPendingSave(type, scenarioId, saveId);
        }

        public static bool TryGetNextSave(SaveManager.SaveType type, out Target target)
        {
            return SaveRuntimeState.TryGetPendingSave(type, out target);
        }

        public static bool ClearNextSave(SaveManager.SaveType type)
        {
            return SaveRuntimeState.ClearPendingSave(type);
        }

        public static bool ClearNextLoad(SaveManager.SaveType type)
        {
            return SaveRuntimeState.ClearPendingLoad(type);
        }

        public static bool ClearNextSaveIfMatches(SaveManager.SaveType type, Target expectedTarget)
        {
            return SaveRuntimeState.ClearPendingSaveIfMatches(type, expectedTarget);
        }

        public static bool ClearNextLoadIfMatches(SaveManager.SaveType type, Target expectedTarget)
        {
            return SaveRuntimeState.ClearPendingLoadIfMatches(type, expectedTarget);
        }

        public static void ResetStatus()
        {
            SaveRuntimeStatus.ResetQuitSaveCompleted();
        }
    }
}
