using System.Collections.Generic;
using ShelteredAPI.Networking.World;
using UnityEngine;

namespace ShelteredAPI.Networking.Tests
{
    internal static class ShelteredMapEntityRegistryTests
    {
        public static void Register(List<TestCase> tests)
        {
            tests.Add(new TestCase("MapEntityRegistry_UpsertReplacesExistingEntity", UpsertReplacesExistingEntity));
            tests.Add(new TestCase("MapEntityRegistry_GetByKindReturnsOnlySelectedKind", GetByKindReturnsOnlySelectedKind));
            tests.Add(new TestCase("MapEntityRegistry_GetByOwnerAndRemoveWork", GetByOwnerAndRemoveWork));
            tests.Add(new TestCase("MapEntityRegistry_SupportsUnknownContactKind", SupportsUnknownContactKind));
            tests.Add(new TestCase("MapEntityRegistry_BunkerAssignmentBecomesBunkerEntity", BunkerAssignmentBecomesBunkerEntity));
            tests.Add(new TestCase("MapEntityRegistry_ClearRemovesEntities", ClearRemovesEntities));
        }

        private static void GetByOwnerAndRemoveWork()
        {
            ShelteredMapEntityRegistry registry = new ShelteredMapEntityRegistry(delegate { return 0; });
            registry.Upsert(CreateEntity("owner-2-bunker", ShelteredMapEntityKind.Bunker, 2));
            registry.Upsert(CreateEntity("owner-2-expedition", ShelteredMapEntityKind.Expedition, 2));
            registry.Upsert(CreateEntity("owner-3-bunker", ShelteredMapEntityKind.Bunker, 3));

            IList<ShelteredMapEntity> ownerEntities = registry.GetByOwnerPlayerId(2);
            bool removed = registry.Remove("owner-2-expedition");

            TestAssert.Equal(2, ownerEntities.Count, "GetByOwner should return only the selected owner's entities.");
            TestAssert.True(removed, "Remove should return true for an existing entity.");
            TestAssert.True(registry.Get("owner-2-expedition") == null, "Removed entity should no longer be returned.");
        }

        private static void SupportsUnknownContactKind()
        {
            ShelteredMapEntityRegistry registry = new ShelteredMapEntityRegistry(delegate { return 0; });
            registry.Upsert(CreateEntity("unknown-1", ShelteredMapEntityKind.UnknownContact, 0));

            IList<ShelteredMapEntity> contacts = registry.GetByKind(ShelteredMapEntityKind.UnknownContact);

            TestAssert.Equal(1, contacts.Count, "Registry should support unknown contact entities.");
            TestAssert.Equal(ShelteredMapEntityKind.UnknownContact, contacts[0].Kind,
                "Unknown contact entity kind should round-trip.");
        }

        private static void UpsertReplacesExistingEntity()
        {
            ShelteredMapEntityRegistry registry = new ShelteredMapEntityRegistry(delegate { return 42; });
            ShelteredMapEntity first = CreateEntity("shared", ShelteredMapEntityKind.Bunker, 1);
            first.DisplayName = "First";
            first.UpdatedWorldTick = 7;

            ShelteredMapEntity second = CreateEntity("shared", ShelteredMapEntityKind.Bunker, 1);
            second.DisplayName = "Second";
            second.UpdatedWorldTick = 0;

            registry.Upsert(first);
            registry.Upsert(second);

            IList<ShelteredMapEntity> all = registry.GetAll();
            TestAssert.Equal(1, all.Count, "Upsert should deduplicate by entity id.");
            TestAssert.Equal("Second", all[0].DisplayName, "Upsert should replace the existing entity.");
            TestAssert.Equal((long)42, all[0].UpdatedWorldTick, "Missing update tick should use the session world tick source.");
        }

        private static void GetByKindReturnsOnlySelectedKind()
        {
            ShelteredMapEntityRegistry registry = new ShelteredMapEntityRegistry(delegate { return 0; });
            registry.Upsert(CreateEntity("bunker-1", ShelteredMapEntityKind.Bunker, 1));
            registry.Upsert(CreateEntity("trade-1", ShelteredMapEntityKind.TradeCaravan, 2));
            registry.Upsert(CreateEntity("bunker-2", ShelteredMapEntityKind.Bunker, 3));

            IList<ShelteredMapEntity> bunkers = registry.GetByKind(ShelteredMapEntityKind.Bunker);

            TestAssert.Equal(2, bunkers.Count, "GetByKind should return only matching entities.");
            TestAssert.Equal(ShelteredMapEntityKind.Bunker, bunkers[0].Kind, "First result should be a bunker.");
            TestAssert.Equal(ShelteredMapEntityKind.Bunker, bunkers[1].Kind, "Second result should be a bunker.");
        }

        private static void BunkerAssignmentBecomesBunkerEntity()
        {
            ShelteredMapEntities.Clear("test-start");

            ShelteredMultiplayerBunkerAssignmentRecord record = new ShelteredMultiplayerBunkerAssignmentRecord(
                3,
                2,
                1,
                new Vector2(12f, -4f),
                "Remote Bunker",
                true);

            ShelteredMapEntity entity = ShelteredMapEntities.RegisterBunkerFromAssignment(record);
            ShelteredMapEntity fetched = ShelteredMapEntities.Registry.Get("mapentity:bunker:1");

            TestAssert.True(entity != null, "Bunker assignment should create a map entity.");
            TestAssert.True(fetched != null, "Bunker entity should be registered by stable bunker id.");
            TestAssert.Equal(ShelteredMapEntityKind.Bunker, fetched.Kind, "Bunker assignment should create a bunker entity.");
            TestAssert.Equal(2, fetched.OwnerPlayerId, "Bunker entity should carry the gameplay player id.");
            TestAssert.Equal((byte)3, fetched.OwnerPeerId, "Bunker entity should carry the network peer id.");
            TestAssert.Equal(1, fetched.BunkerOwnerId, "Bunker entity should carry the bunker owner id.");
            TestAssert.Equal("Remote Bunker", fetched.DisplayName, "Bunker entity should carry the display name.");
            TestAssert.Near(12f, fetched.WorldPosition.x, 0.0001f, "Bunker entity should carry the world x position.");
            TestAssert.Near(-4f, fetched.WorldPosition.y, 0.0001f, "Bunker entity should carry the world y position.");
            TestAssert.True(fetched.IsOnline, "Bunker entity should carry online state.");
            TestAssert.True(fetched.IsVisible, "Bunker entity should be visible by default.");

            ShelteredMapEntities.Clear("test-end");
        }

        private static void ClearRemovesEntities()
        {
            ShelteredMapEntityRegistry registry = new ShelteredMapEntityRegistry(delegate { return 0; });
            registry.Upsert(CreateEntity("bunker-1", ShelteredMapEntityKind.Bunker, 1));
            registry.Upsert(CreateEntity("resource-1", ShelteredMapEntityKind.ResourceNode, 1));

            registry.Clear("test");

            TestAssert.Equal(0, registry.GetAll().Count, "Clear should remove all map entities.");
        }

        private static ShelteredMapEntity CreateEntity(string entityId, ShelteredMapEntityKind kind, int ownerPlayerId)
        {
            ShelteredMapEntity entity = new ShelteredMapEntity();
            entity.EntityId = entityId;
            entity.Kind = kind;
            entity.OwnerPlayerId = ownerPlayerId;
            entity.BunkerOwnerId = ownerPlayerId > 0 ? ownerPlayerId - 1 : 0;
            entity.DisplayName = entityId;
            entity.WorldPosition = Vector2.zero;
            entity.MapPixels = Vector3.zero;
            entity.IsOnline = true;
            entity.IsVisible = true;
            return entity;
        }
    }
}
