using ModAPI.Core;
using System;
using System.Collections.Generic;

namespace ModAPI.Actors.Internal
{
    internal sealed partial class ActorSystemImpl
    {
        private readonly Dictionary<string, AdapterExecutionState> _adapterStates = new Dictionary<string, AdapterExecutionState>(StringComparer.OrdinalIgnoreCase);

        public void Tick(int tickStep, string streamName)
        {
            if (tickStep <= 0) tickStep = 1;

            List<IActorSimulationSystem> systems;
            long tick;
            lock (_sync)
            {
                AdvanceCurrentTickLocked(NowTick());
                _currentTick += tickStep;
                tick = _currentTick;
                systems = new List<IActorSimulationSystem>(_systems);
            }

            string resolvedStream = string.IsNullOrEmpty(streamName) ? "ShelteredAPI.Actors" : streamName;
            ActorSimulationContext context = new ActorSimulationContext(this, this, this, ModRandom.GetStream(resolvedStream), tick);

            for (int i = 0; i < systems.Count; i++)
            {
                IActorSimulationSystem system = systems[i];
                if (system == null) continue;

                try
                {
                    system.Tick(context, tickStep);
                    ReportRecovery(
                        ActorFailureKind.SimulationSystem,
                        ActorEventType.SimulationRecovered,
                        BuiltInOwner,
                        system.SystemId,
                        "Simulation system recovered");
                }
                catch (Exception ex)
                {
                    ReportFailure(
                        ActorFailureKind.SimulationSystem,
                        ActorEventType.SimulationFailed,
                        BuiltInOwner,
                        system.SystemId,
                        "Simulation system '" + system.SystemId + "' failed: " + ex.Message,
                        ex);
                }
            }
        }

        internal void Update()
        {
            EnsureRegistered();

            bool tickAdvanced;
            lock (_sync)
            {
                _updateSequence++;
                tickAdvanced = AdvanceCurrentTickLocked(NowTick());
            }

            bool liveActorChanges = RefreshLiveActors();
            RunAdapters(liveActorChanges, tickAdvanced);
        }

        private void RunAdapters(bool liveActorChanges, bool tickAdvanced)
        {
            List<IActorAdapter> adapters;
            long currentTick;
            long updateSequence;
            lock (_sync)
            {
                adapters = new List<IActorAdapter>(_adapters);
                currentTick = _currentTick;
                updateSequence = _updateSequence;
            }

            bool anyRan = false;

            for (int i = 0; i < adapters.Count; i++)
            {
                IActorAdapter adapter = adapters[i];
                if (adapter == null || string.IsNullOrEmpty(adapter.AdapterId)) continue;

                ActorAdapterContext context;
                lock (_sync)
                {
                    context = BuildAdapterContextLocked(adapter.AdapterId, currentTick, tickAdvanced, liveActorChanges);
                }

                bool shouldRun = context.ShouldRunByDefault;
                IConditionalActorAdapter conditional = adapter as IConditionalActorAdapter;
                if (conditional != null)
                {
                    try
                    {
                        shouldRun = conditional.ShouldSynchronize(context);
                    }
                    catch (Exception ex)
                    {
                        lock (_sync)
                        {
                            NoteAdapterFailureLocked(adapter.AdapterId, currentTick);
                        }

                        ReportFailure(
                            ActorFailureKind.Adapter,
                            ActorEventType.AdapterFailed,
                            BuiltInOwner,
                            adapter.AdapterId,
                            "Actor adapter '" + adapter.AdapterId + "' scheduling predicate failed: " + ex.Message,
                            ex);
                        continue;
                    }
                }

                if (!shouldRun) continue;

                anyRan = true;
                try
                {
                    adapter.Synchronize(this, currentTick);
                    lock (_sync)
                    {
                        NoteAdapterSuccessLocked(adapter.AdapterId, currentTick);
                    }

                    ReportRecovery(
                        ActorFailureKind.Adapter,
                        ActorEventType.AdapterRecovered,
                        BuiltInOwner,
                        adapter.AdapterId,
                        "Actor adapter recovered");
                }
                catch (Exception ex)
                {
                    lock (_sync)
                    {
                        NoteAdapterFailureLocked(adapter.AdapterId, currentTick);
                    }

                    ReportFailure(
                        ActorFailureKind.Adapter,
                        ActorEventType.AdapterFailed,
                        BuiltInOwner,
                        adapter.AdapterId,
                        "Actor adapter '" + adapter.AdapterId + "' failed: " + ex.Message,
                        ex);
                }
            }

            if (!anyRan) return;

            lock (_sync)
            {
                _lastAdapterRunTick = currentTick;
                _lastAdapterRunUpdateSequence = updateSequence;
            }
        }

        private bool AdvanceCurrentTickLocked(long observedTick)
        {
            _lastObservedGameTick = observedTick;
            if (observedTick <= _currentTick) return false;

            _currentTick = observedTick;
            return true;
        }

        private ActorAdapterContext BuildAdapterContextLocked(string adapterId, long currentTick, bool tickAdvanced, bool liveActorChanges)
        {
            AdapterExecutionState state;
            _adapterStates.TryGetValue(adapterId, out state);

            bool hasRegistryChanges = state == null || state.LastSynchronizedRegistryVersion != _registryVersion;
            bool shouldRunByDefault = state == null
                || state.LastSynchronizedUpdateSequence == 0L
                || tickAdvanced
                || liveActorChanges
                || hasRegistryChanges;

            ActorAdapterContext context = new ActorAdapterContext();
            context.AdapterId = adapterId;
            context.CurrentTick = currentTick;
            context.UpdateSequence = _updateSequence;
            context.LastSynchronizedTick = state != null ? state.LastSynchronizedTick : 0L;
            context.LastSuccessfulTick = state != null ? state.LastSuccessfulTick : 0L;
            context.LastSynchronizedUpdateSequence = state != null ? state.LastSynchronizedUpdateSequence : 0L;
            context.LastSynchronizedRegistryVersion = state != null ? state.LastSynchronizedRegistryVersion : 0L;
            context.IsTickAdvanced = tickAdvanced;
            context.HasLiveActorChanges = liveActorChanges;
            context.HasRegistryChanges = hasRegistryChanges;
            context.ShouldRunByDefault = shouldRunByDefault;
            context.ConsecutiveFailureCount = state != null ? state.ConsecutiveFailureCount : 0;
            return context;
        }

        private void NoteAdapterSuccessLocked(string adapterId, long currentTick)
        {
            AdapterExecutionState state = GetOrCreateAdapterStateLocked(adapterId);
            state.LastSynchronizedTick = currentTick;
            state.LastSuccessfulTick = currentTick;
            state.LastSynchronizedUpdateSequence = _updateSequence;
            state.LastSynchronizedRegistryVersion = _registryVersion;
            state.ConsecutiveFailureCount = 0;
        }

        private void NoteAdapterFailureLocked(string adapterId, long currentTick)
        {
            AdapterExecutionState state = GetOrCreateAdapterStateLocked(adapterId);
            state.LastSynchronizedTick = currentTick;
            state.LastSynchronizedUpdateSequence = _updateSequence;
            state.LastSynchronizedRegistryVersion = _registryVersion;
            state.ConsecutiveFailureCount++;
        }

        private AdapterExecutionState GetOrCreateAdapterStateLocked(string adapterId)
        {
            AdapterExecutionState state;
            if (!_adapterStates.TryGetValue(adapterId, out state) || state == null)
            {
                state = new AdapterExecutionState();
                _adapterStates[adapterId] = state;
            }
            return state;
        }

        private sealed class AdapterExecutionState
        {
            public long LastSynchronizedTick;
            public long LastSuccessfulTick;
            public long LastSynchronizedUpdateSequence;
            public long LastSynchronizedRegistryVersion;
            public int ConsecutiveFailureCount;
        }
    }
}
