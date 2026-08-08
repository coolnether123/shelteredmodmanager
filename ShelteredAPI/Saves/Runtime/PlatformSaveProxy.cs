using ShelteredAPI.Saves;

namespace ShelteredAPI.Saves.Runtime
{
    internal class PlatformSaveProxy : PlatformSave_Base
    {
        private readonly PlatformSave_Base _inner;
        private readonly PlatformSaveOperationService _saveService;
        private readonly PlatformLoadOperationService _loadService;

        public PlatformSaveProxy(PlatformSave_Base inner)
        {
            _inner = inner;
            _saveService = new PlatformSaveOperationService(inner);
            _loadService = new PlatformLoadOperationService(inner);
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
    }
}
