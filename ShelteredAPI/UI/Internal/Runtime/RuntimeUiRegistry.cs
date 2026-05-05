using System;
using System.Collections.Generic;
using UnityEngine;
using ShelteredAPI.Content;
namespace ShelteredAPI.UI.Internal.Runtime{
    internal static class RuntimeUiRegistry
    {
        private static readonly object Sync = new object();
        private static readonly Dictionary<string, RuntimeUiPanelRecord> Panels =
            new Dictionary<string, RuntimeUiPanelRecord>(StringComparer.OrdinalIgnoreCase);

        public static bool Contains(string panelId)
        {
            if (string.IsNullOrEmpty(panelId))
                return false;

            lock (Sync)
                return Panels.ContainsKey(panelId);
        }

        public static void Register(RuntimeUiPanelRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.PanelId))
                throw new ArgumentException("Runtime UI panel records require a stable PanelId.");

            Close(record.PanelId);

            lock (Sync)
                Panels[record.PanelId] = record;
        }

        public static bool TryGet(string panelId, out RuntimeUiPanelRecord record)
        {
            record = null;
            if (string.IsNullOrEmpty(panelId))
                return false;

            lock (Sync)
                return Panels.TryGetValue(panelId, out record);
        }

        public static RuntimeUiPanelRecord[] Snapshot()
        {
            lock (Sync)
            {
                RuntimeUiPanelRecord[] result = new RuntimeUiPanelRecord[Panels.Count];
                Panels.Values.CopyTo(result, 0);
                return result;
            }
        }

        public static void RequestRebindAll()
        {
            RuntimeUiPanelRecord[] records = Snapshot();
            for (int i = 0; i < records.Length; i++)
            {
                if (records[i] != null)
                    records[i].RebindRequested = true;
            }
        }

        public static void Close(string panelId)
        {
            RuntimeUiPanelRecord record;
            if (string.IsNullOrEmpty(panelId))
                return;

            lock (Sync)
            {
                if (!Panels.TryGetValue(panelId, out record))
                    return;
                Panels.Remove(panelId);
            }

            CloseRecord(record);
        }

        public static void CloseOwner(string ownerId)
        {
            if (string.IsNullOrEmpty(ownerId))
                return;

            List<RuntimeUiPanelRecord> records = new List<RuntimeUiPanelRecord>();
            lock (Sync)
            {
                List<string> keys = new List<string>();
                foreach (KeyValuePair<string, RuntimeUiPanelRecord> pair in Panels)
                {
                    RuntimeUiPanelRecord record = pair.Value;
                    if (record != null && string.Equals(record.OwnerId, ownerId, StringComparison.OrdinalIgnoreCase))
                    {
                        records.Add(record);
                        keys.Add(pair.Key);
                    }
                }

                for (int i = 0; i < keys.Count; i++)
                    Panels.Remove(keys[i]);
            }

            for (int i = 0; i < records.Count; i++)
                CloseRecord(records[i]);
        }

        public static void CloseAll()
        {
            RuntimeUiPanelRecord[] records;
            lock (Sync)
            {
                records = new RuntimeUiPanelRecord[Panels.Count];
                Panels.Values.CopyTo(records, 0);
                Panels.Clear();
            }

            for (int i = 0; i < records.Length; i++)
                CloseRecord(records[i]);
        }

        private static void CloseRecord(RuntimeUiPanelRecord record)
        {
            if (record == null)
                return;

            UIRuntimeServiceHelper.Run("RuntimeUiRegistry.Close", delegate
            {
                if (record.Close != null)
                    record.Close(record);

                if (record.Root != null)
                    UnityEngine.Object.Destroy(record.Root);
                record.Root = null;
            });
        }
    }
}
