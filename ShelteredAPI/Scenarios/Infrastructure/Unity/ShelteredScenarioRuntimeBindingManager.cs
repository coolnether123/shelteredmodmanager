using System;
using ModAPI.Core;
using ShelteredAPI.Events;
using ModAPI.Scenarios;

using ShelteredAPI.Content;
using ShelteredAPI.Saves;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Domain.Runtime;
namespace ShelteredAPI.Scenarios.Infrastructure.Unity{
    internal sealed class ShelteredScenarioRuntimeBindingManager : IScenarioRuntimeBindingService
    {
        private readonly IScenarioStateManager _stateManager;
        private readonly IScenarioRuntimeBindingPersistence _persistence;
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

        internal ShelteredScenarioRuntimeBindingManager(
            IScenarioStateManager stateManager,
            IScenarioRuntimeBindingPersistence persistence,
            IVanillaScenarioRuntime vanillaRuntime)
        {
            _stateManager = stateManager;
            _persistence = persistence;
            _vanillaRuntime = vanillaRuntime;
        }

        private readonly IVanillaScenarioRuntime _vanillaRuntime;

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
                _persistence.Save(data, snapshot);
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
                ScenarioRuntimeBinding loaded = _persistence.Load(data);
                InvalidateUnavailableCompletionCarrier(loaded);
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

        private void InvalidateUnavailableCompletionCarrier(ScenarioRuntimeBinding binding)
        {
            if (binding == null || !binding.ScenarioQuestInstanceId.HasValue || _vanillaRuntime == null)
                return;

            QuestInstance instance;
            string reason;
            if (_vanillaRuntime.TryGetQuestInstance(binding.ScenarioQuestInstanceId.Value, out instance, out reason)
                && instance != null
                && instance.definition != null
                && instance.definition.IsScenario()
                && (string.IsNullOrEmpty(binding.ScenarioId)
                    || string.Equals(instance.definition.id, binding.ScenarioId, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            binding.ScenarioQuestInstanceId = null;
            MMLog.WriteInfo("[ShelteredScenarioRuntimeBinding] Invalidated unavailable persisted Scenario QuestInstance. "
                + (reason ?? "Carrier did not match the active binding."));
        }

    }
}
