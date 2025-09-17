using System;
using System.Collections.Generic;
using UnityEngine;

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
    }
}