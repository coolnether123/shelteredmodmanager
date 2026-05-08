using System.Collections.Generic;
using ModAPI.Actors;

namespace ShelteredAPI.Storage
{
    /// <summary>
    /// Mod-facing classification for an item assignment attached to a family member.
    /// The backing item remains in its source IItemStore.
    /// </summary>
    public enum CharacterItemAssignmentKind
    {
        Assigned,
        Reserved,
        CargoReserved,
        Equipped,
        Carried,
        Medical,
        Food,
        Tool,
        Quest
    }

    /// <summary>
    /// Optional semantic slot for a character item assignment.
    /// Slots do not apply equipment effects in this first pass.
    /// </summary>
    public enum CharacterItemSlot
    {
        None,
        MainHand,
        OffHand,
        Backpack,
        Medicine,
        Food,
        Tool
    }

    /// <summary>
    /// Saved metadata describing a character association with items in an existing store.
    /// </summary>
    public sealed class CharacterItemAssignment
    {
        public CharacterItemAssignment()
        {
            AssignmentId = string.Empty;
            MemberDisplayName = string.Empty;
            SourceStoreId = string.Empty;
            SourceStoreName = string.Empty;
            SourceStoreKind = ItemStoreKind.Unknown;
            ItemId = string.Empty;
            Kind = CharacterItemAssignmentKind.Assigned;
            Slot = CharacterItemSlot.None;
        }

        public string AssignmentId { get; set; }
        public ActorId ActorId { get; set; }
        public string MemberDisplayName { get; set; }
        public string SourceStoreId { get; set; }
        public string SourceStoreName { get; set; }
        public ItemStoreKind SourceStoreKind { get; set; }
        public string ItemId { get; set; }
        public int Quantity { get; set; }
        public CharacterItemAssignmentKind Kind { get; set; }
        public CharacterItemSlot Slot { get; set; }

        internal CharacterItemAssignment Clone()
        {
            return new CharacterItemAssignment
            {
                AssignmentId = AssignmentId,
                ActorId = ActorId == null ? null : new ActorId(ActorId.Kind, ActorId.LocalId, ActorId.Domain),
                MemberDisplayName = MemberDisplayName,
                SourceStoreId = SourceStoreId,
                SourceStoreName = SourceStoreName,
                SourceStoreKind = SourceStoreKind,
                ItemId = ItemId,
                Quantity = Quantity,
                Kind = Kind,
                Slot = Slot
            };
        }
    }

    /// <summary>
    /// Character assignment service. Assignments classify existing store contents; they do not move or duplicate items.
    /// </summary>
    public interface ICharacterItemAssignmentService
    {
        CharacterItemAssignment Assign(
            ActorId actorId,
            IItemStore source,
            string itemId,
            int quantity,
            CharacterItemAssignmentKind kind,
            CharacterItemSlot slot);

        CharacterItemAssignment Assign(
            FamilyMember member,
            IItemStore source,
            string itemId,
            int quantity,
            CharacterItemAssignmentKind kind,
            CharacterItemSlot slot);

        bool Unassign(string assignmentId);
        IList<CharacterItemAssignment> GetAssignments(ActorId actorId);
        IList<CharacterItemAssignment> GetAssignments(FamilyMember member);
        IList<CharacterItemAssignment> GetAvailableAssignments(ActorId actorId);
        IList<CharacterItemAssignment> GetAvailableAssignments(FamilyMember member);
        int GetAssignedCount(ActorId actorId, string itemId);
        int GetAssignedCount(FamilyMember member, string itemId);
        int ReleaseAssignmentsForActor(ActorId actorId);
        int ReleaseAssignmentsForMember(FamilyMember member);
    }
}
