using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace ConsoleCommands
{
    public class ExpeditionAttackCommand : ICommand
    {
        public string Name => "expeditionattack";
        public string Description => "Triggers an attack on an expedition party. Usage: expeditionattack [party_id] [num_attackers]";

        public string Execute(string[] args)
        {
            if (ExplorationManager.Instance == null)
            {
                return "ExplorationManager not found.";
            }

            if (args.Length == 0)
            {
                return ListExpeditionParties();
            }

            if (!int.TryParse(args[0], out int partyId))
            {
                return "Invalid party ID.";
            }

            int numAttackers = UnityEngine.Random.Range(1, 5);
            if (args.Length > 1 && int.TryParse(args[1], out int parsedNumAttackers))
            {
                numAttackers = Mathf.Clamp(parsedNumAttackers, 1, 4);
            }

            ExplorationParty targetParty = ExplorationManager.Instance.GetParty(partyId);
            if (targetParty == null)
            {
                return $"Party with ID {partyId} not found.";
            }

            if (EncounterManager.Instance == null)
            {
                return "EncounterManager not found.";
            }
            if (NpcVisitManager.Instance == null)
            {
                return "NpcVisitManager not found.";
            }

            List<PartyMember> partyMembers = new List<PartyMember>();
            for (int i = 0; i < targetParty.membersCount; i++)
            {
                partyMembers.Add(targetParty.GetMember(i));
            }

            List<NpcVisitor> opponents = new List<NpcVisitor>();
            MethodInfo createNpcMethod = typeof(NpcVisitManager).GetMethod("CreateNpcVisitor", BindingFlags.NonPublic | BindingFlags.Instance);

            if (createNpcMethod == null)
            {
                return "Could not find CreateNpcVisitor method.";
            }

            for (int i = 0; i < numAttackers; i++)
            {
                object[] parameters = new object[] { NpcVisitor.NpcType.Breacher, null, new Vector3(targetParty.location.x, targetParty.location.y, 0) };
                NpcVisitor npc = (NpcVisitor)createNpcMethod.Invoke(NpcVisitManager.Instance, parameters);
                opponents.Add(npc);
            }

            EncounterManager.Instance.StartShelterEncounter(partyMembers, opponents, targetParty.currentRegion, targetParty.id);

            return $"Attack started on expedition party {targetParty.id} with {numAttackers} attackers.";
        }

        private string ListExpeditionParties()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Active expedition parties:");

            List<ExplorationParty> parties = ExplorationManager.Instance.GetAllExplorarionParties();
            if (parties.Count == 0)
            {
                return "No active expedition parties found.";
            }

            foreach (ExplorationParty party in parties)
            {
                sb.Append($"Party {party.id}: ");
                for (int i = 0; i < party.membersCount; i++)
                {
                    sb.Append(party.GetMember(i).person.firstName);
                    if (i < party.membersCount - 1)
                    {
                        sb.Append(", ");
                    }
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}