using System.Collections.Generic;
using ShelteredAPI.Networking.Resources;
using ShelteredAPI.Networking.World;

namespace ShelteredAPI.Networking.Tests
{
    internal static class ShelteredResourceNodeTests
    {
        public static void Register(List<TestCase> tests)
        {
            tests.Add(new TestCase("ResourceNodeRegistry_RegistersMapEntity", RegistersMapEntity));
            tests.Add(new TestCase("ResourceNodeRegistry_HarvestMarksDepleted", HarvestMarksDepleted));
            tests.Add(new TestCase("ResourceNodeRegistry_RegenerateCapsAtCapacity", RegenerateCapsAtCapacity));
        }

        private static void RegistersMapEntity()
        {
            ShelteredMapEntities.Clear("test-start");
            ShelteredResourceNodeRegistry registry = new ShelteredResourceNodeRegistry(delegate { return 44; });
            ResourceNode node = new ResourceNode();
            node.Kind = "Water";
            node.GridX = 2;
            node.GridY = 3;
            node.Capacity = 10;
            node.Remaining = 10;

            ResourceNode saved = registry.Upsert(node);
            ShelteredMapEntity entity = ShelteredMapEntities.Registry.Get("mapentity:resourcenode:" + saved.NodeId);

            TestAssert.True(entity != null, "Resource nodes should register as map entities.");
            TestAssert.Equal(ShelteredMapEntityKind.ResourceNode, entity.Kind, "Map entity kind should be ResourceNode.");
            TestAssert.Equal(2, entity.GridX, "Map entity should carry grid x.");
            TestAssert.Equal(3, entity.GridY, "Map entity should carry grid y.");
            ShelteredMapEntities.Clear("test-end");
        }

        private static void HarvestMarksDepleted()
        {
            ShelteredResourceNodeRegistry registry = new ShelteredResourceNodeRegistry(delegate { return 1; });
            ResourceNode node = new ResourceNode();
            node.NodeId = "node";
            node.Kind = "Scrap";
            node.Capacity = 5;
            node.Remaining = 5;
            registry.Upsert(node);

            ResourceNode updated;
            bool harvested = registry.Harvest("node", 5, 2, 9, out updated);

            TestAssert.True(harvested, "Harvest should apply to an existing node.");
            TestAssert.Equal(0, updated.Remaining, "Harvest should reduce remaining amount.");
            TestAssert.True(updated.IsDepleted, "Empty node should be depleted.");
            TestAssert.Equal(2, updated.OwnerPlayerId, "Harvesting player should be recorded.");
        }

        private static void RegenerateCapsAtCapacity()
        {
            ShelteredResourceNodeRegistry registry = new ShelteredResourceNodeRegistry(delegate { return 1; });
            ResourceNode node = new ResourceNode();
            node.NodeId = "node";
            node.Kind = "Water";
            node.Capacity = 10;
            node.Remaining = 7;
            registry.Upsert(node);

            ResourceNode updated;
            bool regenerated = registry.Regenerate("node", 9, 12, out updated);

            TestAssert.True(regenerated, "Regeneration should apply to an existing node.");
            TestAssert.Equal(10, updated.Remaining, "Regeneration should cap at capacity.");
            TestAssert.False(updated.IsDepleted, "Regenerated node should not be depleted.");
        }
    }
}
