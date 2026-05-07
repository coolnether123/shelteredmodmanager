using System.Collections.Generic;
using ModAPI.Actors;

namespace ShelteredAPI.Storage
{
    /// <summary>
    /// Public facade for character-associated item assignments.
    /// Assignments are metadata over existing stores and do not move inventory.
    /// </summary>
    public static class ShelteredCharacterItems
    {
        public static ICharacterItemAssignmentService Service
        {
            get
            {
                CharacterItemAssignmentService.Instance.EnsureRegistered();
                return CharacterItemAssignmentService.Instance;
            }
        }

        public static CharacterItemAssignment Assign(
            ActorId actorId,
            IItemStore source,
            string itemId,
            int quantity,
            CharacterItemAssignmentKind kind,
            CharacterItemSlot slot)
        {
            return Service.Assign(actorId, source, itemId, quantity, kind, slot);
        }

        public static CharacterItemAssignment Assign(
            FamilyMember member,
            IItemStore source,
            string itemId,
            int quantity,
            CharacterItemAssignmentKind kind,
            CharacterItemSlot slot)
        {
            return Service.Assign(member, source, itemId, quantity, kind, slot);
        }

        public static bool Unassign(string assignmentId)
        {
            return Service.Unassign(assignmentId);
        }

        public static IList<CharacterItemAssignment> GetAssignments(ActorId actorId)
        {
            return Service.GetAssignments(actorId);
        }

        public static IList<CharacterItemAssignment> GetAssignments(FamilyMember member)
        {
            return Service.GetAssignments(member);
        }

        public static IList<CharacterItemAssignment> GetAvailableAssignments(ActorId actorId)
        {
            return Service.GetAvailableAssignments(actorId);
        }

        public static IList<CharacterItemAssignment> GetAvailableAssignments(FamilyMember member)
        {
            return Service.GetAvailableAssignments(member);
        }

        public static int GetAssignedCount(ActorId actorId, string itemId)
        {
            return Service.GetAssignedCount(actorId, itemId);
        }

        public static int GetAssignedCount(FamilyMember member, string itemId)
        {
            return Service.GetAssignedCount(member, itemId);
        }

        public static int ReleaseAssignmentsForActor(ActorId actorId)
        {
            return Service.ReleaseAssignmentsForActor(actorId);
        }

        public static int ReleaseAssignmentsForMember(FamilyMember member)
        {
            return Service.ReleaseAssignmentsForMember(member);
        }

        internal static void EnsureRegistered()
        {
            CharacterItemAssignmentService.Instance.EnsureRegistered();
        }
    }
}
