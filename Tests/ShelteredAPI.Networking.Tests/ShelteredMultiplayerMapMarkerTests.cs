using System.Collections.Generic;
using ModAPI.Networking;
using ShelteredAPI.Bunkers;
using ShelteredAPI.Networking;
using ShelteredAPI.Networking.Map;
using UnityEngine;

namespace ShelteredAPI.Networking.Tests
{
    internal static class ShelteredMultiplayerMapMarkerTests
    {
        public static void Register(List<TestCase> tests)
        {
            tests.Add(new TestCase("MapMarkers_ResolveLocalOwnerFromAssignments", ResolveLocalOwnerFromAssignments));
            tests.Add(new TestCase("MapMarkers_AssignmentMetadataOverridesBunkerDefaults", AssignmentMetadataOverridesBunkerDefaults));
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
    }
}
