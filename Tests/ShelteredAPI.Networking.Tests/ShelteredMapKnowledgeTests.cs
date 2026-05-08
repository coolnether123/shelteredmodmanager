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
            tests.Add(new TestCase("MapKnowledge_RevealIdentifiesRemoteBunker", RevealIdentifiesRemoteBunker));
            tests.Add(new TestCase("MapKnowledge_DebugRevealShowsAll", DebugRevealShowsAll));
            tests.Add(new TestCase("MapKnowledge_ForgetRemovesRecord", ForgetRemovesRecord));
        }

        private static void RemoteBunkerDefaultsToQuestionMarker()
        {
            ResetClientContext(true);
            ShelteredMapEntity entity = RegisterRemoteBunker();

            ShelteredMultiplayerMapMarker marker =
                ShelteredMapKnowledgeService.Instance.BuildDisplayMarker(1, entity);

            TestAssert.True(marker != null, "Remote bunker should produce a display marker.");
            TestAssert.Equal("?", marker.Label, "Unknown remote bunker should not expose the bunker name.");
            TestAssert.True(marker.IsUnknown, "Unknown remote bunker should use the unknown visual.");
            TestAssert.False(ShelteredMapKnowledgeService.Instance.CanSeeExactLocation(1, entity.EntityId),
                "Suspicious contacts should not count as exact location knowledge.");
        }

        private static void RevealIdentifiesRemoteBunker()
        {
            ResetClientContext(true);
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
            ResetClientContext(true);
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
            ResetClientContext(true);
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
            entity.UpdatedWorldTick = 88;
            return ShelteredMapEntities.Upsert(entity);
        }

        private static void ResetClientContext(bool fog)
        {
            ShelteredMapEntities.Clear("knowledge-test");
            ShelteredMapKnowledgeService.Instance.Clear("knowledge-test");
            ShelteredMapKnowledgeService.DebugRevealAll = false;
            ShelteredMultiplayerSessionCoordinator.Instance.Deactivate("knowledge-test-reset");
            ShelteredMultiplayerSessionCoordinator.Instance.ActivateClient(
                "knowledge-test-session",
                1,
                NetworkDefaults.HostPeerId,
                "client",
                20,
                "knowledge-test-client");
            ShelteredMultiplayerSessionCoordinator.Instance.BeginSetupPreparation(
                new ShelteredMultiplayerSetupSettings(0, 0, 1, 1, 1, 1, 1, 0, fog),
                "knowledge-test-settings");
        }
    }
}
