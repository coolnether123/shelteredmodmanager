using System;
using System.Collections.Generic;
using ModAPI.Actors;
using ShelteredAPI.Networking.Trade;
using ShelteredAPI.Storage;

namespace ShelteredAPI.Networking.Tests
{
    internal static class ShelteredTradeCargoReservationTests
    {
        public static void Register(List<TestCase> tests)
        {
            tests.Add(new TestCase("Trade cargo reservation uses store-backed reservations", StoreBackedReservationHoldsAndReleasesCargo));
            tests.Add(new TestCase("Trade cargo reservation creates soft assignment when store cannot reserve", SoftReservationUsesAssignmentMetadata));
            tests.Add(new TestCase("Character item cargo reservation prevents over assignment", SoftReservationPreventsOverAssignment));
        }

        private static void StoreBackedReservationHoldsAndReleasesCargo()
        {
            FakeReservableStore source = new FakeReservableStore("source");
            source.SetCount("water", 5);
            FakeItemStore target = new FakeItemStore("target");
            ShelteredMultiplayerTradeCargoReservationService service =
                new ShelteredMultiplayerTradeCargoReservationService(Resolve(source, target), new FakeAssignmentService());

            ShelteredTradeCargoReservationResult result = service.Reserve(CreateTrade(3));

            TestAssert.True(result.Success, "Reservation should succeed.");
            TestAssert.Equal(2, source.GetAvailableCount("water"), "Reserved cargo should not remain available.");
            TestAssert.True(service.Release("trade-1"), "Release should find the trade reservation.");
            TestAssert.Equal(5, source.GetAvailableCount("water"), "Release should restore source availability.");
        }

        private static void SoftReservationUsesAssignmentMetadata()
        {
            FakeItemStore source = new FakeItemStore("source");
            source.SetCount("water", 5);
            FakeItemStore target = new FakeItemStore("target");
            FakeAssignmentService assignments = new FakeAssignmentService();
            ShelteredMultiplayerTradeCargoReservationService service =
                new ShelteredMultiplayerTradeCargoReservationService(Resolve(source, target), assignments);

            ShelteredTradeCargoReservationResult result = service.Reserve(CreateTrade(3));

            TestAssert.True(result.Success, "Soft reservation should succeed.");
            TestAssert.Equal(3, assignments.GetAssignedCountForActor("water"), "Soft reservation should be assignment metadata.");
            TestAssert.Equal(5, source.GetCount("water"), "Soft reservation must keep items in the backing store.");
        }

        private static void SoftReservationPreventsOverAssignment()
        {
            FakeItemStore source = new FakeItemStore("source");
            source.SetCount("water", 5);
            FakeItemStore target = new FakeItemStore("target");
            FakeAssignmentService assignments = new FakeAssignmentService();
            ShelteredMultiplayerTradeCargoReservationService service =
                new ShelteredMultiplayerTradeCargoReservationService(Resolve(source, target), assignments);

            TestAssert.True(service.Reserve(CreateTrade(3)).Success, "First reservation should fit.");
            ShelteredTradeCargoReservationResult second = service.Reserve(CreateTrade("trade-2", 3));

            TestAssert.False(second.Success, "Second reservation should reject over-assignment.");
            TestAssert.Equal(3, assignments.GetAssignedCountForActor("water"), "Failed reservation should not add more assignment metadata.");
        }

        internal static Func<string, IItemStore> Resolve(IItemStore source, IItemStore target)
        {
            return delegate(string ownerId)
            {
                if (string.Equals(ownerId, "1", StringComparison.Ordinal))
                    return source;
                if (string.Equals(ownerId, "2", StringComparison.Ordinal))
                    return target;
                return null;
            };
        }

        internal static ShelteredMultiplayerTradeEvent CreateTrade(int count)
        {
            return CreateTrade("trade-1", count);
        }

        internal static ShelteredMultiplayerTradeEvent CreateTrade(string tradeId, int count)
        {
            ShelteredMultiplayerTradeEvent tradeEvent = new ShelteredMultiplayerTradeEvent();
            tradeEvent.EventKind = ShelteredNetworkEventKinds.TradeOfferAccepted;
            tradeEvent.TradeId = tradeId;
            tradeEvent.SourceOwnerId = "1";
            tradeEvent.TargetOwnerId = "2";

            ShelteredTradeCargoDto cargo = new ShelteredTradeCargoDto();
            cargo.ItemId = "water";
            cargo.Count = count;
            cargo.SourceOwnerId = "1";
            cargo.TargetOwnerId = "2";
            tradeEvent.Cargo.Add(cargo);
            return tradeEvent;
        }

        internal class FakeItemStore : IItemStore
        {
            protected readonly Dictionary<string, int> Counts = new Dictionary<string, int>(StringComparer.Ordinal);

            public FakeItemStore(string storeId)
            {
                StoreId = storeId;
                DisplayName = storeId;
            }

            public string StoreId { get; private set; }
            public string DisplayName { get; private set; }
            public ItemStoreKind Kind { get { return ItemStoreKind.Mod; } }
            public int Capacity { get { return -1; } }
            public int Used { get { return 0; } }
            public bool IsReadOnly { get { return false; } }

            public void SetCount(string itemId, int count)
            {
                Counts[itemId] = count;
            }

            public ItemStoreSnapshot Snapshot()
            {
                return new ItemStoreSnapshot();
            }

            public virtual int GetCount(string itemId)
            {
                int count;
                return itemId != null && Counts.TryGetValue(itemId, out count) ? count : 0;
            }

            public bool CanAdd(string itemId, int quantity)
            {
                return !string.IsNullOrEmpty(itemId) && quantity > 0;
            }

            public virtual bool CanRemove(string itemId, int quantity)
            {
                return quantity > 0 && GetCount(itemId) >= quantity;
            }

            public ItemTransferResult Add(string itemId, int quantity)
            {
                if (!CanAdd(itemId, quantity))
                    return ItemTransferResult.Failed(itemId, quantity, "Cannot add.");

                Counts[itemId] = GetCount(itemId) + quantity;
                return ItemTransferResult.Ok(itemId, quantity, quantity);
            }

            public virtual ItemTransferResult Remove(string itemId, int quantity)
            {
                if (!CanRemove(itemId, quantity))
                    return ItemTransferResult.Failed(itemId, quantity, "Cannot remove.");

                Counts[itemId] = GetCount(itemId) - quantity;
                return ItemTransferResult.Ok(itemId, quantity, quantity);
            }
        }

        internal sealed class FakeReservableStore : FakeItemStore, IReservableItemStore
        {
            private readonly Dictionary<string, ItemReservationResult> _reservations =
                new Dictionary<string, ItemReservationResult>(StringComparer.Ordinal);

            public FakeReservableStore(string storeId)
                : base(storeId)
            {
            }

            public ItemReservationResult Reserve(string itemId, int quantity, string ownerToken)
            {
                if (GetAvailableCount(itemId) < quantity)
                    return ItemReservationResult.Failed(itemId, quantity, ownerToken, "Not enough available.");

                string reservationId = "reservation-" + (_reservations.Count + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                ItemReservationResult result = ItemReservationResult.Ok(reservationId, itemId, quantity, quantity, ownerToken);
                _reservations[reservationId] = result;
                return result;
            }

            public ItemTransferResult CommitReservation(string reservationId)
            {
                ItemReservationResult reservation;
                if (!_reservations.TryGetValue(reservationId, out reservation))
                    return ItemTransferResult.Failed(null, 0, "Missing reservation.");

                _reservations.Remove(reservationId);
                return base.Remove(reservation.ItemId, reservation.Reserved);
            }

            public ItemTransferResult CancelReservation(string reservationId)
            {
                ItemReservationResult reservation;
                if (!_reservations.TryGetValue(reservationId, out reservation))
                    return ItemTransferResult.Failed(null, 0, "Missing reservation.");

                _reservations.Remove(reservationId);
                return ItemTransferResult.Ok(reservation.ItemId, reservation.Reserved, 0);
            }

            public int GetAvailableCount(string itemId)
            {
                int reserved = 0;
                foreach (ItemReservationResult reservation in _reservations.Values)
                {
                    if (reservation != null && string.Equals(reservation.ItemId, itemId, StringComparison.Ordinal))
                        reserved += reservation.Reserved;
                }

                return Math.Max(0, GetCount(itemId) - reserved);
            }

            public override bool CanRemove(string itemId, int quantity)
            {
                return GetAvailableCount(itemId) >= quantity;
            }
        }

        internal sealed class FakeAssignmentService : ICharacterItemAssignmentService
        {
            private readonly Dictionary<string, CharacterItemAssignment> _assignments =
                new Dictionary<string, CharacterItemAssignment>(StringComparer.Ordinal);

            public CharacterItemAssignment Assign(ActorId actorId, IItemStore source, string itemId, int quantity, CharacterItemAssignmentKind kind, CharacterItemSlot slot)
            {
                if (source.GetCount(itemId) - GetAssignedCountInStore(source.StoreId, itemId) < quantity)
                    throw new InvalidOperationException("Source store does not have enough unassigned quantity for this item.");

                CharacterItemAssignment assignment = new CharacterItemAssignment();
                assignment.AssignmentId = "assignment-" + (_assignments.Count + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                assignment.ActorId = actorId;
                assignment.SourceStoreId = source.StoreId;
                assignment.ItemId = itemId;
                assignment.Quantity = quantity;
                assignment.Kind = kind;
                assignment.Slot = slot;
                _assignments[assignment.AssignmentId] = assignment;
                return assignment;
            }

            public CharacterItemAssignment Assign(FamilyMember member, IItemStore source, string itemId, int quantity, CharacterItemAssignmentKind kind, CharacterItemSlot slot)
            {
                return Assign((ActorId)null, source, itemId, quantity, kind, slot);
            }

            public bool Unassign(string assignmentId)
            {
                return _assignments.Remove(assignmentId ?? string.Empty);
            }

            public IList<CharacterItemAssignment> GetAssignments(ActorId actorId) { return new List<CharacterItemAssignment>(_assignments.Values); }
            public IList<CharacterItemAssignment> GetAssignments(FamilyMember member) { return new List<CharacterItemAssignment>(_assignments.Values); }
            public IList<CharacterItemAssignment> GetAvailableAssignments(ActorId actorId) { return GetAssignments(actorId); }
            public IList<CharacterItemAssignment> GetAvailableAssignments(FamilyMember member) { return GetAssignments(member); }

            public int GetAssignedCount(ActorId actorId, string itemId)
            {
                int total = 0;
                foreach (CharacterItemAssignment assignment in _assignments.Values)
                {
                    if (assignment != null && string.Equals(assignment.ItemId, itemId, StringComparison.Ordinal))
                        total += assignment.Quantity;
                }

                return total;
            }

            public int GetAssignedCountForActor(string itemId) { return GetAssignedCount((ActorId)null, itemId); }
            public int GetAssignedCount(FamilyMember member, string itemId) { return GetAssignedCountForActor(itemId); }
            public int ReleaseAssignmentsForActor(ActorId actorId) { int count = _assignments.Count; _assignments.Clear(); return count; }
            public int ReleaseAssignmentsForMember(FamilyMember member) { return ReleaseAssignmentsForActor(null); }

            private int GetAssignedCountInStore(string storeId, string itemId)
            {
                int total = 0;
                foreach (CharacterItemAssignment assignment in _assignments.Values)
                {
                    if (assignment != null
                        && string.Equals(assignment.SourceStoreId, storeId, StringComparison.Ordinal)
                        && string.Equals(assignment.ItemId, itemId, StringComparison.Ordinal))
                    {
                        total += assignment.Quantity;
                    }
                }

                return total;
            }
        }
    }
}
