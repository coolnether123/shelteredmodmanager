using System;
using System.Collections.Generic;
using UnityEngine;
using ModAPI.Core;

namespace ModAPI
{
    /// <summary>
    /// Helpers to safely access game state without fragile reflection.
    /// </summary>
    public static class GameUtil
    {
        public static bool TryGetShelterRadio(out Obj_Radio radio)
        {
            radio = null;
            try
            {
                if (ObjectManager.Instance == null) return false;
                List<Obj_Base> radios = ObjectManager.Instance.GetObjectsOfType(ObjectManager.ObjectType.RadioTransmitter);
                if (radios == null || radios.Count == 0 || radios[0] == null) return false;
                radio = radios[0] as Obj_Radio;
                return radio != null;
            }
            catch { radio = null; return false; }
        }

        public static bool TryGetParties(out IDictionary<int, ExplorationParty> parties)
        {
            parties = null;
            try
            {
                if (ExplorationManager.Instance == null)
                {
                    parties = (IDictionary<int, ExplorationParty>)new Dictionary<int, ExplorationParty>();
                    return false;
                }
                var list = ExplorationManager.Instance.GetAllExplorarionParties();
                var map = new Dictionary<int, ExplorationParty>();
                if (list != null)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        var p = list[i];
                        if (p != null) map[p.id] = p;
                    }
                }
                parties = map;
                return map.Count > 0;
            }
            catch (Exception ex) { MMLog.WarnOnce("GameUtil.TryGetParties", "Error getting exploration parties: " + ex.Message); parties = (IDictionary<int, ExplorationParty>)new Dictionary<int, ExplorationParty>(); return false; }
        }

        public static bool TryGetMembers(ExplorationParty party, out IList<PartyMember> members)
        {
            var list = new List<PartyMember>();
            members = list;
            if (party == null) return false;
            try
            {
                var count = party.membersCount;
                for (int i = 0; i < count; i++)
                {
                    var m = party.GetMember(i);
                    if (m != null) list.Add(m);
                }
                return list.Count > 0;
            }
            catch (Exception ex) { MMLog.WarnOnce("GameUtil.TryGetMembers", "Error accessing party members: " + ex.Message); return false; }
        }

        /// <summary>
        /// Returns a stable, unique identifier for a save slot. 
        /// For vanilla saves, returns Slot1, Slot2, etc. 
        /// For custom saves, returns a unique ID (e.g., Standard_Guid).
        /// Use this to key your mod-specific persistence when not using the built-in SaveSystem.
        /// </summary>
        public static string GetSaveSlotKey(SaveManager.SaveType type)
        {
            if (type == SaveManager.SaveType.GlobalData) return "Global";
            if (type == SaveManager.SaveType.Invalid) return "Invalid";

            // 1. Check if we are currently LOADING a custom save (intercepted by proxy)
            if (ModAPI.Hooks.PlatformSaveProxy.NextLoad.ContainsKey(type))
            {
                var target = ModAPI.Hooks.PlatformSaveProxy.NextLoad[type];
                return string.Format("{0}_{1}", target.scenarioId, target.saveId);
            }

            // 2. Check if we currently HAVE an active custom save loaded
            var active = ModAPI.Hooks.PlatformSaveProxy.ActiveCustomSave;
            if (active != null)
            {
                string scenario = string.IsNullOrEmpty(active.scenarioId) ? "Standard" : active.scenarioId;
                return string.Format("{0}_{1}", scenario, active.id);
            }

            // 3. Fallback to vanilla slot name for non-modded slots
            return type.ToString();
        }
    }
}