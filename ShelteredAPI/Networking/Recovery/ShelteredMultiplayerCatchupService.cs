using System;
using System.Collections.Generic;
using ShelteredAPI.Networking.Persistence;
using ShelteredAPI.Networking.World;

namespace ShelteredAPI.Networking.Recovery
{
    internal sealed class ShelteredMultiplayerCatchupService
    {
        private readonly ShelteredMultiplayerSessionCoordinator _coordinator;
        private readonly ShelteredMultiplayerWorldPersistence _persistence;
        private readonly IShelteredWorldEventJournal _journal;
        private readonly ShelteredMultiplayerResyncPolicy _policy;
        private readonly Func<string> _compatibilityHashProvider;

        public ShelteredMultiplayerCatchupService()
            : this(
                ShelteredMultiplayerSessionCoordinator.Instance,
                ShelteredMultiplayerWorldPersistence.Instance,
                ShelteredWorldEvents.Journal,
                new ShelteredMultiplayerResyncPolicy(),
                null)
        {
        }

        internal ShelteredMultiplayerCatchupService(
            ShelteredMultiplayerSessionCoordinator coordinator,
            ShelteredMultiplayerWorldPersistence persistence,
            IShelteredWorldEventJournal journal,
            ShelteredMultiplayerResyncPolicy policy,
            Func<string> compatibilityHashProvider)
        {
            _coordinator = coordinator ?? ShelteredMultiplayerSessionCoordinator.Instance;
            _persistence = persistence;
            _journal = journal;
            _policy = policy ?? new ShelteredMultiplayerResyncPolicy();
            _compatibilityHashProvider = compatibilityHashProvider;
        }

        public ShelteredMultiplayerCatchupPackage BuildHostPackage(ShelteredMultiplayerCatchupRequest request)
        {
            string compatibilityHash = ResolveCompatibilityHash();
            ShelteredMultiplayerSessionContext context = _coordinator.Context;
            ShelteredMultiplayerCatchupDecision decision = _policy.Choose(request, context, _journal, compatibilityHash);

            ShelteredMultiplayerCatchupPackage package = new ShelteredMultiplayerCatchupPackage();
            package.Decision = decision;
            package.HostTick = context != null ? context.WorldTick : 0;

            if (!decision.Accepted)
                return package;

            long replayFromTick = decision.ReplayFromTick;
            if (decision.Kind == ShelteredMultiplayerCatchupDecisionKind.SnapshotAndEvents && _persistence != null)
            {
                package.Snapshot = _persistence.Capture("catchup");
                replayFromTick = package.Snapshot != null ? package.Snapshot.WorldTick : replayFromTick;
            }

            if (_journal != null)
            {
                IList<ShelteredWorldEventRecord> events = _journal.GetSince(replayFromTick);
                for (int i = 0; i < events.Count; i++)
                    package.Events.Add(events[i]);
            }

            return package;
        }

        public ShelteredMultiplayerCatchupApplyResult ApplyClientPackage(ShelteredMultiplayerCatchupPackage package)
        {
            ShelteredMultiplayerCatchupApplyResult result = new ShelteredMultiplayerCatchupApplyResult();
            if (package == null || package.Decision == null || !package.Decision.Accepted)
            {
                result.Success = false;
                result.Error = package != null && package.Decision != null ? package.Decision.Reason : "missing-catchup-package";
                return result;
            }

            try
            {
                if (package.Decision.RequiresSnapshot)
                {
                    if (package.Snapshot == null)
                    {
                        result.Success = false;
                        result.Error = "catchup-snapshot-missing";
                        return result;
                    }

                    string error = string.Empty;
                    if (_persistence == null || !_persistence.Apply(package.Snapshot, "catchup", out error))
                    {
                        result.Success = false;
                        result.Error = error;
                        return result;
                    }
                }

                for (int i = 0; i < package.Events.Count; i++)
                {
                    ShelteredWorldEventRecord record = package.Events[i];
                    if (record == null)
                        continue;
                    if (_journal != null && !_journal.Contains(record.EventId))
                        _journal.Append(record);
                    result.AppliedEventCount++;
                }

                ShelteredMultiplayerSessionContext context = _coordinator.Context;
                if (context != null && context.IsMultiplayerActive)
                    _coordinator.SetWorldTick(package.HostTick, context.WorldDeltaSeconds, "catchup-resume");

                result.ResumeTick = package.HostTick;
                result.Success = true;
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = "catchup-apply-failed: " + ex.Message;
                return result;
            }
        }

        private string ResolveCompatibilityHash()
        {
            try
            {
                if (_compatibilityHashProvider != null)
                    return _compatibilityHashProvider() ?? string.Empty;
            }
            catch
            {
                // GuardrailAllow: SilentCatch - compatibility hash provider failures reject strict matching via empty hash fallback.
            }

            return string.Empty;
        }
    }
}
