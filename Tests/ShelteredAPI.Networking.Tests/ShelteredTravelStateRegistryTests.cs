using System.Collections.Generic;
using ShelteredAPI.Networking.Knowledge;
using ShelteredAPI.Networking.Map;
using ShelteredAPI.Networking.Travel;
using ShelteredAPI.Networking.World;

namespace ShelteredAPI.Networking.Tests
{
    internal static class ShelteredTravelStateRegistryTests
    {
        public static void Register(List<TestCase> tests)
        {
            tests.Add(new TestCase("Travel registry applies started event and predicts active location", StartedEventPredictsActiveLocation));
            tests.Add(new TestCase("Travel registry ignores duplicate event ids", DuplicateEventIdIgnored));
            tests.Add(new TestCase("Travel registry ignores out-of-order corrections", OlderCorrectionIgnored));
            tests.Add(new TestCase("Travel replay rewinds only corrected travel entity", ReplayCorrectionOnlyUpdatesTargetTravel));
            tests.Add(new TestCase("Travel arrival marks terminal state", ArrivalMarksTerminalState));
            tests.Add(new TestCase("Travel registry map entity receives predicted position", MapEntityReceivesPredictedPosition));
            tests.Add(new TestCase("Travel registry map entity remains knowledge filtered", TravelMapEntityRequiresKnowledgeToDisplay));
            tests.Add(new TestCase("Travel crossing detects two active crossing expeditions", CrossingDetectorCreatesCandidate));
            tests.Add(new TestCase("Travel crossing ignores same player expeditions", CrossingDetectorIgnoresSamePlayer));
            tests.Add(new TestCase("Travel crossing candidate id is stable regardless of order", CrossingCandidateIdStableRegardlessOfOrder));
        }

        private static void StartedEventPredictsActiveLocation()
        {
            ShelteredTravelStateRegistry registry = new ShelteredTravelStateRegistry(null);
            ShelteredTravelApplyResult applied = registry.ApplyTravelStarted(CreateStarted("travel-1", 0, 0, 10, 0), "event-1");

            ShelteredTravelPredictionResult prediction = registry.Predict("travel-1", 5);
            IList<ShelteredTravelState> active = registry.GetActive();

            TestAssert.Equal(true, applied.AppliedEvent, "Started event should apply.");
            TestAssert.Equal(1, active.Count, "Started travel should be active.");
            TestAssert.Equal(5, prediction.GridX, "Prediction should advance along the route.");
            TestAssert.Equal(0, prediction.GridY, "Prediction should preserve Y on a horizontal route.");
        }

        private static void DuplicateEventIdIgnored()
        {
            ShelteredTravelStateRegistry registry = new ShelteredTravelStateRegistry(null);
            registry.ApplyTravelStarted(CreateStarted("travel-1", 0, 0, 10, 0), "event-1");
            ShelteredTravelApplyResult duplicate =
                registry.ApplyTravelStarted(CreateStarted("travel-2", 0, 0, 20, 0), "event-1");

            TestAssert.Equal(false, duplicate.AppliedEvent, "Duplicate event id should not apply.");
            TestAssert.Equal("duplicate-event-id", duplicate.Reason, "Duplicate should report duplicate reason.");
            TestAssert.Equal(1, registry.GetActive().Count, "Duplicate start must not add another active travel.");
        }

        private static void OlderCorrectionIgnored()
        {
            ShelteredTravelStateRegistry registry = new ShelteredTravelStateRegistry(null);
            registry.ApplyTravelStarted(CreateStarted("travel-1", 0, 0, 10, 0), "event-1");

            ShelteredTravelCorrectedEvent newer = CreateCorrection("travel-1", 8, 8, 0, 20, 0);
            ShelteredTravelCorrectedEvent older = CreateCorrection("travel-1", 6, 6, 0, 30, 0);
            registry.ApplyTravelCorrected(newer, "event-2");
            ShelteredTravelApplyResult oldResult = registry.ApplyTravelCorrected(older, "event-3");
            ShelteredTravelPredictionResult prediction = registry.Predict("travel-1", 8);

            TestAssert.Equal(false, oldResult.AppliedEvent, "Older correction should be ignored.");
            TestAssert.Equal("out-of-order-event", oldResult.Reason, "Older correction should report ordering reason.");
            TestAssert.Equal(8, prediction.GridX, "Prediction should keep newer correction start.");
        }

        private static void ReplayCorrectionOnlyUpdatesTargetTravel()
        {
            ShelteredTravelStateRegistry registry = new ShelteredTravelStateRegistry(null);
            ShelteredTravelReplayService replay = new ShelteredTravelReplayService(registry);
            registry.ApplyTravelStarted(CreateStarted("travel-1", 0, 0, 10, 0), "event-1");
            registry.ApplyTravelStarted(CreateStarted("travel-2", 0, 10, 10, 10), "event-2");

            ShelteredTravelPredictionResult replayed =
                replay.ReplayCorrection(CreateCorrection("travel-1", 5, 3, 0, 13, 0), "event-3", 5);
            ShelteredTravelPredictionResult untouched = registry.Predict("travel-2", 5);

            TestAssert.Equal(3, replayed.GridX, "Replay should rewind corrected travel to corrected X.");
            TestAssert.Equal(0, replayed.GridY, "Replay should rewind corrected travel to corrected Y.");
            TestAssert.Equal(5, untouched.GridX, "Uncorrected travel should keep original prediction.");
            TestAssert.Equal(10, untouched.GridY, "Uncorrected travel should keep original Y.");
        }

        private static void ArrivalMarksTerminalState()
        {
            ShelteredTravelStateRegistry registry = new ShelteredTravelStateRegistry(null);
            registry.ApplyTravelStarted(CreateStarted("travel-1", 0, 0, 10, 0), "event-1");

            ShelteredTravelArrivedEvent arrived = new ShelteredTravelArrivedEvent();
            arrived.TravelId = "travel-1";
            arrived.ArrivalTick = 10;
            arrived.ArrivalGridX = 10;
            arrived.ArrivalGridY = 0;
            arrived.ResultKind = ShelteredTravelArrivalKinds.ReturnedHome;

            ShelteredTravelApplyResult applied = registry.ApplyTravelArrived(arrived, "event-2");

            TestAssert.Equal(true, applied.AppliedEvent, "Arrival event should apply.");
            TestAssert.Equal(0, registry.GetActive().Count,
                "Arrived travel should not remain in the active prediction set.");
        }

        private static void MapEntityReceivesPredictedPosition()
        {
            ShelteredMapEntityRegistry map = new ShelteredMapEntityRegistry(delegate { return 0; });
            ShelteredTravelStateRegistry registry = new ShelteredTravelStateRegistry(map);
            registry.ApplyTravelStarted(CreateStarted("travel-1", 0, 0, 10, 0), "event-1");

            registry.Predict("travel-1", 6);

            ShelteredMapEntity entity = map.Get(ShelteredTravelStateRegistry.CreateMapEntityId("travel-1"));
            TestAssert.True(entity != null, "Prediction should upsert a map entity for the expedition.");
            TestAssert.Equal(6, entity.GridX,
                "Map entity grid X should come from the WorldTick prediction.");
            TestAssert.Equal(0, entity.GridY,
                "Map entity grid Y should come from the WorldTick prediction.");
            TestAssert.Equal((long)6, entity.UpdatedWorldTick,
                "Map entity update tick should be the prediction tick, not only the route-start tick.");
        }

        private static void TravelMapEntityRequiresKnowledgeToDisplay()
        {
            ShelteredNetworkingTestContext.ResetClientContext(true);
            ShelteredTravelStateRegistry registry = new ShelteredTravelStateRegistry(ShelteredMapEntities.Registry);
            ShelteredTravelStartedEvent started = CreateStarted("travel-remote", 2, 0, 0, 10, 0);
            started.OwnerPeerId = 5;
            started.PartyId = 4;

            registry.ApplyTravelStarted(started, "travel-remote-started");
            string entityId = ShelteredTravelStateRegistry.CreateMapEntityId("travel-remote");
            ShelteredMapEntity entity = ShelteredMapEntities.Get(entityId);
            IList<ShelteredMapEntity> hidden =
                ShelteredMapKnowledgeService.Instance.GetVisibleEntities(1, ShelteredMapEntities.Registry);

            TestAssert.True(entity != null, "Travel registry should upsert an expedition map entity.");
            TestAssert.Equal(ShelteredMapEntityKind.Expedition, entity.Kind,
                "Travel map entity should be an expedition entity.");
            TestAssert.Equal(0, hidden.Count,
                "Fog-on remote expedition entities should not display without knowledge.");
            TestAssert.True(ShelteredMapKnowledgeService.Instance.BuildDisplayMarker(1, entity) == null,
                "Remote expedition marker should be filtered until knowledge reveals it.");

            ShelteredMapKnowledgeService.Instance.Reveal(1, entityId, MapKnowledgeLevel.Scouted, "travel-scouted");
            IList<ShelteredMapEntity> visible =
                ShelteredMapKnowledgeService.Instance.GetVisibleEntities(1, ShelteredMapEntities.Registry);
            ShelteredMultiplayerMapMarker marker =
                ShelteredMapKnowledgeService.Instance.BuildDisplayMarker(1, entity);

            TestAssert.Equal(1, visible.Count,
                "Scouted remote expedition should display through the knowledge-filtered query.");
            TestAssert.Equal(ShelteredMultiplayerMapMarkerVisualKind.Expedition, marker.VisualKind,
                "Scouted remote expedition should reveal the concrete expedition marker kind.");
            TestAssert.Equal("?", marker.Label,
                "Scouted remote expedition should not reveal identity.");
        }

        private static void CrossingDetectorCreatesCandidate()
        {
            ShelteredTravelStateRegistry registry = new ShelteredTravelStateRegistry(null);
            registry.ApplyTravelStarted(CreateStarted("travel-1", 1, 0, 0, 10, 0), "event-1");
            registry.ApplyTravelStarted(CreateStarted("travel-2", 2, 10, 0, 0, 0), "event-2");

            ShelteredTravelCrossingDetector detector = new ShelteredTravelCrossingDetector(registry, null);
            IList<ShelteredTravelCrossingCandidate> candidates = detector.Detect(5);

            TestAssert.Equal(1, candidates.Count, "Crossing active travels should create one candidate.");
            TestAssert.Equal("travel-1", candidates[0].FirstTravelId, "First travel id should be retained.");
            TestAssert.Equal("travel-2", candidates[0].SecondTravelId, "Second travel id should be retained.");
            TestAssert.Equal(0, candidates[0].CellDistance, "Crossing at the same cell should have zero cell distance.");
            TestAssert.Equal(ShelteredTravelCrossingDetector.CreateEncounterId("travel-1", "travel-2"), candidates[0].EncounterId, "Candidate encounter id should be stable.");
        }

        private static void CrossingDetectorIgnoresSamePlayer()
        {
            ShelteredTravelStateRegistry registry = new ShelteredTravelStateRegistry(null);
            registry.ApplyTravelStarted(CreateStarted("travel-1", 1, 0, 0, 10, 0), "event-1");
            registry.ApplyTravelStarted(CreateStarted("travel-2", 1, 10, 0, 0, 0), "event-2");

            ShelteredTravelCrossingDetector detector = new ShelteredTravelCrossingDetector(registry, null);
            IList<ShelteredTravelCrossingCandidate> candidates = detector.Detect(5);

            TestAssert.Equal(0, candidates.Count, "Same-player active travels should not create a negotiation candidate.");
        }

        private static void CrossingCandidateIdStableRegardlessOfOrder()
        {
            string first = ShelteredTravelCrossingDetector.CreateEncounterId("travel-a", "travel-b");
            string second = ShelteredTravelCrossingDetector.CreateEncounterId("travel-b", "travel-a");

            TestAssert.Equal(first, second, "Crossing encounter id should not depend on travel order.");
        }

        private static ShelteredTravelStartedEvent CreateStarted(string travelId, int startX, int startY, int destinationX, int destinationY)
        {
            return CreateStarted(travelId, 1, startX, startY, destinationX, destinationY);
        }

        private static ShelteredTravelStartedEvent CreateStarted(
            string travelId,
            int ownerPlayerId,
            int startX,
            int startY,
            int destinationX,
            int destinationY)
        {
            return new ShelteredTravelStartedEvent
            {
                TravelId = travelId,
                OwnerPlayerId = ownerPlayerId,
                OwnerPeerId = 0,
                PartyId = travelId == "travel-1" ? 1 : 2,
                StartTick = 0,
                StartGridX = startX,
                StartGridY = startY,
                DestinationGridX = destinationX,
                DestinationGridY = destinationY,
                WorldUnitsPerTick = 1f,
                ExpectedArrivalTick = 10,
                SeedStreamName = "MultiplayerSync.Travel.test"
            };
        }

        private static ShelteredTravelCorrectedEvent CreateCorrection(
            string travelId,
            long correctionTick,
            int correctedX,
            int correctedY,
            int destinationX,
            int destinationY)
        {
            return new ShelteredTravelCorrectedEvent
            {
                TravelId = travelId,
                CorrectionTick = correctionTick,
                CorrectedGridX = correctedX,
                CorrectedGridY = correctedY,
                DestinationGridX = destinationX,
                DestinationGridY = destinationY,
                WorldUnitsPerTick = 1f,
                ExpectedArrivalTick = correctionTick + 10,
                Reason = ShelteredTravelCorrectionReasons.HostCorrection
            };
        }
    }
}
