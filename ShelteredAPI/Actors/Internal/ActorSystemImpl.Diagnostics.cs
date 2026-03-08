using ModAPI.Core;
using ModAPI.Util;
using System;
using System.Collections.Generic;

namespace ModAPI.Actors.Internal
{
    internal sealed partial class ActorSystemImpl
    {
        private const int MaxFailureRecords = 256;

        private readonly Dictionary<string, ActorFailureRecord> _failureRecords = new Dictionary<string, ActorFailureRecord>(StringComparer.OrdinalIgnoreCase);
        private readonly LinkedList<string> _failureOrder = new LinkedList<string>();

        private long _updateSequence;
        private long _registryVersion;
        private long _lastObservedGameTick;
        private long _lastLiveRefreshTick;
        private long _lastLiveRefreshUpdateSequence;
        private long _lastAdapterRunTick;
        private long _lastAdapterRunUpdateSequence;

        public ActorRuntimeSnapshot GetRuntimeSnapshot()
        {
            ActorRuntimeSnapshot snapshot = new ActorRuntimeSnapshot();
            lock (_sync)
            {
                snapshot.CurrentTick = _currentTick;
                snapshot.UpdateSequence = _updateSequence;
                snapshot.RegistryVersion = _registryVersion;
                snapshot.LastObservedGameTick = _lastObservedGameTick;
                snapshot.LastLiveRefreshTick = _lastLiveRefreshTick;
                snapshot.LastLiveRefreshUpdateSequence = _lastLiveRefreshUpdateSequence;
                snapshot.LastAdapterRunTick = _lastAdapterRunTick;
                snapshot.LastAdapterRunUpdateSequence = _lastAdapterRunUpdateSequence;
                snapshot.ActorCount = _records.Count;
                snapshot.ComponentCount = CountComponentsLocked();
                snapshot.BindingCount = CountBindingsLocked();
                snapshot.AdapterCount = _adapters.Count;
                snapshot.SimulationSystemCount = _systems.Count;
                snapshot.RecentEventCount = _recentEvents.Count;
                snapshot.ActiveFailureCount = CountActiveFailuresLocked();
            }
            return snapshot;
        }

        public IReadOnlyList<ActorFailureRecord> GetFailureRecords()
        {
            List<ActorFailureRecord> records = new List<ActorFailureRecord>();
            lock (_sync)
            {
                foreach (ActorFailureRecord record in _failureRecords.Values)
                {
                    if (record == null) continue;
                    records.Add(record.Clone());
                }
            }

            records.Sort(delegate(ActorFailureRecord left, ActorFailureRecord right)
            {
                int byActive = right.IsActive.CompareTo(left.IsActive);
                if (byActive != 0) return byActive;

                int byTick = right.LastTick.CompareTo(left.LastTick);
                if (byTick != 0) return byTick;

                return string.Compare(left.FailureKey, right.FailureKey, StringComparison.OrdinalIgnoreCase);
            });
            return records.ToReadOnlyList();
        }

        private void MarkRegistryChangedLocked()
        {
            _registryVersion++;
        }

        private void ResetRuntimeDiagnosticsLocked()
        {
            _failureRecords.Clear();
            _failureOrder.Clear();
            _adapterStates.Clear();
            _updateSequence = 0L;
            _registryVersion = 0L;
            _lastObservedGameTick = 0L;
            _lastLiveRefreshTick = 0L;
            _lastLiveRefreshUpdateSequence = 0L;
            _lastAdapterRunTick = 0L;
            _lastAdapterRunUpdateSequence = 0L;
        }

        private void ReportFailure(
            ActorFailureKind kind,
            ActorEventType eventType,
            string sourceModId,
            string subjectId,
            string message,
            Exception ex)
        {
            ActorFailureRecord record = null;
            bool shouldPublish;
            lock (_sync)
            {
                shouldPublish = RecordFailureLocked(kind, sourceModId, subjectId, message, ex, out record);
            }

            if (!shouldPublish || record == null) return;

            string formatted = FormatFailureMessage(record);
            Publish(eventType, sourceModId, null, null, formatted);
            MMLog.WriteWarning("[ActorSystem] " + formatted);
        }

        private void ReportRecovery(
            ActorFailureKind kind,
            ActorEventType eventType,
            string sourceModId,
            string subjectId,
            string message)
        {
            ActorFailureRecord record = null;
            bool recovered;
            lock (_sync)
            {
                recovered = TryRecoverFailureLocked(kind, sourceModId, subjectId, message, out record);
            }

            if (!recovered || record == null) return;

            string formatted = FormatRecoveryMessage(record);
            Publish(eventType, sourceModId, null, null, formatted);
            MMLog.WriteInfo("[ActorSystem] " + formatted);
        }

        private bool RecordFailureLocked(
            ActorFailureKind kind,
            string sourceModId,
            string subjectId,
            string message,
            Exception ex,
            out ActorFailureRecord record)
        {
            string failureKey = BuildFailureKey(kind, sourceModId, subjectId);
            if (!_failureRecords.TryGetValue(failureKey, out record) || record == null)
            {
                record = new ActorFailureRecord();
                record.FailureKey = failureKey;
                record.FailureKind = kind;
                record.SubjectId = NormalizeKey(subjectId);
                record.SourceModId = sourceModId ?? string.Empty;
                record.Message = message ?? string.Empty;
                record.ExceptionType = ex != null ? ex.GetType().FullName : string.Empty;
                record.FirstTick = _currentTick;
                record.LastTick = _currentTick;
                record.Count = 1;
                record.SuppressedCount = 0;
                record.IsActive = true;
                _failureRecords[failureKey] = record;
                TouchFailureOrderLocked(failureKey);
                PruneFailureRecordsLocked();
                return true;
            }

            record.LastTick = _currentTick;
            record.Count++;
            record.Message = message ?? string.Empty;
            record.ExceptionType = ex != null ? ex.GetType().FullName : string.Empty;
            record.IsActive = true;
            TouchFailureOrderLocked(failureKey);

            if (ShouldReportFailureCount(record.Count))
                return true;

            record.SuppressedCount++;
            return false;
        }

        private bool TryRecoverFailureLocked(
            ActorFailureKind kind,
            string sourceModId,
            string subjectId,
            string message,
            out ActorFailureRecord record)
        {
            string failureKey = BuildFailureKey(kind, sourceModId, subjectId);
            if (!_failureRecords.TryGetValue(failureKey, out record) || record == null || !record.IsActive)
                return false;

            record.IsActive = false;
            record.LastTick = _currentTick;
            if (!string.IsNullOrEmpty(message))
                record.Message = message;
            TouchFailureOrderLocked(failureKey);
            return true;
        }

        private static bool ShouldPublishComponentWrite(ActorComponentWriteResult result, string message)
        {
            if (string.Equals(message, "Component unchanged", StringComparison.Ordinal))
                return false;

            return result == ActorComponentWriteResult.Added
                || result == ActorComponentWriteResult.Updated
                || result == ActorComponentWriteResult.Replaced
                || result == ActorComponentWriteResult.Merged;
        }

        private static bool ShouldReportFailureCount(int count)
        {
            return count <= 1 || IsPowerOfTwo(count);
        }

        private static bool IsPowerOfTwo(int value)
        {
            return value > 0 && (value & (value - 1)) == 0;
        }

        private static string BuildFailureKey(ActorFailureKind kind, string sourceModId, string subjectId)
        {
            return ((int)kind).ToString()
                + "|"
                + NormalizeKey(sourceModId)
                + "|"
                + NormalizeKey(subjectId);
        }

        private static string FormatFailureMessage(ActorFailureRecord record)
        {
            string subject = string.IsNullOrEmpty(record.SubjectId) ? "runtime" : record.SubjectId;
            string message = string.IsNullOrEmpty(record.Message) ? "Actor runtime failure" : record.Message;
            if (record.Count > 1)
                message += " (occurrence " + record.Count + ")";
            return subject + ": " + message;
        }

        private static string FormatRecoveryMessage(ActorFailureRecord record)
        {
            string subject = string.IsNullOrEmpty(record.SubjectId) ? "runtime" : record.SubjectId;
            return subject + ": " + (string.IsNullOrEmpty(record.Message) ? "Recovered" : record.Message);
        }

        private int CountComponentsLocked()
        {
            int count = 0;
            foreach (Dictionary<string, ActorComponentSlot> map in _components.Values)
            {
                if (map == null) continue;
                count += map.Count;
            }
            return count;
        }

        private int CountBindingsLocked()
        {
            int count = 0;
            foreach (List<ActorBinding> bindings in _bindings.Values)
            {
                if (bindings == null) continue;
                count += bindings.Count;
            }
            return count;
        }

        private int CountActiveFailuresLocked()
        {
            int count = 0;
            foreach (ActorFailureRecord record in _failureRecords.Values)
            {
                if (record != null && record.IsActive)
                    count++;
            }
            return count;
        }

        private void TouchFailureOrderLocked(string failureKey)
        {
            LinkedListNode<string> node = _failureOrder.Find(failureKey);
            if (node != null)
                _failureOrder.Remove(node);
            _failureOrder.AddLast(failureKey);
        }

        private void PruneFailureRecordsLocked()
        {
            while (_failureRecords.Count > MaxFailureRecords && _failureOrder.First != null)
            {
                LinkedListNode<string> cursor = _failureOrder.First;
                LinkedListNode<string> target = null;

                while (cursor != null)
                {
                    ActorFailureRecord candidate;
                    if (_failureRecords.TryGetValue(cursor.Value, out candidate) && candidate != null && !candidate.IsActive)
                    {
                        target = cursor;
                        break;
                    }
                    cursor = cursor.Next;
                }

                if (target == null)
                    target = _failureOrder.First;

                _failureOrder.Remove(target);
                _failureRecords.Remove(target.Value);
            }
        }
    }
}
