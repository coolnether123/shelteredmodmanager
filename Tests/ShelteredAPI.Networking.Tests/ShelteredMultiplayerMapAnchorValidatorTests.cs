using System.Collections.Generic;
using ModAPI.Networking;
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
