using System;
using ModAPI.Core;
using ModAPI.Events;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    public sealed class ShelteredScenarioRuntimeBindingManager
    {
        private const string SaveGroupName = "CustomScenarioBinding";
        private const string HasLastEditorTickName = "HasLastEditorSaveTick";
        private static readonly ShelteredScenarioRuntimeBindingManager _instance = new ShelteredScenarioRuntimeBindingManager();
        private readonly object _sync = new object();
        private ScenarioRuntimeBinding _binding;
        private int _bindingRevision;
        private bool _hooked;

        public static ShelteredScenarioRuntimeBindingManager Instance
        {
            get { return _instance; }
        }

        public ScenarioRuntimeBinding CurrentBinding
        {
            get
            {
                lock (_sync)
                {
                    return CloneBinding(_binding);
                }
            }
        }

        public int CurrentRevision
        {
            get
            {
                lock (_sync)
                {
                    return _bindingRevision;
                }
            }
        }

        private ShelteredScenarioRuntimeBindingManager()
        {
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
            lock (_sync)
            {
                _binding = CloneBinding(binding);
                _bindingRevision++;
            }
        }

        public void ConvertToNormalSave()
        {
            lock (_sync)
            {
                if (_binding == null)
                    return;
                _binding.IsActive = false;
                _binding.IsConvertedToNormalSave = true;
                _bindingRevision++;
            }
        }

        public ScenarioRuntimeBinding GetActiveBindingForStartup()
        {
            lock (_sync)
            {
                if (_binding == null || _binding.IsConvertedToNormalSave)
                    return null;
                return CloneBinding(_binding);
            }
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

        private static ScenarioRuntimeBinding CloneBinding(ScenarioRuntimeBinding binding)
        {
            if (binding == null)
                return null;

            return new ScenarioRuntimeBinding
            {
                ScenarioId = binding.ScenarioId,
                VersionApplied = binding.VersionApplied,
                IsActive = binding.IsActive,
                IsConvertedToNormalSave = binding.IsConvertedToNormalSave,
                DayCreated = binding.DayCreated,
                LastEditorSaveTick = binding.LastEditorSaveTick
            };
        }
    }
}
