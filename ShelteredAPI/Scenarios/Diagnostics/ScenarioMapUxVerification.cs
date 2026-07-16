using System;
using System.Globalization;
using System.Text;

using ModAPI.Core;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Application.Map;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Map;
using ShelteredAPI.Scenarios.Infrastructure.Runtime;

namespace ShelteredAPI.Scenarios.Diagnostics
{
    internal static class ScenarioMapUxVerification
    {
        public static void Verify(ScenarioValidationResult result)
        {
            VerifyLootPreview(result);
            VerifyDuplicate(result);
            VerifyProjectionMap(result);
            VerifyTerrainBrushDraft(result);
        }

        private static void VerifyLootPreview(ScenarioValidationResult result)
        {
            const int fixedSeed = 731942;
            ModRandom.ResetForSaveSeed(fixedSeed);
            ScenarioDefinition definition = new ScenarioDefinition { Id = "Scenario.MapUx" };
            MapLocationDefinition location = new MapLocationDefinition { Id = "mapux-location", GridX = 2, GridY = 7, LootTableId = "mapux-loot" };
            MapLootTableDefinition table = new MapLootTableDefinition { Id = "mapux-loot" };
            table.Entries.Add(new MapLootEntryDefinition { ItemId = "Water", MinQuantity = 1, MaxQuantity = 3, Weight = 3, Chance = 1f });
            table.Entries.Add(new MapLootEntryDefinition { ItemId = "Food", MinQuantity = 2, MaxQuantity = 4, Weight = 1, Chance = 0.65f });
            definition.Map.Locations.Add(location);
            definition.Map.LootTables.Add(table);

            ScenarioMapLootPreview preview = ScenarioMapLootPreviewService.Build(definition, location, fixedSeed, 1000);
            string runtimeSignature = ScenarioMapProjectionApplyService.BuildLootRollSignature(
                ScenarioMapProjectionApplyService.PlanLootRolls(definition, location, table));
            Assert(string.Equals(runtimeSignature, ScenarioMapProjectionApplyService.BuildLootRollSignature(preview.ExactRoll), StringComparison.Ordinal),
                "MAPUX loot preview must match the runtime fixed-seed roll.", result);

            ScenarioMapLootPreview repeated = ScenarioMapLootPreviewService.Build(definition, location, fixedSeed, 1000);
            Assert(string.Equals(DistributionSignature(preview), DistributionSignature(repeated), StringComparison.Ordinal),
                "MAPUX loot distribution must be deterministic for a supplied simulation seed.", result);
        }

        private static void VerifyDuplicate(ScenarioValidationResult result)
        {
            MapAuthoringDefinition map = new MapAuthoringDefinition();
            MapLocationDefinition source = new MapLocationDefinition
            {
                Id = "source",
                GridX = 1,
                GridY = 1,
                LootTableId = "loot",
                EncounterTableId = "encounters",
                Danger = 22
            };
            source.Properties.Add(new ScenarioProperty { Key = "custom", Value = "kept" });
            map.Locations.Add(source);

            MapLocationDefinition rejected;
            string error;
            Assert(!ScenarioMapLocationDuplicateService.TryDuplicateAtGrid(map, source.Id, 1, 1, 1f, 1f, out rejected, out error),
                "MAPUX duplicate must reject the source cell.", result);

            MapLocationDefinition first;
            Assert(ScenarioMapLocationDuplicateService.TryDuplicateAtGrid(map, source.Id, 4, 5, 4f, 5f, out first, out error),
                "MAPUX duplicate should create a copy at a new empty cell.", result);
            Assert(first != null && !string.Equals(first.Id, source.Id, StringComparison.OrdinalIgnoreCase)
                && string.Equals(first.LootTableId, source.LootTableId, StringComparison.Ordinal)
                && string.Equals(first.EncounterTableId, source.EncounterTableId, StringComparison.Ordinal)
                && first.Properties.Count >= source.Properties.Count,
                "MAPUX duplicate must issue a fresh id and copy loot, encounter, and properties.", result);

            MapLocationDefinition second;
            Assert(ScenarioMapLocationDuplicateService.TryDuplicateAtGrid(map, source.Id, 6, 5, 6f, 5f, out second, out error)
                && !string.Equals(first.Id, second.Id, StringComparison.OrdinalIgnoreCase),
                "MAPUX duplicate ids must remain unique.", result);
        }

        private static void VerifyProjectionMap(ScenarioValidationResult result)
        {
            ScenarioMapProjectionField[] fields = ScenarioMapProjectionFieldCatalog.GetEncounterFields();
            int projected = 0;
            int storedOnly = 0;
            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i].AppliesInGame) projected++;
                else storedOnly++;
            }
            Assert(ScenarioMapProjectionFieldCatalog.IsSynchronized(),
                "MAPUX encounter projection descriptors and executable apply actions must stay synchronized.", result);
            Assert(projected == 4 && storedOnly == 2,
                "MAPUX encounter projection matrix must cover four runtime chance fields plus stored-only entries and properties.", result);
        }

        private static void VerifyTerrainBrushDraft(ScenarioValidationResult result)
        {
            ScenarioEditorSession session = new ScenarioEditorSession
            {
                WorkingDefinition = new ScenarioDefinition { Id = "Scenario.MapBrush" }
            };
            ScenarioMapDraftService service = new ScenarioMapDraftService();
            MapTerrainPatchDefinition round = service.PaintTerrainArea(
                session, 10, 6, ScenarioMapTerrainModes.GeneratedBlend, MapTerrainBrushShape.Circle, 5);
            MapTerrainPatchDefinition square = service.PaintTerrainArea(
                session, 10, 6, "Woodland", MapTerrainBrushShape.Rectangle, 3);

            Assert(round != null
                && round.Shape == MapTerrainBrushShape.Circle
                && Math.Abs(round.X - 10.5f) < 0.001f
                && Math.Abs(round.Y - 6.5f) < 0.001f
                && Math.Abs(round.Radius - 2.5f) < 0.001f,
                "MAPUX round generated-blend brush must persist centered five-cell geometry.", result);
            Assert(square != null
                && square.Shape == MapTerrainBrushShape.Rectangle
                && Math.Abs(square.X - 9f) < 0.001f
                && Math.Abs(square.Y - 5f) < 0.001f
                && Math.Abs(square.Width - 3f) < 0.001f
                && session.WorkingDefinition.Map.TerrainPatches.Count == 2,
                "MAPUX overlapping brush strokes must persist as ordered area patches.", result);
        }

        private static string DistributionSignature(ScenarioMapLootPreview preview)
        {
            StringBuilder signature = new StringBuilder();
            for (int i = 0; preview != null && i < preview.Distribution.Count; i++)
            {
                ScenarioMapLootDistributionEntry entry = preview.Distribution[i];
                signature.Append(entry.Hidden).Append(':').Append(entry.ItemId).Append(':')
                    .Append(entry.PercentOfRolls.ToString("0.000", CultureInfo.InvariantCulture)).Append(':')
                    .Append(entry.AverageQuantityPerRoll.ToString("0.000", CultureInfo.InvariantCulture)).Append('|');
            }
            return signature.ToString();
        }

        private static void Assert(bool condition, string message, ScenarioValidationResult result)
        {
            if (!condition && result != null)
                result.AddError(message);
        }
    }
}
