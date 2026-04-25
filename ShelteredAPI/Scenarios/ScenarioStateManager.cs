using System;
using ModAPI.Events;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioStateManager : IScenarioStateManager
    {
        private const string StateChangedEventName = "ShelteredAPI.Scenarios.StateChanged";
        private readonly object _sync = new object();
        private CustomScenarioState _customScenarioState = CustomScenarioState.None();
        private ScenarioRuntimeBinding _runtimeBinding;
        private int _runtimeBindingRevision;

        public event Action<ScenarioStateSnapshot> StateChanged;

        public int RuntimeBindingRevision
        {
            get
            {
                lock (_sync)
                {
                    return _runtimeBindingRevision;
                }
            }
        }

        public CustomScenarioState GetCustomScenarioState()
        {
            lock (_sync)
            {
                return CloneCustomState(_customScenarioState);
            }
        }

        public void SetCustomScenarioState(CustomScenarioState state, string source, string reason)
        {
            ScenarioStateSnapshot snapshot;
            lock (_sync)
            {
                _customScenarioState = CloneCustomState(state) ?? CustomScenarioState.None();
                snapshot = CreateSnapshot(source, reason);
            }

            Publish(snapshot);
        }

        public ScenarioRuntimeBinding GetRuntimeBinding()
        {
            lock (_sync)
            {
                return CloneBinding(_runtimeBinding);
            }
        }

        public void SetRuntimeBinding(ScenarioRuntimeBinding binding, string source, string reason)
        {
            ScenarioStateSnapshot snapshot;
            lock (_sync)
            {
                _runtimeBinding = CloneBinding(binding);
                _runtimeBindingRevision++;
                snapshot = CreateSnapshot(source, reason);
            }

            Publish(snapshot);
        }

        public void ConvertRuntimeBindingToNormalSave(string source, string reason)
        {
            ScenarioStateSnapshot snapshot;
            lock (_sync)
            {
                if (_runtimeBinding == null)
                    return;

                _runtimeBinding.IsActive = false;
                _runtimeBinding.IsConvertedToNormalSave = true;
                _runtimeBindingRevision++;
                snapshot = CreateSnapshot(source, reason);
            }

            Publish(snapshot);
        }

        private ScenarioStateSnapshot CreateSnapshot(string source, string reason)
        {
            return new ScenarioStateSnapshot
            {
                CustomScenarioState = CloneCustomState(_customScenarioState),
                RuntimeBinding = CloneBinding(_runtimeBinding),
                RuntimeBindingRevision = _runtimeBindingRevision,
                Source = source,
                Reason = reason
            };
        }

        private void Publish(ScenarioStateSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            Action<ScenarioStateSnapshot> handler = StateChanged;
            if (handler != null)
            {
                try
                {
                    handler(snapshot);
                }
                catch
                {
                }
            }

            ModEventBus.Publish(StateChangedEventName, snapshot);
        }

        private static CustomScenarioState CloneCustomState(CustomScenarioState state)
        {
            return state != null ? state.Copy() : null;
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
