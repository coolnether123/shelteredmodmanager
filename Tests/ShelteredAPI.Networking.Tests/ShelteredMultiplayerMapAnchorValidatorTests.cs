using System.Collections.Generic;
using ModAPI.Networking;
using ShelteredAPI.Bunkers;
using ShelteredAPI.Networking.Knowledge;
using ShelteredAPI.Networking.Map;
using ShelteredAPI.Networking.World;
using UnityEngine;

namespace ShelteredAPI.Networking.Tests
{
    internal static class ShelteredMultiplayerMapAnchorValidatorTests
    {
        public static void Register(List<TestCase> tests)
        {
            tests.Add(new TestCase("MapAnchorValidator_ValidAnchorPassesUnchanged", ValidAnchorPassesUnchanged));
            tests.Add(new TestCase("MapAnchorValidator_InvalidAnchorFallsBackNearest", InvalidAnchorFallsBackNearest));
            tests.Add(new TestCase("MapAnchorValidator_TieBreakIsDeterministic", TieBreakIsDeterministic));
            tests.Add(new TestCase("MapAnchorValidator_NoValidRegionsReturnsSafeFailure", NoValidRegionsReturnsSafeFailure));
            tests.Add(new TestCase("MapAnchorValidator_FogKnowledgeMarkerDoesNotRequireValidRegion", FogKnowledgeMarkerDoesNotRequireValidRegion));
            tests.Add(new TestCase("BunkerAssignments_CreateForHostIsDeterministic", BunkerAssignmentsCreateForHostIsDeterministic));
            tests.Add(new TestCase("BunkerAssignments_ClientUsesHostSnapshotAssignment", BunkerAssignmentsClientUsesHostSnapshotAssignment));
            tests.Add(new TestCase("BunkerAnchorRuntime_CanonicalMapAnchorUsesHostAssignment", CanonicalMapAnchorUsesHostAssignment));
            tests.Add(new TestCase("BunkerAnchorRuntime_InactiveContextDoesNotExposeAnchors", InactiveContextDoesNotExposeAnchors));
        }

        private static void ValidAnchorPassesUnchanged()
        {
            FakeRegionSource regions = new FakeRegionSource(5, 5);
            regions.SetRegion(2, 3, true);

            ShelteredMultiplayerMapAnchorGridResult result =
                ShelteredMultiplayerMapAnchorFallback.ValidateGrid(2, 3, regions);

            TestAssert.True(result.HasValidRegion, "A requested grid with a region should validate.");
            TestAssert.False(result.IsFallback, "A valid requested grid should not use fallback.");
            TestAssert.Equal(2, result.ChosenGridX, "Valid anchor should keep the requested X grid.");
            TestAssert.Equal(3, result.ChosenGridY, "Valid anchor should keep the requested Y grid.");
            TestAssert.Equal("RequestedGridValid", result.Reason, "Valid anchor should report the direct-valid reason.");
        }

        private static void InvalidAnchorFallsBackNearest()
        {
            FakeRegionSource regions = new FakeRegionSource(6, 6);
            regions.SetRegion(1, 1, true);
            regions.SetRegion(5, 5, true);

            ShelteredMultiplayerMapAnchorGridResult result =
                ShelteredMultiplayerMapAnchorFallback.ValidateGrid(0, 1, regions);

            TestAssert.True(result.HasValidRegion, "Invalid requested grid should still resolve when valid regions exist.");
            TestAssert.True(result.IsFallback, "Invalid requested grid should use fallback.");
            TestAssert.Equal(1, result.ChosenGridX, "Fallback should choose the nearest valid region X.");
            TestAssert.Equal(1, result.ChosenGridY, "Fallback should choose the nearest valid region Y.");
            TestAssert.Equal(2, result.ValidRegionCount, "Fallback should report the valid region count.");
        }

        private static void TieBreakIsDeterministic()
        {
            FakeRegionSource regions = new FakeRegionSource(5, 5);
            regions.SetRegion(1, 2, true);
            regions.SetRegion(3, 2, true);

            ShelteredMultiplayerMapAnchorGridResult first =
                ShelteredMultiplayerMapAnchorFallback.ValidateGrid(2, 2, regions);
            ShelteredMultiplayerMapAnchorGridResult second =
                ShelteredMultiplayerMapAnchorFallback.ValidateGrid(2, 2, regions);

            TestAssert.True(first.IsFallback, "Tie case should be a fallback.");
            TestAssert.Equal(1, first.ChosenGridX, "Fallback ties should choose the lower X grid.");
            TestAssert.Equal(2, first.ChosenGridY, "Fallback ties should preserve the tied Y grid.");
            TestAssert.Equal(first.ChosenGridX, second.ChosenGridX, "Tie fallback X should be stable across calls.");
            TestAssert.Equal(first.ChosenGridY, second.ChosenGridY, "Tie fallback Y should be stable across calls.");
        }

        private static void NoValidRegionsReturnsSafeFailure()
        {
            FakeRegionSource regions = new FakeRegionSource(3, 3);

            ShelteredMultiplayerMapAnchorGridResult result =
                ShelteredMultiplayerMapAnchorFallback.ValidateGrid(1, 1, regions);

            TestAssert.False(result.HasValidRegion, "No valid regions should be reported as failure.");
            TestAssert.False(result.IsFallback, "No valid regions should not invent a fallback.");
            TestAssert.Equal(0, result.ValidRegionCount, "No valid regions should report zero valid cells.");
            TestAssert.Equal("NoValidMapRegions", result.Reason, "No valid regions should use the safe failure reason.");
        }

        private static void FogKnowledgeMarkerDoesNotRequireValidRegion()
        {
            ShelteredNetworkingTestContext.ResetClientContext(true);

            ShelteredMapEntity entity = new ShelteredMapEntity();
            entity.EntityId = "mapentity:bunker:99";
            entity.Kind = ShelteredMapEntityKind.Bunker;
            entity.OwnerPlayerId = 2;
            entity.OwnerPeerId = 4;
            entity.BunkerOwnerId = 99;
            entity.DisplayName = "Remote";
            entity.WorldPosition = new Vector2(999f, -999f);
            entity.MapPixels = new Vector3(50f, 60f, 0f);
            entity.GridX = -12;
            entity.GridY = 200;
            entity.IsOnline = true;
            entity.IsVisible = true;
            ShelteredMapEntities.Upsert(entity);

            ShelteredMultiplayerMapMarker marker =
                ShelteredMapKnowledgeService.Instance.BuildDisplayMarker(1, entity);

            TestAssert.True(marker != null, "Fog/knowledge marker generation should not require a valid MapRegion.");
            TestAssert.Equal("multiplayer-bunker-99", marker.MarkerId, "Bunker marker id should remain stable.");
            TestAssert.Equal("?", marker.Label, "Fog-on unknown bunker marker should not reveal identity.");
            TestAssert.True(marker.IsUnknown, "Fog-on unknown bunker marker should remain an unknown visual.");

            ShelteredMapEntities.Clear("anchor-validator-test-end");
            ShelteredMapKnowledgeService.Instance.Clear("anchor-validator-test-end");
        }

        private static void BunkerAssignmentsCreateForHostIsDeterministic()
        {
            ShelteredMultiplayerSessionContext firstContext = CreateHostContext("anchor-determinism-session");
            ShelteredMultiplayerSessionContext secondContext = CreateHostContext("anchor-determinism-session");

            ShelteredMultiplayerBunkerAssignmentSnapshot first =
                ShelteredMultiplayerBunkerAssignments.CreateForHost(firstContext);
            ShelteredMultiplayerBunkerAssignmentSnapshot second =
                ShelteredMultiplayerBunkerAssignments.CreateForHost(secondContext);

            TestAssert.Equal(first.Records.Count, second.Records.Count, "Same session and roster should produce the same assignment count.");
            for (int i = 0; i < first.Records.Count; i++)
                AssertAssignmentEqual(first.Records[i], second.Records[i], "Assignment " + i + " should be deterministic.");
        }

        private static void BunkerAssignmentsClientUsesHostSnapshotAssignment()
        {
            ResetAnchorState("client-snapshot-start");

            try
            {
                ShelteredMultiplayerBunkerAssignmentSnapshot hostSnapshot =
                    ShelteredMultiplayerBunkerAssignments.CreateForHost(CreateHostContext("anchor-client-snapshot-session"));
                ShelteredMultiplayerBunkerAssignmentRecord clientRecord = FindByPeer(hostSnapshot.Records, 5);
                TestAssert.True(clientRecord != null, "Host snapshot should include the test client peer.");

                ShelteredMultiplayerSessionCoordinator.Instance.ActivateClient(
                    hostSnapshot.SessionId,
                    clientRecord.PlayerId,
                    clientRecord.NetworkPeerId,
                    "client-a",
                    20,
                    "anchor-client-snapshot");
                ShelteredMultiplayerSessionCoordinator.Instance.SetBunkerAssignments(
                    hostSnapshot.Records.ToArray(),
                    clientRecord.PlayerId,
                    "anchor-client-snapshot");
                ShelteredMultiplayerBunkerAssignments.Apply(
                    hostSnapshot.Records.ToArray(),
                    clientRecord.PlayerId,
                    "anchor-client-snapshot");

                Vector2 activeWorldPosition;
                TestAssert.True(
                    ShelteredMultiplayerBunkerAnchorRuntime.TryGetActiveBunkerWorldPosition(out activeWorldPosition),
                    "Client should resolve its active bunker from the host-authored assignment snapshot.");
                TestAssert.Near(clientRecord.Position.x, activeWorldPosition.x, 0.0001f, "Client active bunker X should match host snapshot.");
                TestAssert.Near(clientRecord.Position.y, activeWorldPosition.y, 0.0001f, "Client active bunker Y should match host snapshot.");
            }
            finally
            {
                ResetAnchorState("client-snapshot-end");
            }
        }

        private static void CanonicalMapAnchorUsesHostAssignment()
        {
            ResetAnchorState("canonical-anchor-start");

            try
            {
                ShelteredMultiplayerBunkerAssignmentRecord[] assignments = new ShelteredMultiplayerBunkerAssignmentRecord[]
                {
                    new ShelteredMultiplayerBunkerAssignmentRecord(NetworkDefaults.HostPeerId, 1, 0, new Vector2(10f, 20f), "Host", true),
                    new ShelteredMultiplayerBunkerAssignmentRecord(5, 2, 1, new Vector2(100f, -50f), "Client", true)
                };

                ShelteredMultiplayerSessionCoordinator.Instance.ActivateClient(
                    "canonical-anchor-session",
                    2,
                    5,
                    "client",
                    20,
                    "canonical-anchor");
                ShelteredMultiplayerSessionCoordinator.Instance.SetBunkerAssignments(assignments, 2, "canonical-anchor");
                ShelteredMultiplayerBunkerAssignments.Apply(assignments, 2, "canonical-anchor");

                Vector2 activeWorldPosition;
                Vector2 canonicalWorldPosition;
                TestAssert.True(
                    ShelteredMultiplayerBunkerAnchorRuntime.TryGetActiveBunkerWorldPosition(out activeWorldPosition),
                    "Active bunker should resolve from the local player assignment.");
                TestAssert.True(
                    ShelteredMultiplayerBunkerAnchorRuntime.TryGetCanonicalMapBunkerWorldPosition(out canonicalWorldPosition),
                    "Canonical map bunker should resolve from the host assignment.");
                TestAssert.Near(100f, activeWorldPosition.x, 0.0001f, "Client active bunker X should use the local owner.");
                TestAssert.Near(-50f, activeWorldPosition.y, 0.0001f, "Client active bunker Y should use the local owner.");
                TestAssert.Near(10f, canonicalWorldPosition.x, 0.0001f, "Canonical map-generation bunker X should use host owner 0.");
                TestAssert.Near(20f, canonicalWorldPosition.y, 0.0001f, "Canonical map-generation bunker Y should use host owner 0.");
            }
            finally
            {
                ResetAnchorState("canonical-anchor-end");
            }
        }

        private static void InactiveContextDoesNotExposeAnchors()
        {
            ResetAnchorState("inactive-anchor");

            Vector3 mapPixels = ShelteredMultiplayerBunkerAnchorRuntime.GetActiveBunkerMapPixels();
            Vector2 canonicalWorldPosition;

            TestAssert.Near(0f, mapPixels.sqrMagnitude, 0.0001f, "Inactive multiplayer should not expose active bunker map pixels.");
            TestAssert.False(
                ShelteredMultiplayerBunkerAnchorRuntime.TryGetCanonicalMapBunkerWorldPosition(out canonicalWorldPosition),
                "Inactive multiplayer should not expose a canonical map-generation bunker.");
            TestAssert.False(
                ShelteredMultiplayerBunkerAnchorRuntime.IsMultiplayerAnchorActive(),
                "Inactive multiplayer should not activate map-anchor runtime behavior.");
        }

        private static ShelteredMultiplayerSessionContext CreateHostContext(string sessionId)
        {
            return new ShelteredMultiplayerSessionContext(
                ShelteredMultiplayerSessionMode.Host,
                sessionId,
                1,
                NetworkDefaults.HostPeerId,
                "host",
                20,
                0,
                0f,
                ShelteredMultiplayerGameTimeMode.HostAuthoritative,
                ShelteredMultiplayerSetupPhase.Activated,
                new ShelteredMultiplayerPeerInfo[]
                {
                    new ShelteredMultiplayerPeerInfo(NetworkDefaults.HostPeerId, true, "host", "Host", true),
                    new ShelteredMultiplayerPeerInfo(5, false, "client-a", "Client A", true),
                    new ShelteredMultiplayerPeerInfo(6, false, "client-b", "Client B", true)
                },
                new ShelteredMultiplayerBunkerAssignmentRecord[0],
                ShelteredMultiplayerSetupSettings.Empty,
                "test");
        }

        private static void AssertAssignmentEqual(
            ShelteredMultiplayerBunkerAssignmentRecord expected,
            ShelteredMultiplayerBunkerAssignmentRecord actual,
            string message)
        {
            TestAssert.True(expected != null && actual != null, message + " Records must both exist.");
            TestAssert.Equal(expected.NetworkPeerId, actual.NetworkPeerId, message + " Peer id mismatch.");
            TestAssert.Equal(expected.PlayerId, actual.PlayerId, message + " Player id mismatch.");
            TestAssert.Equal(expected.BunkerOwnerId, actual.BunkerOwnerId, message + " Bunker owner mismatch.");
            TestAssert.Near(expected.Position.x, actual.Position.x, 0.0001f, message + " Position X mismatch.");
            TestAssert.Near(expected.Position.y, actual.Position.y, 0.0001f, message + " Position Y mismatch.");
            TestAssert.Equal(expected.DisplayName, actual.DisplayName, message + " Display name mismatch.");
            TestAssert.Equal(expected.IsOnline, actual.IsOnline, message + " Online state mismatch.");
        }

        private static ShelteredMultiplayerBunkerAssignmentRecord FindByPeer(
            IList<ShelteredMultiplayerBunkerAssignmentRecord> records,
            byte peerId)
        {
            if (records == null)
                return null;

            for (int i = 0; i < records.Count; i++)
            {
                ShelteredMultiplayerBunkerAssignmentRecord record = records[i];
                if (record != null && record.NetworkPeerId == peerId)
                    return record;
            }

            return null;
        }

        private static void ResetAnchorState(string reason)
        {
            ShelteredMultiplayerSessionCoordinator.Instance.Deactivate(reason);
            ShelteredBunkers.Service.Clear();
            ShelteredMapEntities.Clear(reason);
            ShelteredMapKnowledgeService.Instance.Clear(reason);
            ShelteredMultiplayerBunkerAnchorRuntime.ResetValidatedAnchor(reason);
        }

        private sealed class FakeRegionSource : IShelteredMultiplayerMapRegionSource
        {
            private readonly bool[,] _regions;

            public FakeRegionSource(int width, int height)
            {
                Width = width;
                Height = height;
                _regions = new bool[width, height];
            }

            public int Width { get; private set; }

            public int Height { get; private set; }

            public void SetRegion(int gridX, int gridY, bool valid)
            {
                _regions[gridX, gridY] = valid;
            }

            public bool HasRegion(int gridX, int gridY)
            {
                return gridX >= 0 && gridX < Width
                    && gridY >= 0 && gridY < Height
                    && _regions[gridX, gridY];
            }

            public bool IsShelterRegion(int gridX, int gridY)
            {
                return false;
            }
        }
    }
}
