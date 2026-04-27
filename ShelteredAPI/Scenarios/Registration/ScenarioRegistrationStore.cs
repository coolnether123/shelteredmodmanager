using System;
using System.Collections.Generic;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal interface IScenarioRegistrationStore
    {
        bool TryGet(string id, out ScenarioRecord record);
        ScenarioRecord[] ListRecords();
        CustomScenarioInfo[] ListInfos();
        bool Upsert(ScenarioRecord record, out ScenarioRecord previous);
        bool Remove(string id, out ScenarioRecord removed);
    }

    internal sealed class ScenarioRegistrationStore : IScenarioRegistrationStore
    {
        private readonly Dictionary<string, ScenarioRecord> _registrations = new Dictionary<string, ScenarioRecord>(StringComparer.OrdinalIgnoreCase);
        private readonly object _sync = new object();

        public bool TryGet(string id, out ScenarioRecord record)
        {
            record = null;
            if (string.IsNullOrEmpty(id))
                return false;

            lock (_sync)
            {
                return _registrations.TryGetValue(id, out record);
            }
        }

        public ScenarioRecord[] ListRecords()
        {
            List<ScenarioRecord> items = new List<ScenarioRecord>();
            lock (_sync)
            {
                foreach (KeyValuePair<string, ScenarioRecord> pair in _registrations)
                    items.Add(pair.Value);
            }

            return items.ToArray();
        }

        public CustomScenarioInfo[] ListInfos()
        {
            List<CustomScenarioInfo> items = new List<CustomScenarioInfo>();
            lock (_sync)
            {
                foreach (KeyValuePair<string, ScenarioRecord> pair in _registrations)
                {
                    if (pair.Value != null)
                        items.Add(pair.Value.Info);
                }
            }

            items.Sort(CompareScenarioInfo);
            return items.ToArray();
        }

        public bool Upsert(ScenarioRecord record, out ScenarioRecord previous)
        {
            previous = null;
            if (record == null || record.Info == null || string.IsNullOrEmpty(record.Info.Id))
                return false;

            lock (_sync)
            {
                _registrations.TryGetValue(record.Info.Id, out previous);
                _registrations[record.Info.Id] = record;
                return previous != null;
            }
        }

        public bool Remove(string id, out ScenarioRecord removed)
        {
            removed = null;
            if (string.IsNullOrEmpty(id))
                return false;

            lock (_sync)
            {
                if (!_registrations.TryGetValue(id, out removed))
                    return false;

                _registrations.Remove(id);
                return true;
            }
        }

        private static int CompareScenarioInfo(CustomScenarioInfo left, CustomScenarioInfo right)
        {
            if (object.ReferenceEquals(left, right)) return 0;
            if (left == null) return 1;
            if (right == null) return -1;

            int order = left.Order.CompareTo(right.Order);
            if (order != 0) return order;

            int name = string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
            if (name != 0) return name;

            return string.Compare(left.Id, right.Id, StringComparison.OrdinalIgnoreCase);
        }
    }
}
