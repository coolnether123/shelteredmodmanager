using System.Collections.Generic;
using ModAPI.Networking;
using ShelteredAPI.Networking.Knowledge;
using ShelteredAPI.Networking.Map;
using ShelteredAPI.Networking.World;
using UnityEngine;

namespace ShelteredAPI.Networking.Tests
{
    internal static class ShelteredMapKnowledgeTests
    {
        public static void Register(List<TestCase> tests)
        {
            tests.Add(new TestCase("MapKnowledge_RemoteBunkerDefaultsToQuestionMarker", RemoteBunkerDefaultsToQuestionMarker));
            tests.Add(new TestCase("MapKnowledge_FogOnHidesOtherPlayerBunkerDetails", FogOnHidesOtherPlayerBunkerDetails));
            tests.Add(new TestCase("MapKnowledge_FogOffRevealsRemoteBunker", FogOffRevealsRemoteBunker));
            tests.Add(new TestCase("MapKnowledge_UpgradeRevealsProgressiveData", UpgradeRevealsProgressiveData));
            tests.Add(new TestCase("MapKnowledge_RevealIdentifiesRemoteBunker", RevealIdentifiesRemoteBunker));
            tests.Add(new TestCase("MapKnowledge_DebugRevealShowsAll", DebugRevealShowsAll));
            tests.Add(new TestCase("MapKnowledge_ForgetRemovesRecord", ForgetRemovesRecord));
        }

        private static void RemoteBunkerDefaultsToQuestionMarker()
        {
            ShelteredNetworkingTestContext.ResetClientContext(true);
            ShelteredMapEntity entity = RegisterRemoteBunker();

            ShelteredMultiplayerMapMarker marker =
                ShelteredMapKnowledgeService.Instance.BuildDisplayMarker(1, entity);

            TestAssert.True(marker != null, "Remote bunker should produce a display marker.");
            TestAssert.Equal("?", marker.Label, "Unknown remote bunker should not expose the bunker name.");
            TestAssert.True(marker.IsUnknown, "Unknown remote bunker should use the unknown visual.");
            TestAssert.False(ShelteredMapKnowledgeService.Instance.CanSeeExactLocation(1, entity.EntityId),
                "Suspicious contacts should not count as exact location knowledge.");
        }

        private static void FogOnHidesOtherPlayerBunkerDetails()
        {
            ShelteredNetworkingTestContext.ResetClientContext(true);
            ShelteredMapEntity entity = RegisterRemoteBunker();

            ShelteredMapEntity visible =
                ShelteredMapKnowledgeService.Instance.BuildVisibleEntity(1, entity);
            MapKnowledgeRecord knowledge =
                ShelteredMapKnowledgeService.Instance.GetEffectiveKnowledge(1, entity);

            TestAssert.True(visible != null, "Fog-on remote bunkers should remain visible as generic contacts.");
            TestAssert.Equal(MapKnowledgeLevel.Suspicious, knowledge.KnowledgeLevel,
                "Fog-on remote bunkers should default to suspicious contact knowledge.");
            TestAssert.Equal(MapContactKind.Unknown, knowledge.KnownKind,
                "Suspicious contacts should not reveal the concrete entity kind.");
            TestAssert.Equal(string.Empty, visible.DisplayName,
                "Suspicious contacts should not expose the remote bunker display name.");
            TestAssert.Equal(NetworkDefaults.UnassignedPeerId, visible.OwnerPeerId,
                "Suspicious contacts should not expose the owning network peer id.");
            TestAssert.Equal(string.Empty, visible.State,
                "Suspicious contacts should not expose detailed state.");
            TestAssert.Equal(string.Empty, visible.PayloadJson,
                "Suspicious contacts should not expose payload data.");
            TestAssert.False(ShelteredMapKnowledgeService.Instance.CanSeeExactLocation(1, entity.EntityId),
                "Suspicious contacts should not expose exact-location knowledge.");
        }

        private static void FogOffRevealsRemoteBunker()
        {
            ShelteredNetworkingTestContext.ResetClientContext(false);
            ShelteredMapEntity entity = RegisterRemoteBunker();

            ShelteredMapEntity visible =
                ShelteredMapKnowledgeService.Instance.BuildVisibleEntity(1, entity);
            ShelteredMultiplayerMapMarker marker =
                ShelteredMapKnowledgeService.Instance.BuildDisplayMarker(1, entity);

            TestAssert.True(visible != null, "Fog-off remote bunker should be visible.");
            TestAssert.Equal("Remote Player", visible.DisplayName,
                "Fog-off remote bunker should expose the display name.");
            TestAssert.Equal((byte)7, visible.OwnerPeerId,
                "Fog-off remote bunker should expose the owning peer id.");
            TestAssert.Equal("online", visible.State,
                "Fog-off remote bunker should expose detailed state.");
            TestAssert.Equal("{\"owner\":3}", visible.PayloadJson,
                "Fog-off remote bunker should expose payload details.");
            TestAssert.Equal("Remote Player", marker.Label,
                "Fog-off remote bunker marker should expose identity.");
            TestAssert.Equal(ShelteredMultiplayerMapMarkerVisualKind.RemoteBunker, marker.VisualKind,
                "Fog-off remote bunker marker should use the concrete bunker visual.");
            TestAssert.True(ShelteredMapKnowledgeService.Instance.CanSeeExactLocation(1, entity.EntityId),
                "Fog-off remote bunker should expose exact-location knowledge.");
        }

        private static void UpgradeRevealsProgressiveData()
        {
            ShelteredNetworkingTestContext.ResetClientContext(true);
            ShelteredMapEntity entity = RegisterRemoteBunker();

            ShelteredMapKnowledgeService.Instance.Reveal(1, entity.EntityId, MapKnowledgeLevel.Scouted, "scouted");
            ShelteredMapEntity scoutedVisible =
                ShelteredMapKnowledgeService.Instance.BuildVisibleEntity(1, entity);
            ShelteredMultiplayerMapMarker scoutedMarker =
                ShelteredMapKnowledgeService.Instance.BuildDisplayMarker(1, entity);

            TestAssert.Equal(string.Empty, scoutedVisible.DisplayName,
                "Scouted knowledge should not reveal bunker identity.");
            TestAssert.Equal(ShelteredMultiplayerMapMarkerVisualKind.RemoteBunker, scoutedMarker.VisualKind,
                "Scouted knowledge should reveal the concrete contact kind.");
            TestAssert.Equal("?", scoutedMarker.Label,
                "Scouted knowledge should still hide the display name.");
            TestAssert.True(ShelteredMapKnowledgeService.Instance.CanSeeExactLocation(1, entity.EntityId),
                "Scouted knowledge should reveal exact-location knowledge.");

            ShelteredMapKnowledgeService.Instance.Reveal(1, entity.EntityId, MapKnowledgeLevel.Identified, "identified");
            ShelteredMapEntity identifiedVisible =
                ShelteredMapKnowledgeService.Instance.BuildVisibleEntity(1, entity);
            ShelteredMultiplayerMapMarker identifiedMarker =
                ShelteredMapKnowledgeService.Instance.BuildDisplayMarker(1, entity);

            TestAssert.Equal("Remote Player", identifiedVisible.DisplayName,
                "Identified knowledge should reveal display name.");
            TestAssert.Equal(string.Empty, identifiedVisible.PayloadJson,
                "Identified knowledge should not reveal full payload details.");
            TestAssert.Equal("Remote Player", identifiedMarker.Label,
                "Identified marker should expose the display name.");

            ShelteredMapKnowledgeService.Instance.Reveal(1, entity.EntityId, MapKnowledgeLevel.Confirmed, "confirmed");
            ShelteredMapEntity confirmedVisible =
                ShelteredMapKnowledgeService.Instance.BuildVisibleEntity(1, entity);

            TestAssert.Equal("{\"owner\":3}", confirmedVisible.PayloadJson,
                "Confirmed knowledge should reveal full payload details.");
            TestAssert.Equal("online", confirmedVisible.State,
                "Confirmed knowledge should reveal full state details.");
        }

        private static void RevealIdentifiesRemoteBunker()
        {
            ShelteredNetworkingTestContext.ResetClientContext(true);
            ShelteredMapEntity entity = RegisterRemoteBunker();

            MapKnowledgeRecord record = ShelteredMapKnowledgeService.Instance.Reveal(
                1,
                entity.EntityId,
                MapKnowledgeLevel.Confirmed,
                "test-confirmed");
            ShelteredMultiplayerMapMarker marker =
                ShelteredMapKnowledgeService.Instance.BuildDisplayMarker(1, entity);

            TestAssert.Equal(MapKnowledgeLevel.Confirmed, record.KnowledgeLevel,
                "Reveal should store the requested knowledge level.");
            TestAssert.Equal("Remote Player", marker.Label,
                "Confirmed remote bunker should expose the known display name.");
            TestAssert.False(marker.IsUnknown, "Confirmed remote bunker should use its concrete visual.");
            TestAssert.True(ShelteredMapKnowledgeService.Instance.CanSeeExactLocation(1, entity.EntityId),
                "Confirmed contacts should expose exact location.");
        }

        private static void DebugRevealShowsAll()
        {
            ShelteredNetworkingTestContext.ResetClientContext(true);
            ShelteredMapKnowledgeService.DebugRevealAll = true;
            ShelteredMapEntity entity = RegisterRemoteBunker();

            ShelteredMultiplayerMapMarker marker =
                ShelteredMapKnowledgeService.Instance.BuildDisplayMarker(1, entity);

            TestAssert.Equal("Remote Player", marker.Label,
                "Debug reveal should expose remote bunker identity.");
            TestAssert.False(marker.IsUnknown, "Debug reveal should use the concrete marker visual.");
            ShelteredMapKnowledgeService.DebugRevealAll = false;
        }

        private static void ForgetRemovesRecord()
        {
            ShelteredNetworkingTestContext.ResetClientContext(true);
            ShelteredMapEntity entity = RegisterRemoteBunker();
            ShelteredMapKnowledgeService.Instance.Reveal(1, entity.EntityId, MapKnowledgeLevel.Confirmed, "test");

            bool removed = ShelteredMapKnowledgeService.Instance.Forget(1, entity.EntityId, "test-forget");

            TestAssert.True(removed, "Forget should remove an existing knowledge record.");
            TestAssert.True(ShelteredMapKnowledgeService.Instance.GetKnowledge(1, entity.EntityId) == null,
                "Forgotten knowledge should not be returned.");
        }

        private static ShelteredMapEntity RegisterRemoteBunker()
        {
            ShelteredMapEntity entity = new ShelteredMapEntity();
            entity.EntityId = "mapentity:bunker:2";
            entity.Kind = ShelteredMapEntityKind.Bunker;
            entity.OwnerPlayerId = 3;
            entity.OwnerPeerId = 7;
            entity.BunkerOwnerId = 2;
            entity.DisplayName = "Remote Player";
            entity.WorldPosition = new Vector2(10f, 20f);
            entity.MapPixels = new Vector3(100f, 200f, 0f);
            entity.GridX = 4;
            entity.GridY = 5;
            entity.IsOnline = true;
            entity.IsVisible = true;
            entity.State = "online";
            entity.PayloadJson = "{\"owner\":3}";
            entity.UpdatedWorldTick = 88;
            return ShelteredMapEntities.Upsert(entity);
        }
    }
}
