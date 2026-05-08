using System.Collections.Generic;
using ShelteredAPI.Networking.Settlements;
using ShelteredAPI.Networking.World;

namespace ShelteredAPI.Networking.Tests
{
    internal static class ShelteredSettlementStateTests
    {
        public static void Register(List<TestCase> tests)
        {
            tests.Add(new TestCase("SettlementStateRegistry_AppliesSettlementAndCreatesMapEntity", AppliesSettlementAndCreatesMapEntity));
            tests.Add(new TestCase("SettlementProductionService_BuildsTagBasedProduction", BuildsTagBasedProduction));
        }

        private static void AppliesSettlementAndCreatesMapEntity()
        {
            ShelteredMapEntityRegistry mapEntities = new ShelteredMapEntityRegistry(delegate { return 0; });
            ShelteredSettlementStateRegistry registry = new ShelteredSettlementStateRegistry(mapEntities);
            ShelteredSettlementEvent settlementEvent = new ShelteredSettlementEvent();
            settlementEvent.SettlementId = "settlement-a";
            settlementEvent.EventKind = ShelteredNetworkEventKinds.SettlementFounded;
            settlementEvent.OwnerPlayerId = 2;
            settlementEvent.OwnerFactionId = "faction-a";
            settlementEvent.GridX = 4;
            settlementEvent.GridY = 5;
            settlementEvent.Population = 12;
            settlementEvent.Defense = 40;
            settlementEvent.ProductionTags.Add("water");

            ShelteredSettlementApplyResult result = registry.Apply(settlementEvent, "settlement-event-1");
            ShelteredSettlementState state = registry.Get("settlement-a");
            ShelteredMapEntity entity = mapEntities.Get(ShelteredSettlementStateRegistry.CreateMapEntityId("settlement-a"));

            TestAssert.True(result.AppliedEvent, "Settlement event should apply.");
            TestAssert.Equal("faction-a", state.OwnerFactionId, "Faction id should be retained without a hard dependency.");
            TestAssert.Equal(1, state.ProductionTags.Count, "Production tags should be retained.");
            TestAssert.True(entity != null, "Settlement should exist as a map entity.");
            TestAssert.Equal(ShelteredMapEntityKind.Settlement, entity.Kind, "Settlement map entity kind should be used.");
            TestAssert.Equal(4, entity.GridX, "Settlement grid x should be projected.");
        }

        private static void BuildsTagBasedProduction()
        {
            ShelteredSettlementState state = new ShelteredSettlementState();
            state.SettlementId = "settlement-b";
            state.Population = 8;
            state.Defense = 20;
            state.ProductionTags.Add("food");
            state.ProductionTags.Add("scrap");

            ShelteredSettlementProductionResult result = new ShelteredSettlementProductionService().BuildProduction(state, 99);

            TestAssert.Equal("settlement-b", result.SettlementId, "Production should identify settlement.");
            TestAssert.Equal((long)99, result.ProductionTick, "Production tick should be supplied by caller.");
            TestAssert.Equal(10, result.ProductionScore, "Production score should be deterministic.");
            TestAssert.Equal(2, result.ProducedTags.Count, "Production tags should be copied.");
        }
    }
}
