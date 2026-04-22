using System;
using ModAPI.Core;
using ModAPI.Events;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    public sealed class ShelteredScenarioRuntimeBindingManager : IScenarioRuntimeBindingService
    {
        private const string SaveGroupName = "CustomScenarioBinding";
        private const string HasLastEditorTickName = "HasLastEditorSaveTick";
        private readonly IScenarioStateManager _stateManager;
        private readonly object _sync = new object();
        private bool _hooked;

        public static ShelteredScenarioRuntimeBindingManager Instance
        {
            get { return ScenarioCompositionRoot.Resolve<ShelteredScenarioRuntimeBindingManager>(); }
        }

        public ScenarioRuntimeBinding CurrentBinding
        {
            get
            {
                return _stateManager.GetRuntimeBinding();
            }
        }

        public int CurrentRevision
        {
            get
            {
                return _stateManager.RuntimeBindingRevision;
            }
        }

        internal ShelteredScenarioRuntimeBindingManager(IScenarioStateManager stateManager)
        {
            _stateManager = stateManager;
        }

        public void EnsureHooked()
        {
            if (_hooked)
                return;

            GameEvents.OnBeforeSave += HandleBeforeSave;
            GameEvents.OnAfterLoad += HandleAfterLoad;
            GameEvents.OnNewGame += HandleNewGame;
            _hooked = true;
        }

        public void SetBinding(ScenarioRuntimeBinding binding)
        {
            _stateManager.SetRuntimeBinding(binding, "runtime-binding", "Binding updated.");
        }

        public void ConvertToNormalSave()
        {
            _stateManager.ConvertRuntimeBindingToNormalSave("runtime-binding", "Converted to normal save.");
        }

        public ScenarioRuntimeBinding GetActiveBindingForStartup()
        {
            ScenarioRuntimeBinding binding = _stateManager.GetRuntimeBinding();
            if (binding == null || binding.IsConvertedToNormalSave)
                return null;
            return binding;
        }

        private void HandleBeforeSave(SaveData data)
        {
            if (data == null || !data.isSaving)
                return;

            ScenarioRuntimeBinding snapshot = CurrentBinding;
            if (snapshot == null || string.IsNullOrEmpty(snapshot.ScenarioId))
                return;

            try
            {
                SaveLoad(data, snapshot);
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ShelteredScenarioRuntimeBinding] Failed to save binding: " + ex.Message);
            }
        }

        private void HandleAfterLoad(SaveData data)
        {
            if (data == null || !data.isLoading)
                return;

            try
            {
                ScenarioRuntimeBinding loaded = Load(data);
                SetBinding(loaded);
                if (loaded != null && loaded.IsConvertedToNormalSave)
                    MMLog.WriteInfo("[ShelteredScenarioRuntimeBinding] Save is converted to normal; scenario logic is disabled.");
            }
            catch
            {
                // Vanilla saves do not contain this additive group. Missing data must be
                // treated as "no scenario binding" so existing saves keep loading normally.
                SetBinding(null);
            }
        }

        private void HandleNewGame()
        {
            // Bindings are save-slot metadata, not global scenario data. A fresh game
            // must start unbound unless the scenario/editor startup flow explicitly
            // creates a new binding later in that flow.
            SetBinding(null);
        }

        private static ScenarioRuntimeBinding Load(SaveData data)
        {
            data.GroupStart(SaveGroupName);
            ScenarioRuntimeBinding binding = new ScenarioRuntimeBinding();
            string scenarioId = string.Empty;
            string versionApplied = string.Empty;
            bool isActive = false;
            bool isConverted = false;
            int dayCreated = 0;
            int lastEditorTick = 0;
            bool hasLastEditorTick = false;

            data.SaveLoad("ScenarioId", ref scenarioId);
            data.SaveLoad("VersionApplied", ref versionApplied);
            data.SaveLoad("IsActive", ref isActive);
            data.SaveLoad("IsConverted", ref isConverted);
            data.SaveLoad("DayCreated", ref dayCreated);
            data.SaveLoad(HasLastEditorTickName, ref hasLastEditorTick);
            data.SaveLoad("LastEditorSaveTick", ref lastEditorTick);
            data.GroupEnd();

            if (string.IsNullOrEmpty(scenarioId))
                return null;

            binding.ScenarioId = scenarioId;
            binding.VersionApplied = versionApplied;
            binding.IsActive = isActive;
            binding.IsConvertedToNormalSave = isConverted;
            binding.DayCreated = dayCreated;
            binding.LastEditorSaveTick = hasLastEditorTick ? new int?(lastEditorTick) : null;
            return binding;
        }

        private static void SaveLoad(SaveData data, ScenarioRuntimeBinding binding)
        {
            data.GroupStart(SaveGroupName);
            string scenarioId = binding.ScenarioId ?? string.Empty;
            string versionApplied = binding.VersionApplied ?? string.Empty;
            bool isActive = binding.IsActive;
            bool isConverted = binding.IsConvertedToNormalSave;
            int dayCreated = binding.DayCreated;
            bool hasLastEditorTick = binding.LastEditorSaveTick.HasValue;
            int lastEditorTick = binding.LastEditorSaveTick.HasValue ? binding.LastEditorSaveTick.Value : 0;

            data.SaveLoad("ScenarioId", ref scenarioId);
            data.SaveLoad("VersionApplied", ref versionApplied);
            data.SaveLoad("IsActive", ref isActive);
            data.SaveLoad("IsConverted", ref isConverted);
            data.SaveLoad("DayCreated", ref dayCreated);
            data.SaveLoad(HasLastEditorTickName, ref hasLastEditorTick);
            data.SaveLoad("LastEditorSaveTick", ref lastEditorTick);
            data.GroupEnd();
        }

    }
}
