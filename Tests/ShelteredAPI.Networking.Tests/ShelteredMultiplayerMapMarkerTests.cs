using System.Collections.Generic;
using ModAPI.Networking;
using ShelteredAPI.Bunkers;
using ShelteredAPI.Networking;
using ShelteredAPI.Networking.Knowledge;
using ShelteredAPI.Networking.Map;
using ShelteredAPI.Networking.World;
using UnityEngine;

namespace ShelteredAPI.Networking.Tests
{
    internal static class ShelteredMultiplayerMapMarkerTests
    {
        public static void Register(List<TestCase> tests)
        {
            tests.Add(new TestCase("MapMarkers_ResolveLocalOwnerFromAssignments", ResolveLocalOwnerFromAssignments));
            tests.Add(new TestCase("MapMarkers_AssignmentMetadataOverridesBunkerDefaults", AssignmentMetadataOverridesBunkerDefaults));
            tests.Add(new TestCase("MapMarkers_KnowledgeMarkerUsesStableId", KnowledgeMarkerUsesStableId));
        }

        private static void ResolveLocalOwnerFromAssignments()
        {
            ShelteredMultiplayerBunkerAssignmentRecord[] assignments =
            {
                new ShelteredMultiplayerBunkerAssignmentRecord(0, 1, 0, Vector2.zero, "Host", true),
                new ShelteredMultiplayerBunkerAssignmentRecord(2, 3, 2, new Vector2(10f, 20f), "Client", true)
            };

            TestAssert.Equal(2,
                ShelteredMultiplayerMapMarkerAssignmentResolver.ResolveLocalBunkerOwnerId(assignments, 3),
                "Local bunker owner should resolve from the coordinator assignment player id.");
            TestAssert.Equal(4,
                ShelteredMultiplayerMapMarkerAssignmentResolver.ResolveLocalBunkerOwnerId(assignments, 5),
                "Missing assignments should fall back to player id minus one.");
        }

        private static void AssignmentMetadataOverridesBunkerDefaults()
        {
            BunkerDefinition bunker = new BunkerDefinition(
                2,
                new Vector2(10f, 20f),
                "Stale Name",
                true,
                false,
                NetworkDefaults.UnassignedPeerId);
            ShelteredMultiplayerBunkerAssignmentRecord assignment =
                new ShelteredMultiplayerBunkerAssignmentRecord(7, 3, 2, bunker.Position, "Remote Player", true);

            TestAssert.Equal("multiplayer-bunker-2",
                ShelteredMultiplayerMapMarkerAssignmentResolver.CreateMarkerId(2),
                "Marker ids should be stable across map openings.");
            TestAssert.Equal("Remote Player",
                ShelteredMultiplayerMapMarkerAssignmentResolver.ResolveLabel(bunker, assignment),
                "Assignment display name should win over stale bunker names.");
            TestAssert.Equal((byte)7,
                ShelteredMultiplayerMapMarkerAssignmentResolver.ResolvePeerId(bunker, assignment),
                "Assignment peer id should win over stale bunker peer ids.");
            TestAssert.True(
                ShelteredMultiplayerMapMarkerAssignmentResolver.ResolveOnlineState(bunker, assignment),
                "Assignment online state should win over stale bunker online state.");
        }

        private static void KnowledgeMarkerUsesStableId()
        {
            ShelteredMapEntities.Clear("marker-test");
            ShelteredMapKnowledgeService.Instance.Clear("marker-test");

            ShelteredMapEntity entity = new ShelteredMapEntity();
            entity.EntityId = "mapentity:bunker:3";
            entity.Kind = ShelteredMapEntityKind.Bunker;
            entity.OwnerPlayerId = 4;
            entity.OwnerPeerId = 9;
            entity.BunkerOwnerId = 3;
            entity.DisplayName = "Remote";
            entity.MapPixels = new Vector3(33f, 44f, 0f);
            entity.IsOnline = false;
            entity.IsVisible = true;
            ShelteredMapEntities.Upsert(entity);

            ShelteredMultiplayerMapMarker marker =
                ShelteredMapKnowledgeService.Instance.BuildDisplayMarker(1, entity);

            TestAssert.Equal("multiplayer-bunker-3", marker.MarkerId,
                "Knowledge-built bunker markers should keep stable bunker marker ids.");
            TestAssert.Equal("?", marker.Label,
                "Unrevealed remote bunker markers should display as question marks.");
            TestAssert.False(marker.IsOnline, "Marker should preserve offline state.");

            ShelteredMapEntities.Clear("marker-test-end");
            ShelteredMapKnowledgeService.Instance.Clear("marker-test-end");
        }
    }
}
