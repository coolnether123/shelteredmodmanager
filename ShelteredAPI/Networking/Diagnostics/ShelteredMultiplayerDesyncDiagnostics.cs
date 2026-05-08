using System;
using System.Collections.Generic;
using ModAPI.Core;
using ShelteredAPI.Networking.Travel;
using ShelteredAPI.Networking.World;

namespace ShelteredAPI.Networking.Diagnostics
{
    internal sealed class ShelteredMultiplayerDesyncDiagnostics
    {
        private readonly ShelteredMultiplayerSessionCoordinator _coordinator;
        private readonly IShelteredWorldEventJournal _journal;
        private readonly IShelteredMapEntityRegistry _mapEntities;
        private readonly IShelteredTravelStateRegistry _travelStates;
        private readonly Func<string> _compatibilityHashProvider;

        public ShelteredMultiplayerDesyncDiagnostics()
            : this(
                ShelteredMultiplayerSessionCoordinator.Instance,
                ShelteredWorldEvents.Journal,
                ShelteredMapEntities.Registry,
                ShelteredExpeditionTravelHookService.Instance.Registry,
                null)
        {
        }

        internal ShelteredMultiplayerDesyncDiagnostics(
            ShelteredMultiplayerSessionCoordinator coordinator,
            IShelteredWorldEventJournal journal,
            IShelteredMapEntityRegistry mapEntities,
            IShelteredTravelStateRegistry travelStates,
            Func<string> compatibilityHashProvider)
        {
            _coordinator = coordinator ?? ShelteredMultiplayerSessionCoordinator.Instance;
            _journal = journal;
            _mapEntities = mapEntities;
            _travelStates = travelStates;
            _compatibilityHashProvider = compatibilityHashProvider;
        }

        public ShelteredMultiplayerDesyncReport BuildReport(string reason)
        {
            ShelteredMultiplayerSessionContext context = _coordinator.Context;
            ShelteredMultiplayerDesyncReport report = new ShelteredMultiplayerDesyncReport();
            report.SessionId = context != null ? context.SessionId : string.Empty;
            report.WorldTick = context != null ? context.WorldTick : 0;
            report.CompatibilityHash = ResolveCompatibilityHash();
            report.RngDigest = ModRandom.GetDeterminismDigest().ToString();
            report.EventJournalCount = _journal != null ? _journal.Count : 0;
            report.MapEntityCount = _mapEntities != null ? _mapEntities.GetAll().Count : 0;
            report.ActiveTravelCount = _travelStates != null ? _travelStates.GetActive().Count : 0;
            report.LatestEventId = ResolveLatestEventId();
            report.BunkerAssignmentSummary = BuildBunkerSummary(context);
            AddWarnings(report);
            return report;
        }

        public string DumpReport(string reason)
        {
            ShelteredMultiplayerDesyncReport report = BuildReport(reason);
            string text = report.ToText();
            try
            {
                MMLog.WriteWithSource(MMLog.LogLevel.Warning, MMLog.LogCategory.Network, "ShelteredAPI.Desync", text);
            }
            catch
            {
                // GuardrailAllow: SilentCatch - desync report generation must still return text if diagnostic logging fails.
            }

            return text;
        }

        private string ResolveLatestEventId()
        {
            if (_journal == null || _journal.Count == 0)
                return string.Empty;

            IList<ShelteredWorldEventRecord> records = _journal.GetSince(0);
            return records.Count > 0 ? records[records.Count - 1].EventId ?? string.Empty : string.Empty;
        }

        private string BuildBunkerSummary(ShelteredMultiplayerSessionContext context)
        {
            if (context == null || context.BunkerAssignments.Length == 0)
                return "none";

            List<string> parts = new List<string>();
            for (int i = 0; i < context.BunkerAssignments.Length; i++)
            {
                ShelteredMultiplayerBunkerAssignmentRecord assignment = context.BunkerAssignments[i];
                if (assignment == null)
                    continue;
                parts.Add("p" + assignment.PlayerId + ":b" + assignment.BunkerOwnerId + ":" + (assignment.IsOnline ? "online" : "offline"));
            }

            return parts.Count == 0 ? "none" : string.Join(",", parts.ToArray());
        }

        private void AddWarnings(ShelteredMultiplayerDesyncReport report)
        {
            try
            {
                List<MMLog.LogEntry> entries = MMLog.GetRecentEntries(MMLog.LogLevel.Warning, 20);
                for (int i = 0; i < entries.Count; i++)
                {
                    MMLog.LogEntry entry = entries[i];
                    if (entry == null)
                        continue;
                    report.RecentWarnings.Add(entry.Timestamp.ToString("o") + " " + entry.Source + ": " + entry.Message);
                }
            }
            catch
            {
                // GuardrailAllow: SilentCatch - diagnostics providers are optional; report generation falls back to empty sections.
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
                // GuardrailAllow: SilentCatch - compatibility hash provider failures are diagnostics-only here.
            }

            return string.Empty;
        }
    }
}
