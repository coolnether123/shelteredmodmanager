using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ModAPI.Saves
{
    public static class SlotReservationManager
    {
        private static SlotReservationMap _cache;
        private static readonly object _lock = new object();

        public static SlotReservation GetSlotReservation(int physicalSlot)
        {
            var map = Load();
            foreach (var r in map.reserved)
                if (r.physicalSlot == physicalSlot) return r;
            return new SlotReservation { physicalSlot = physicalSlot, usage = SaveSlotUsage.Standard, scenarioId = null };
        }

        public static void SetSlotReservation(int physicalSlot, SaveSlotUsage usage, string scenarioId)
        {
            var map = Load();
            var list = new List<SlotReservation>(map.reserved);
            var idx = list.FindIndex(r => r.physicalSlot == physicalSlot);
            if (idx >= 0) list.RemoveAt(idx);
            list.Add(new SlotReservation { physicalSlot = physicalSlot, usage = usage, scenarioId = scenarioId });
            map.reserved = list.ToArray();
            Save(map);
            try { Events.RaiseReservationChanged(physicalSlot, GetSlotReservation(physicalSlot)); } catch { }
            MMLog.WriteDebug($"SetSlotReservation slot={physicalSlot} usage={usage} scenario={scenarioId}");
        }

        public static void ClearSlotReservation(int physicalSlot)
        {
            var map = Load();
            var list = new List<SlotReservation>(map.reserved);
            var idx = list.FindIndex(r => r.physicalSlot == physicalSlot);
            if (idx >= 0) list.RemoveAt(idx);
            map.reserved = list.ToArray();
            Save(map);
            try { Events.RaiseReservationChanged(physicalSlot, GetSlotReservation(physicalSlot)); } catch { }
            MMLog.WriteDebug($"ClearSlotReservation slot={physicalSlot}");
        }

        public static SlotReservationMap Load()
        {
            lock (_lock)
            {
                if (_cache != null) return _cache;
                var path = DirectoryProvider.GlobalReservationPath;
                try
                {
                    if (File.Exists(path))
                    {
                        var json = File.ReadAllText(path);
                        var map = JsonUtility.FromJson<SlotReservationMap>(json);
                        _cache = map ?? new SlotReservationMap();
                    }
                    else
                    {
                        _cache = new SlotReservationMap();
                    }
                }
                catch (System.Exception ex)
                {
                    MMLog.Write("SlotReservation load error: " + ex.Message);
                    _cache = new SlotReservationMap();
                }
                MMLog.WriteDebug("SlotReservation loaded");
                return _cache;
            }
        }

        public static void Save(SlotReservationMap map)
        {
            lock (_lock)
            {
                _cache = map;
                try
                {
                    var json = JsonUtility.ToJson(map, true);
                    File.WriteAllText(DirectoryProvider.GlobalReservationPath, json);
                }
                catch (System.Exception ex)
                {
                    MMLog.Write("SlotReservation save error: " + ex.Message);
                }
                MMLog.WriteDebug("SlotReservation saved");
            }
        }
    }
}
