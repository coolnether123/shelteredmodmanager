using ShelteredAPI.Scenarios.Diagnostics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using ModAPI.Core;
using ModAPI.Scenarios;
using ShelteredAPI.Content;
using ShelteredAPI.Infrastructure;
using ShelteredAPI.Saves;
using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Application.Authoring.Supplies;
using ShelteredScenarioEditor.Application.Runtime;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Conditions;
using ShelteredAPI.Scenarios.Domain.Effects;
using ShelteredAPI.Scenarios.Domain.Compatibility;
using ShelteredAPI.Scenarios.Domain.Map;
using ShelteredAPI.Scenarios.Domain.Runtime;
using ShelteredAPI.Scenarios.Domain.Scheduling;
using ShelteredScenarioEditor.Presentation.Authoring.Shell;
using ShelteredScenarioEditor.Infrastructure.Resilience;
namespace ShelteredScenarioEditor.Diagnostics{
    /// <summary>
    /// Executable verification harness for the scenario framework. This follows the
    /// existing smoke-test style and avoids a test framework so it can run under the
    /// .NET Framework 3.5 game runtime.
    /// </summary>
    internal static class ScenarioEditorFrameworkVerification
    {
        public static ScenarioValidationResult Run()
        {
            ScenarioValidationResult result = new ScenarioValidationResult();
            string root = Path.Combine(Path.GetTempPath(), "SMMScenarioEditorFrameworkVerification_" + Guid.NewGuid().ToString("N"));

            try
            {
                Directory.CreateDirectory(root);
                VerifyEditorSeamRecovery(result);
                VerifyRoundTripAndCatalog(root, result);
                VerifyScoringValidation(result);
                VerifyAssetEscapes(root, result);
                VerifySecureXmlParsing(result);
                VerifyAtomicScenarioWrites(root, result);
                VerifyDraftDeleteDurability(root, result);
                VerifyInventoryProjectionReconciliation(result);
                VerifySuppliesAuthoring(result);
                VerifyWizInfoContent(result);
                ScenarioStarterTemplateVerification.Verify(result);
                ScenarioTimelineUxVerification.Verify(result);
                ScenarioStoryGraphUxVerification.Verify(result);
                ScenarioAssetInventoryVerification.Verify(root, result);
                ScenarioAuthoringShortcutHelpVerification.Verify(result);
                ScenarioAuthorTestChecklistVerification.Verify(root, result);
                ScenarioAuthoringActionCoverageVerification.Verify(result);
                ScenarioAuthoringActionCoverageVerification.VerifyWorkspaceFoundation(result);
                ScenarioWorkspaceRoutingVerification.Verify(result);
                ScenarioAuthoringDisplayNameVerification.Verify(result);
                ScenarioSuppliesWorkspaceVerification.Verify(result);
                ScenarioMapWorkspaceVerification.Verify(result);
            }
            catch (Exception ex)
            {
                result.AddError("Scenario framework verification failed: " + ex.Message);
            }
            finally
            {
                TryDelete(root);
            }

            return result;
        }

        private static void VerifyEditorSeamRecovery(ScenarioValidationResult result)
        {
            ScenarioEditorSeamGuard.ResetForDiagnostics();
            int attempts = 0;
            int value;
            string message;
            bool retried = ScenarioEditorSeamGuard.Try<int>(
                "diagnostics.retry",
                ScenarioEditorSeamRecoveryPolicy.RetryOnce,
                delegate
                {
                    attempts++;
                    if (attempts == 1)
                        throw new InvalidOperationException("expected first-attempt failure");
                    return 7;
                },
                -1,
                "Retry diagnostic degraded.",
                null,
                out value,
                out message);
            Assert(retried && attempts == 2 && value == 7,
                "Editor seam retry policy did not recover on its second attempt.", result);

            bool restored = false;
            bool succeeded = ScenarioEditorSeamGuard.Run(
                "diagnostics.restore",
                ScenarioEditorSeamRecoveryPolicy.RestoreState,
                delegate { throw new InvalidOperationException("expected restore failure"); },
                "Restore diagnostic degraded.",
                delegate { restored = true; },
                out message);
            Assert(!succeeded && restored && !string.IsNullOrEmpty(ScenarioEditorSeamGuard.BuildSystemHealthLine()),
                "Editor seam restore policy did not recover and report degraded health.", result);
            ScenarioEditorSeamGuard.ResetForDiagnostics();
        }

        private static void VerifyRoundTripAndCatalog(string root, ScenarioValidationResult result)
        {
            string scenarioFile = CreateScenarioPack(root, "PackOne", "Scenario.PackOne", "Assets\\icon.png");
            ScenarioEditorDefinitionSerializer serializer = new ScenarioEditorDefinitionSerializer();
            ScenarioDefinition loaded = serializer.Load(scenarioFile);
            string xml = serializer.ToXml(loaded);
            ScenarioDefinition roundTrip = serializer.FromXml(xml);

            Assert(string.Equals(xml, serializer.ToXml(roundTrip), StringComparison.Ordinal), "Scenario XML round-trip changed the definition.", result);
            Assert(loaded.Dependencies.Count == 2, "Scenario dependency declarations were not parsed.", result);
            Assert(loaded.FamilySetup.Members.Count == 1, "Family member definition was not parsed.", result);
            Assert(loaded.FamilySetup.Members[0].Stats.Count == 1, "Family stat override was not parsed.", result);
            Assert(loaded.FamilySetup.Members[0].Traits.Count == 1, "Family trait override was not parsed.", result);
            Assert(loaded.FamilySetup.Members[0].Skills.Count == 1, "Family skill override was not parsed.", result);
            Assert(loaded.BunkerEdits.ObjectPlacements.Count == 1, "Object placement was not parsed.", result);
            Assert(loaded.BackendWorlds != null
                && loaded.BackendWorlds.Find(loaded.BaseGameMode) != null
                && loaded.BackendWorlds.Find(loaded.BaseGameMode).BunkerEdits.ObjectPlacements.Count == 1,
                "Legacy current world was not migrated into the active backend world.", result);
            Assert(loaded.TriggersAndEvents.Triggers.Count == 1, "Trigger definition was not parsed.", result);
            Assert(loaded.WinLossConditions.WinConditions.Count == 1, "Win condition was not parsed.", result);
            Assert(loaded.Scoring != null, "Scenario scoring definition was not initialized.", result);
            Assert(loaded.Scoring.Categories.Count == 1, "Score category was not parsed.", result);
            Assert(loaded.Scoring.Rules.Count == 1, "Score rule was not parsed.", result);
            Assert(loaded.Scoring.Rules.Count > 0 && string.Equals(loaded.Scoring.Rules[0].Source, "daysSurvived", StringComparison.OrdinalIgnoreCase), "Score rule source was not parsed.", result);

            ScenarioDefinition launchPolicy = CreateDefinition("Scenario.LaunchPolicy");
            launchPolicy.LaunchSetup.Mode = ScenarioLaunchSetupMode.Guided;
            launchPolicy.LaunchSetup.Categories[0].AuthoredValue = 3;
            launchPolicy.LaunchSetup.Categories[0].PlayerSelectable = false;
            ScenarioDefinition launchRoundTrip = serializer.FromXml(serializer.ToXml(launchPolicy));
            Assert(launchRoundTrip.LaunchSetup.Mode == ScenarioLaunchSetupMode.Guided
                && launchRoundTrip.LaunchSetup.Categories.Count == 7
                && launchRoundTrip.LaunchSetup.Categories[0].AuthoredValue == 3
                && !launchRoundTrip.LaunchSetup.Categories[0].PlayerSelectable,
                "Launch setup policy did not round-trip through scenario XML.", result);

            ScenarioDefinition legacyLaunch = serializer.FromXml("<Scenario><Meta><Id>Scenario.LegacyLaunch</Id><DisplayName>Legacy Launch</DisplayName></Meta></Scenario>");
            Assert(legacyLaunch.LaunchSetup != null && legacyLaunch.LaunchSetup.Mode == ScenarioLaunchSetupMode.FullSetup,
                "Legacy XML without LaunchSetup did not preserve the full vanilla setup flow.", result);

            launchPolicy.LaunchSetup.Categories.Add(new ScenarioDifficultyCategoryDefinition { Id = "future-difficulty", AuthoredValue = 2, PlayerSelectable = false });
            ScenarioValidationResult launchValidation = new ScenarioEditorDefinitionValidator().Validate(launchPolicy, null);
            Assert(ContainsIssue(launchValidation, "Unknown launch difficulty category"),
                "Unknown launch difficulty categories were not reported as validation warnings.", result);

            ScenarioDefinition familyChoice = CreateDefinition("Scenario.BaseFamilyChoice");
            familyChoice.BaseFamilyChoice = ShelteredAPI.Scenarios.Definitions.ScenarioBaseFamilyChoices.KeepCurrentCast;
            ScenarioDefinition familyChoiceRoundTrip = serializer.FromXml(serializer.ToXml(familyChoice));
            Assert(string.Equals(familyChoiceRoundTrip.BaseFamilyChoice, ShelteredAPI.Scenarios.Definitions.ScenarioBaseFamilyChoices.KeepCurrentCast, StringComparison.Ordinal),
                "Base family choice did not round-trip through scenario XML.", result);

            ScenarioDefinition backendWorlds = CreateDefinition("Scenario.BackendWorlds");
            ScenarioBackendWorldDefinition stasisWorld = backendWorlds.BackendWorlds.GetOrCreate(ScenarioBaseGameMode.Stasis);
            stasisWorld.BunkerEdits.ObjectPlacements.Add(new ObjectPlacement { DefinitionReference = "Bed" });
            stasisWorld.SceneSpritePlacements.Add(new SceneSpritePlacement { Id = "stasis_sprite", SpriteId = "main" });
            ScenarioDefinition backendRoundTrip = serializer.FromXml(serializer.ToXml(backendWorlds));
            Assert(backendRoundTrip.BackendWorlds.Find(ScenarioBaseGameMode.Survival).BunkerEdits.ObjectPlacements.Count == 1,
                "Active backend world was not stored during XML write.", result);
            Assert(backendRoundTrip.BackendWorlds.Find(ScenarioBaseGameMode.Stasis).BunkerEdits.ObjectPlacements.Count == 1,
                "Inactive backend world did not round-trip.", result);
            Assert(backendRoundTrip.BackendWorlds.Find(ScenarioBaseGameMode.Stasis).SceneSpritePlacements.Count == 1
                && string.Equals(backendRoundTrip.BackendWorlds.Find(ScenarioBaseGameMode.Stasis).SceneSpritePlacements[0].Id, "stasis_sprite", StringComparison.Ordinal),
                "Inactive backend scene sprite placements did not round-trip.", result);

            ScenarioDefinition legacyBackend = serializer.FromXml(
                "<Scenario><Meta><Id>Scenario.LegacyBackend</Id><DisplayName>Legacy Backend</DisplayName></Meta><BaseMode>Stasis</BaseMode><BunkerEdits><RoomChanges /><ObjectPlacements><ObjectPlacement definition=\"Generator\"><Position x=\"1\" y=\"2\" z=\"0\" /><Rotation x=\"0\" y=\"0\" z=\"0\" /><Tags /><CustomProperties /></ObjectPlacement></ObjectPlacements></BunkerEdits></Scenario>");
            Assert(legacyBackend.BackendWorlds.Find(ScenarioBaseGameMode.Stasis) != null
                && legacyBackend.BackendWorlds.Find(ScenarioBaseGameMode.Stasis).BunkerEdits.ObjectPlacements.Count == 1,
                "Legacy single-world XML did not migrate into the selected backend.", result);
            Assert(legacyBackend.BackendWorlds.Find(ScenarioBaseGameMode.Survival) == null,
                "Legacy single-world XML should leave other backends empty until authored.", result);

            ScenarioDefinition missingScoring = serializer.FromXml("<Scenario><Meta><Id>Scenario.NoScoring</Id><DisplayName>No Scoring</DisplayName></Meta></Scenario>");
            Assert(missingScoring.Scoring != null, "Missing <Scoring> did not create a default scoring definition.", result);
            Assert(!missingScoring.Scoring.Enabled && missingScoring.Scoring.Rules.Count == 0,
                "Missing <Scoring> did not preserve default disabled scoring.", result);

        }

        private static void VerifyInventoryProjectionReconciliation(ScenarioValidationResult result)
        {
            Dictionary<string, int> previous = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            previous["Water"] = 2;
            previous["Food"] = 1;

            Dictionary<string, int> authored = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            authored["Water"] = 5;

            List<InventoryProjectionDelta> deltas = ScenarioAuthoringInventoryProjectionService.PlanProjectionDeltas(previous, authored);
            Assert(FindDelta(deltas, "Water") == 3, "Inventory projection did not compute the authored add delta.", result);
            Assert(FindDelta(deltas, "Food") == -1, "Inventory projection did not compute the authored removal delta.", result);

            List<InventoryProjectionDelta> idempotent = ScenarioAuthoringInventoryProjectionService.PlanProjectionDeltas(authored, authored);
            Assert(idempotent.Count == 0, "Inventory projection is not idempotent for an unchanged authored stockpile.", result);

            Dictionary<string, int> live = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            live["Water"] = 7;
            live["Food"] = 4;
            Dictionary<string, int> seed = ScenarioAuthoringInventoryProjectionService.BuildProjectionSeed(authored, live);
            Assert(seed.ContainsKey("Water") && seed["Water"] == 5, "Inventory projection seed should cap live authored items at the authored quantity.", result);
            Assert(!seed.ContainsKey("Food"), "Inventory projection seed should not claim extra live-only items as projected draft items.", result);

            Dictionary<string, int> liveAdd = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            liveAdd["Water"] = 5;
            liveAdd["Food"] = 2;
            List<InventoryProjectionDelta> liveAddDeltas = ScenarioAuthoringInventoryProjectionService.PlanProjectionDeltas(authored, liveAdd);
            Assert(FindDelta(liveAddDeltas, "Food") == 2, "Live-truth reverse reconciliation did not detect a live add into the draft.", result);

            Dictionary<string, int> liveRemove = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            liveRemove["Water"] = 3;
            List<InventoryProjectionDelta> liveRemoveDeltas = ScenarioAuthoringInventoryProjectionService.PlanProjectionDeltas(authored, liveRemove);
            Assert(FindDelta(liveRemoveDeltas, "Water") == -2, "Live-truth reverse reconciliation did not detect a live removal into the draft.", result);
            Assert(ScenarioAuthoringInventoryProjectionService.SnapshotsEqual(authored, authored), "Live-truth reconciliation should treat matching draft/live snapshots as no-op to avoid feedback loops.", result);
        }

        private static int FindDelta(List<InventoryProjectionDelta> deltas, string itemId)
        {
            for (int i = 0; deltas != null && i < deltas.Count; i++)
            {
                InventoryProjectionDelta delta = deltas[i];
                if (delta != null && string.Equals(delta.ItemId, itemId, StringComparison.OrdinalIgnoreCase))
                    return delta.QuantityDelta;
            }

            return 0;
        }

        private static void VerifySuppliesAuthoring(ScenarioValidationResult result)
        {
            // Preset apply produces the advertised stacks.
            List<ItemEntry> balanced = ScenarioSuppliesPresetCatalog.BuildStacks(ScenarioSuppliesPresetCatalog.PresetBalanced);
            Assert(FindQuantity(balanced, "Water") == 8, "Balanced preset did not advertise the expected water stack.", result);
            Assert(FindQuantity(balanced, "Ration") == 8, "Balanced preset did not advertise the expected food stack.", result);
            Assert(FindQuantity(balanced, "FirstAid") == 2, "Balanced preset did not advertise the expected first aid stack.", result);
            List<ItemEntry> empty = ScenarioSuppliesPresetCatalog.BuildStacks(ScenarioSuppliesPresetCatalog.PresetEmpty);
            Assert(empty.Count == 0, "Empty preset should advertise no stacks.", result);

            // Preset apply is undoable via the authoring history snapshot mechanism.
            ScenarioAuthoringHistoryService history = new ScenarioAuthoringHistoryService();
            ScenarioDefinition definition = CreateDefinition("Scenario.SuppliesPreset");
            definition.StartingInventory.Items.Clear();
            history.RecordAuthoringChange(definition, "Apply Scarce loadout", ScenarioDirtySection.Inventory, ScenarioEditCategory.Inventory);
            List<ItemEntry> scarce = ScenarioSuppliesPresetCatalog.BuildStacks(ScenarioSuppliesPresetCatalog.PresetScarce);
            for (int i = 0; i < scarce.Count; i++)
                definition.StartingInventory.Items.Add(new ItemEntry { ItemId = scarce[i].ItemId, Quantity = scarce[i].Quantity });
            Assert(definition.StartingInventory.Items.Count == scarce.Count && scarce.Count > 0, "Applying the Scarce preset did not populate the starting inventory.", result);
            string undoDescription;
            Assert(history.Undo(definition, out undoDescription), "Preset apply snapshot could not be undone.", result);
            Assert(definition.StartingInventory.Items.Count == 0, "Undo did not restore the starting inventory to before the preset.", result);

            // Duplicate merge and zero-quantity policy: duplicates sum, non-positive stacks drop.
            List<ItemEntry> messy = new List<ItemEntry>();
            messy.Add(new ItemEntry { ItemId = "Water", Quantity = 3 });
            messy.Add(new ItemEntry { ItemId = "Ration", Quantity = 2 });
            messy.Add(new ItemEntry { ItemId = "water", Quantity = 4 });
            messy.Add(new ItemEntry { ItemId = "Wood", Quantity = 0 });
            messy.Add(new ItemEntry { ItemId = "Metal", Quantity = -1 });
            ScenarioSuppliesInventoryNormalizer.NormalizeResult normalize = ScenarioSuppliesInventoryNormalizer.Normalize(messy);
            Assert(normalize.MergedStacks == 1, "Duplicate merge did not report the merged stack.", result);
            Assert(normalize.RemovedStacks == 2, "Zero and negative quantity stacks were not removed.", result);
            Assert(messy.Count == 2, "Normalize should leave exactly the merged water and food stacks.", result);
            Assert(FindQuantity(messy, "Water") == 7, "Duplicate water stacks were not summed.", result);
            Assert(FindQuantity(messy, "Wood") == 0, "Zero-quantity stack should be removed, not retained.", result);

            // Balance math on a known fixture: 6 water, 2 food, 3 survivors, no medicine.
            StartingInventoryDefinition fixture = new StartingInventoryDefinition();
            fixture.Items.Add(new ItemEntry { ItemId = "Water", Quantity = 6 });
            fixture.Items.Add(new ItemEntry { ItemId = "Ration", Quantity = 2 });
            ScenarioSuppliesBalanceEstimator.BalanceEstimate estimate = ScenarioSuppliesBalanceEstimator.Estimate(fixture, 3);
            Assert(estimate.WaterUnits == 6 && Approximately(estimate.WaterDays, 2.0), "Balance estimator computed the wrong water-days.", result);
            Assert(estimate.FoodUnits == 2 && Approximately(estimate.FoodDays, 2.0 / 3.0), "Balance estimator computed the wrong food-days.", result);
            Assert(!estimate.HasFirstAid && estimate.MedicineUnits == 0, "Balance estimator should report no medicine for the fixture.", result);
            Assert(estimate.MissingEssentials.Contains("No first aid"), "Balance estimator should flag the missing first aid essential.", result);

            StartingInventoryDefinition emptyFixture = new StartingInventoryDefinition();
            ScenarioSuppliesBalanceEstimator.BalanceEstimate emptyEstimate = ScenarioSuppliesBalanceEstimator.Estimate(emptyFixture, 0);
            Assert(emptyEstimate.SurvivorCount == ScenarioSuppliesBalanceEstimator.DefaultSurvivorCount, "Balance estimator should fall back to the default cast size.", result);
            Assert(emptyEstimate.MissingEssentials.Count == 3, "Empty start should flag all three missing essentials.", result);
        }

        private static int FindQuantity(List<ItemEntry> items, string itemId)
        {
            for (int i = 0; items != null && i < items.Count; i++)
            {
                if (items[i] != null && string.Equals(items[i].ItemId, itemId, StringComparison.OrdinalIgnoreCase))
                    return items[i].Quantity;
            }
            return 0;
        }

        private static bool Approximately(double left, double right)
        {
            return Math.Abs(left - right) < 0.001;
        }

#if false // API runtime projection and scheduling policy are verified by API-owned contracts.
        private static void VerifyMapLootProjectionContracts(ScenarioValidationResult result)
        {
            ModRandom.ResetForSaveSeed(24681357);

            ScenarioDefinition definition = CreateDefinition("Scenario.MapLootProjection");
            MapLocationDefinition location = new MapLocationDefinition();
            location.Id = "test-region";
            location.GridX = 3;
            location.GridY = 4;
            location.LootTableId = "weighted";
            location.ReplaceGeneratedLoot = true;
            definition.Map.Locations.Add(location);

            MapLootTableDefinition table = new MapLootTableDefinition();
            table.Id = "weighted";
            table.Entries.Add(new MapLootEntryDefinition
            {
                ItemId = "Water",
                MinQuantity = 2,
                MaxQuantity = 4,
                Weight = 3,
                Chance = 1f
            });
            table.Entries.Add(new MapLootEntryDefinition
            {
                ItemId = "Food",
                MinQuantity = 1,
                MaxQuantity = 2,
                Weight = 1,
                Chance = 1f,
                Hidden = true,
                HiddenUnlockItemId = "LockpickSet"
            });
            definition.Map.LootTables.Add(table);

            string first = ScenarioMapProjectionApplyService.BuildLootRollSignature(
                ScenarioMapProjectionApplyService.PlanLootRolls(definition, location, table));
            string second = ScenarioMapProjectionApplyService.BuildLootRollSignature(
                ScenarioMapProjectionApplyService.PlanLootRolls(definition, location, table));
            Assert(string.Equals(first, second, StringComparison.Ordinal), "Map loot rolls must be deterministic for the same scenario seed, location, and table.", result);
            Assert(first.IndexOf("hidden:", StringComparison.OrdinalIgnoreCase) >= 0, "Map loot rolls must preserve hidden loot entries.", result);

            MapLocationDefinition otherLocation = new MapLocationDefinition();
            otherLocation.Id = "other-region";
            otherLocation.GridX = 5;
            otherLocation.GridY = 4;
            string third = ScenarioMapProjectionApplyService.BuildLootRollSignature(
                ScenarioMapProjectionApplyService.PlanLootRolls(definition, otherLocation, table));
            Assert(!string.Equals(first, third, StringComparison.Ordinal), "Map loot rolls should vary by location identity/grid under the same scenario seed.", result);

            ScenarioDefinition invalid = CreateDefinition("Scenario.InvalidMapLoot");
            MapLocationDefinition badLocation = new MapLocationDefinition();
            badLocation.Id = "bad-location";
            badLocation.ReplaceGeneratedLoot = true;
            invalid.Map.Locations.Add(badLocation);
            ScenarioValidationResult validation = new ScenarioEditorDefinitionValidator().Validate(invalid, null);
            Assert(ContainsIssue(validation, "cannot replace generated loot without a lootTableId"), "Map validation did not reject replaceGeneratedLoot without a loot table.", result);

            badLocation.LootTableId = "weighted";
            badLocation.VisibleAtStart = true;
            badLocation.HiddenUntilDiscovered = true;
            invalid.Map.LootTables.Add(table);
            validation = new ScenarioEditorDefinitionValidator().Validate(invalid, null);
            Assert(ContainsIssue(validation, "cannot be both VisibleAtStart and HiddenUntilDiscovered"), "Map validation did not reject contradictory visibility flags.", result);
        }

        private static void VerifySchedulePolicyWindows(ScenarioValidationResult result)
        {
            ScenarioScheduledActionDefinition action = new ScenarioScheduledActionDefinition();
            action.Id = "window_policy";
            action.DueTime.Day = 5;
            action.DueTime.Hour = 8;
            action.DueTime.Minute = 0;
            action.Policy.Repeatable = true;
            action.Policy.CooldownMinutes = 60;
            action.Policy.WindowEndDay = 5;
            action.Policy.Chance = 1f;
            action.Policy.JitterMinutes = 0;
            action.Policy.MaxRuns = 2;
            action.Effects.Add(new ScenarioEffectDefinition { Kind = ScenarioEffectKind.WorldEvent });

            string reason;
            long due = ScenarioSchedulePolicyEvaluator.ToGameMinutes(5, 8, 0);
            Assert(ScenarioSchedulePolicyEvaluator.Evaluate(action, due - 1, 0, 0, null, out reason) == ScenarioSchedulePolicyDecision.NotDue,
                "Schedule window fired before DueTime.", result);
            Assert(ScenarioSchedulePolicyEvaluator.Evaluate(action, due, 0, 0, null, out reason) == ScenarioSchedulePolicyDecision.Due,
                "Schedule window did not fire at DueTime.", result);
            Assert(ScenarioSchedulePolicyEvaluator.Evaluate(action, due + 60, 2, 2, due, out reason) == ScenarioSchedulePolicyDecision.NotDue,
                "Schedule maxRuns did not cap repeatable execution.", result);
            Assert(ScenarioSchedulePolicyEvaluator.Evaluate(action, ScenarioSchedulePolicyEvaluator.ToGameMinutes(6, 0, 0), 0, 0, null, out reason) == ScenarioSchedulePolicyDecision.NotDue,
                "Schedule windowEndDay did not close the random window.", result);

            action.Policy.Chance = 0f;
            Assert(ScenarioSchedulePolicyEvaluator.Evaluate(action, due, 0, 0, null, out reason) == ScenarioSchedulePolicyDecision.Skipped,
                "Schedule chance=0 did not produce a skipped decision.", result);
            action.Policy.Chance = 1f;
            action.Policy.JitterMinutes = 30;
            Assert(ScenarioSchedulePolicyEvaluator.Evaluate(action, due - 1, 0, 0, null, out reason) == ScenarioSchedulePolicyDecision.NotDue,
                "Schedule jitter allowed execution before the base due time.", result);
        }

#endif
        private static void VerifyScoringValidation(ScenarioValidationResult result)
        {
            ScenarioDefinition enabledWithoutRules = CreateDefinition("Scenario.ScoringNoRules");
            enabledWithoutRules.Scoring.Rules.Clear();
            ScenarioValidationResult noRules = new ScenarioEditorDefinitionValidator().Validate(enabledWithoutRules, null);
            Assert(ContainsIssue(noRules, "no score rules"), "Enabled scoring without rules did not produce a warning.", result);

            ScenarioDefinition invalid = CreateDefinition("Scenario.ScoringInvalid");
            invalid.Scoring.Categories.Add(new ScenarioScoreCategoryDefinition { Id = "survival", DisplayName = "Duplicate" });
            invalid.Scoring.Rules[0].CategoryId = "missing";
            invalid.Scoring.Rules[0].Source = string.Empty;
            ScenarioValidationResult validation = new ScenarioEditorDefinitionValidator().Validate(invalid, null);
            Assert(ContainsIssue(validation, "duplicated"), "Duplicate score category was not reported.", result);
            Assert(ContainsIssue(validation, "unknown category"), "Unknown score category reference was not reported.", result);
            Assert(ContainsIssue(validation, "requires a source"), "Missing score rule source was not reported.", result);
        }

        private static void VerifyAssetEscapes(string root, ScenarioValidationResult result)
        {
            string pack1File = CreateScenarioPack(root, "Pack1", "Scenario.AssetEscape", "Assets\\icon.png");
            string pack2 = Path.Combine(root, "Pack2");
            Directory.CreateDirectory(pack2);
            File.WriteAllBytes(Path.Combine(pack2, "file.png"), new byte[] { 1, 2, 3, 4 });

            ScenarioEditorDefinitionSerializer serializer = new ScenarioEditorDefinitionSerializer();
            ScenarioDefinition definition = serializer.Load(pack1File);
            definition.AssetReferences.CustomIcons.Clear();
            definition.AssetReferences.CustomIcons.Add(new IconRef { Id = "escaped", RelativePath = "..\\Pack2\\file.png" });

            ScenarioValidationResult validation = new ScenarioEditorDefinitionValidator().Validate(definition, pack1File);
            Assert(ContainsIssue(validation, "escapes the scenario pack folder"), "Sibling-prefix asset escape was not blocked.", result);
        }

        private static void VerifySecureXmlParsing(ScenarioValidationResult result)
        {
            try
            {
                new ScenarioEditorDefinitionSerializer().FromXml("<!DOCTYPE Scenario [<!ENTITY xxe SYSTEM \"file:///C:/Windows/win.ini\">]><Scenario><Meta><Id>&xxe;</Id><DisplayName>Invalid</DisplayName></Meta></Scenario>");
                Assert(false, "Scenario XML parser allowed a DTD/external entity declaration.", result);
            }
            catch (XmlException)
            {
            }
        }

#if false // Save id guarding is owned and verified by ShelteredAPI.Saves.
        private static void VerifyScenarioSaveIdGuards(ScenarioValidationResult result)
        {
            string[] reservedIds = new string[]
            {
                ScenarioSaveIdGuards.StandardStorageScenarioId,
                ScenarioSaveIdGuards.VanillaSurroundedScenarioId,
                ScenarioSaveIdGuards.VanillaStasisScenarioId,
                ScenarioSaveIdGuards.ScenarioAuthoringDraftStorageScenarioId
            };

            for (int i = 0; i < reservedIds.Length; i++)
            {
                string reservedId = reservedIds[i];
                Assert(ScenarioSaveIdGuards.IsReservedStorageId(reservedId), "Reserved scenario save id was not recognized: " + reservedId, result);
                AssertThrowsReserved(delegate { ScenarioSaveIdGuards.RequireCustomScenarioId(reservedId, "ScenarioEditorFrameworkVerification"); },
                    "Reserved scenario save id was accepted by the custom scenario guard: " + reservedId,
                    result);
            }

            string customId = "com.example.scenario.valid";
            Assert(!ScenarioSaveIdGuards.IsReservedStorageId(customId), "Valid custom scenario save id was treated as reserved.", result);
            Assert(ScenarioSaveIdGuards.RequireCustomScenarioId(customId, "ScenarioEditorFrameworkVerification") == customId,
                "Valid custom scenario save id was not preserved by the guard.", result);

            AssertThrowsReserved(delegate { ShelteredSaves.DeleteScenario(ScenarioSaveIdGuards.StandardStorageScenarioId, "__verification__"); },
                "DeleteScenario accepted the Standard save root through the custom scenario facade.",
                result);
            AssertThrowsReserved(delegate { ShelteredSaves.OverwriteScenario(ScenarioSaveIdGuards.StandardStorageScenarioId, "__verification__", null, new byte[0]); },
                "OverwriteScenario accepted the Standard save root through the custom scenario facade.",
                result);
        }

        private static void AssertThrowsReserved(Action action, string message, ScenarioValidationResult result)
        {
            try
            {
                if (action != null)
                    action();
                Assert(false, message, result);
            }
            catch (ArgumentException ex)
            {
                Assert(ex.Message.IndexOf("reserved for built-in saves", StringComparison.OrdinalIgnoreCase) >= 0,
                    "Reserved scenario save id guard threw without an actionable message.",
                    result);
            }
        }

#endif
        private static void VerifyAtomicScenarioWrites(string root, ScenarioValidationResult result)
        {
            string scenarioFile = Path.Combine(Path.Combine(root, "Atomic"), ScenarioEditorDefinitionSerializer.DefaultFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(scenarioFile));

            ScenarioEditorDefinitionSerializer serializer = new ScenarioEditorDefinitionSerializer();
            ScenarioDefinition original = CreateDefinition("Scenario.Atomic");
            original.DisplayName = "Original Atomic Scenario";
            serializer.Save(original, scenarioFile);
            string originalXml = File.ReadAllText(scenarioFile);

            ScenarioDefinition updated = CreateDefinition("Scenario.Atomic");
            updated.DisplayName = "Updated Atomic Scenario";
            serializer.Save(updated, scenarioFile);

            string backupPath = scenarioFile + ".bak";
            Assert(File.Exists(backupPath), "Atomic scenario save did not create a .bak file when replacing an existing scenario.xml.", result);
            Assert(serializer.Load(scenarioFile).DisplayName == "Updated Atomic Scenario", "Atomic scenario save did not write a parseable replacement scenario.xml.", result);
            Assert(serializer.Load(backupPath).DisplayName == "Original Atomic Scenario", "Atomic scenario save backup did not preserve the previous parseable scenario.xml.", result);

            using (FileStream locked = File.Open(scenarioFile, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                try
                {
                    ScenarioDefinition blocked = CreateDefinition("Scenario.Atomic");
                    blocked.DisplayName = "Blocked Atomic Scenario";
                    serializer.Save(blocked, scenarioFile);
                    Assert(false, "Atomic scenario save unexpectedly succeeded while scenario.xml was locked.", result);
                }
                catch (IOException)
                {
                }
            }

            string afterFailureXml = File.ReadAllText(scenarioFile);
            Assert(string.Equals(afterFailureXml, serializer.ToXml(updated), StringComparison.Ordinal)
                || afterFailureXml.IndexOf("Updated Atomic Scenario", StringComparison.Ordinal) >= 0,
                "Failed atomic scenario save did not leave the previous scenario.xml intact.", result);
            Assert(originalXml.IndexOf("Original Atomic Scenario", StringComparison.Ordinal) >= 0, "Verification setup failed to capture the original XML.", result);
        }

#if false // API cache/carrier/discovery internals are verified by API-owned contracts.
        private static void VerifyDraftMetadataCachePersistence(string root, ScenarioValidationResult result)
        {
            string originalPath;
            string originalOwner;
            ScenarioDefinitionMetadataCache.GetPersistentStoreConfiguration(out originalPath, out originalOwner);
            string owner = "verification.drafts";
            string cachePath = Path.Combine(root, "draft-metadata-cache.json");
            string scenarioPath = Path.Combine(root, "cached-scenario.xml");
            ScenarioDefinition definition = new ScenarioDefinition();
            definition.Id = "Verification.PersistentDraft";
            definition.DisplayName = "Persistent Draft";
            definition.Author = "Contract";
            definition.Version = "1.0";
            definition.Description = "Metadata sidecar contract.";
            definition.BaseGameMode = ScenarioBaseGameMode.Stasis;
            ScenarioEditorDefinitionSerializer serializer = new ScenarioEditorDefinitionSerializer();
            serializer.Save(definition, scenarioPath);

            try
            {
                ScenarioDefinitionMetadataCache.ConfigurePersistentStore(cachePath, owner);
                CountingDefinitionSerializer firstLoader = new CountingDefinitionSerializer(serializer);
                ScenarioDefinitionMetadata metadata;
                Assert(ScenarioDefinitionMetadataCache.TryLoad(firstLoader, scenarioPath, owner, out metadata)
                    && firstLoader.LoadCount == 1, "Draft metadata cache did not populate from the source XML.", result);
                ScenarioDefinitionMetadataCache.FlushPersistentStoreForVerification();

                ScenarioDefinitionMetadataCache.ConfigurePersistentStore(null, null);
                ScenarioDefinitionMetadataCache.ConfigurePersistentStore(cachePath, owner);
                CountingDefinitionSerializer persistedLoader = new CountingDefinitionSerializer(serializer);
                Assert(ScenarioDefinitionMetadataCache.TryLoad(persistedLoader, scenarioPath, owner, out metadata)
                    && persistedLoader.LoadCount == 0
                    && metadata != null && metadata.Info != null
                    && metadata.BaseGameMode == ScenarioBaseGameMode.Stasis
                    && string.Equals(metadata.Description, definition.Description, StringComparison.Ordinal),
                    "Unchanged draft metadata did not load from the persistent sidecar without parsing XML.", result);

                File.SetLastWriteTimeUtc(scenarioPath, File.GetLastWriteTimeUtc(scenarioPath).AddSeconds(2));
                Assert(ScenarioDefinitionMetadataCache.TryLoad(persistedLoader, scenarioPath, owner, out metadata)
                    && persistedLoader.LoadCount == 1,
                    "Draft metadata cache accepted an entry whose exact file stamp changed.", result);

                string corruptCachePath = Path.Combine(root, "corrupt-draft-metadata-cache.json");
                ScenarioDefinitionMetadataCache.ConfigurePersistentStore(null, null);
                File.WriteAllText(corruptCachePath, "{ corrupt sidecar");
                ScenarioDefinitionMetadataCache.ConfigurePersistentStore(corruptCachePath, owner);
                CountingDefinitionSerializer corruptFallbackLoader = new CountingDefinitionSerializer(serializer);
                Assert(ScenarioDefinitionMetadataCache.TryLoad(corruptFallbackLoader, scenarioPath, owner, out metadata)
                    && corruptFallbackLoader.LoadCount == 1,
                    "A corrupt draft metadata sidecar did not fall back silently to rebuilding from XML.", result);
            }
            finally
            {
                ScenarioDefinitionMetadataCache.ConfigurePersistentStore(originalPath, originalOwner);
            }
        }

        private static void VerifyCompletionCarrierContract(ScenarioValidationResult result)
        {
            ScenarioDefinition definition = CreateDefinition("Scenario.CompletionCarrier");
            definition.ScenarioFlow.Stages.Add(new ScenarioFlowStageDefinition { Id = "authored-stage" });

            ScenarioDef carrier = ScenarioDefinitionService.BuildScenarioDef(definition);

            Assert(carrier != null && carrier.IsScenario(),
                "Completion carrier was not a vanilla ScenarioDef.", result);
            Assert(carrier != null && carrier.stages.Count == 0,
                "Completion carrier projected an authored stage into vanilla visitor flow.", result);
            Assert(carrier != null && string.Equals(carrier.id, definition.Id, StringComparison.Ordinal),
                "Completion carrier did not preserve the authored scenario id used by outcome resolution.", result);

            ScenarioDef playable = ScenarioDefinitionService.BuildPlayableScenarioDef(definition);
            Assert(playable != null && playable.stages.Count == 0,
                "Playable carrier should ignore authored stages that have no intercom content.", result);

            definition.ScenarioFlow.Stages[0].IntercomStages.Add(new ScenarioIntercomStageDefinition { Id = "opening" });
            playable = ScenarioDefinitionService.BuildPlayableScenarioDef(definition);
            Assert(playable != null && playable.stages.Count == 1,
                "Playable carrier did not project an authored intercom stage into vanilla story flow.", result);
        }

        private static void VerifyScenarioSaveDiscoveryExcludesSoftDeletedFolders(string root, ScenarioValidationResult result)
        {
            string scenarioRoot = Path.Combine(root, "save-discovery");
            int slot;
            Assert(SaveRegistryCore.TryGetActiveSlotNumber(scenarioRoot, Path.Combine(scenarioRoot, "Slot_1"), out slot) && slot == 1,
                "Top-level scenario Slot_* folders must remain discoverable.", result);
            Assert(!SaveRegistryCore.TryGetActiveSlotNumber(scenarioRoot, Path.Combine(Path.Combine(scenarioRoot, "_trash"), "Slot_1_deleted"), out slot),
                "Scenario save discovery must exclude nested _trash entries.", result);
            Assert(!SaveRegistryCore.TryGetActiveSlotNumber(scenarioRoot, Path.Combine(Path.Combine(scenarioRoot, "_corrupt"), "Slot_2"), out slot),
                "Scenario save discovery must exclude nested soft-deleted/quarantine entries.", result);
        }

#endif
        private static void VerifyDraftDeleteDurability(string root, ScenarioValidationResult result)
        {
            string draftsRoot = Path.Combine(root, "DraftDeleteDurability");
            string fixtureDraft = Path.Combine(draftsRoot, "Slot_7");
            string preservedDraft = Path.Combine(draftsRoot, "Slot_23");
            string historyRoot = Path.Combine(fixtureDraft, ".history");
            Directory.CreateDirectory(historyRoot);
            Directory.CreateDirectory(preservedDraft);
            File.WriteAllText(Path.Combine(fixtureDraft, ScenarioEditorDefinitionSerializer.DefaultFileName), "fixture scenario");
            File.WriteAllText(Path.Combine(fixtureDraft, ScenarioEditorDefinitionSerializer.DefaultFileName + ".bak"), "fixture backup");
            File.WriteAllText(Path.Combine(historyRoot, "autosave.xml"), "fixture autosave");
            File.WriteAllText(Path.Combine(preservedDraft, ScenarioEditorDefinitionSerializer.DefaultFileName), "keepsake scenario");

            bool deleted = ScenarioAuthoringDraftRepository.DeleteDraftDirectory(draftsRoot, fixtureDraft);
            Assert(deleted, "Fixture draft delete did not report durable success.", result);
            Assert(!Directory.Exists(fixtureDraft), "Deleted fixture draft folder remains in the draft root.", result);
            Assert(!File.Exists(Path.Combine(fixtureDraft, ScenarioEditorDefinitionSerializer.DefaultFileName)), "Deleted fixture scenario.xml remains on disk.", result);
            Assert(!File.Exists(Path.Combine(fixtureDraft, ScenarioEditorDefinitionSerializer.DefaultFileName + ".bak")), "Deleted fixture scenario.xml.bak remains on disk.", result);
            Assert(!File.Exists(Path.Combine(historyRoot, "autosave.xml")), "Deleted fixture autosave history remains on disk.", result);
            Assert(Directory.Exists(preservedDraft), "Draft delete touched an unrelated Slot_23 fixture.", result);

            // A restart-safe catalog simulation deliberately performs a fresh disk
            // scan: a deleted draft must have neither a catalog source file nor a
            // quarantined scenario.xml that could be rediscovered by a future scan.
            string[] remainingScenarioFiles = Directory.GetFiles(draftsRoot, ScenarioEditorDefinitionSerializer.DefaultFileName, SearchOption.AllDirectories);
            Assert(remainingScenarioFiles.Length == 1
                && string.Equals(remainingScenarioFiles[0], Path.Combine(preservedDraft, ScenarioEditorDefinitionSerializer.DefaultFileName), StringComparison.OrdinalIgnoreCase),
                "Fresh draft catalog scan rediscovered deleted fixture data.", result);
        }

        private static string CreateScenarioPack(string root, string packName, string scenarioId, string assetPath)
        {
            string packRoot = Path.Combine(Path.Combine(root, packName), "Scenarios\\Main");
            string assetFullPath = Path.Combine(packRoot, assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(assetFullPath));
            File.WriteAllBytes(assetFullPath, new byte[] { 137, 80, 78, 71 });

            ScenarioDefinition definition = CreateDefinition(scenarioId);
            definition.Dependencies.Add("Required.Mod@1.3.0");
            definition.Dependencies.Add("Optional.Mod");
            definition.AssetReferences.CustomIcons.Add(new IconRef { Id = "main", RelativePath = assetPath });

            string scenarioFile = Path.Combine(packRoot, ScenarioEditorDefinitionSerializer.DefaultFileName);
            new ScenarioEditorDefinitionSerializer().Save(definition, scenarioFile);
            return scenarioFile;
        }

        private static ScenarioDefinition CreateDefinition(string scenarioId)
        {
            ScenarioDefinition definition = new ScenarioDefinition();
            definition.Id = scenarioId;
            definition.DisplayName = "Verification Scenario";
            definition.Description = "Used by the scenario framework verification harness.";
            definition.Author = "SMM";
            definition.Version = "1.0.0";

            FamilyMemberConfig member = new FamilyMemberConfig();
            member.Name = "Alex";
            member.Gender = ScenarioGender.Female;
            member.Stats.Add(new StatOverride { StatId = "Strength", Value = 7 });
            member.Traits.Add("Strength:Courageous");
            member.Skills.Add(new SkillOverride { SkillId = "Crafting", Level = 2 });
            definition.FamilySetup.Members.Add(member);

            definition.StartingInventory.OverrideRandomStart = true;
            definition.StartingInventory.Items.Add(new ItemEntry { ItemId = "Water", Quantity = 2 });

            ObjectPlacement placement = new ObjectPlacement();
            placement.DefinitionReference = "Generator";
            placement.Position.X = 1f;
            placement.Position.Y = -2f;
            placement.CustomProperties.Add(new ScenarioProperty { Key = "level", Value = "1" });
            definition.BunkerEdits.ObjectPlacements.Add(placement);

            TriggerDef trigger = new TriggerDef();
            trigger.Id = "day-3";
            trigger.Type = "day";
            trigger.Properties.Add(new ScenarioProperty { Key = "day", Value = "3" });
            definition.TriggersAndEvents.Triggers.Add(trigger);

            ScenarioConditionRef condition = new ScenarioConditionRef();
            condition.Id = "survive-7-days";
            condition.Kind = ScenarioConditionKind.SurviveDays;
            condition.Quantity = 7;
            definition.WinLossConditions.WinConditions.Add(condition);

            definition.Scoring.Enabled = true;
            definition.Scoring.ScoreLabel = "Points";
            definition.Scoring.LeaderboardKey = "verification";
            definition.Scoring.Categories.Add(new ScenarioScoreCategoryDefinition
            {
                Id = "survival",
                DisplayName = "Survival",
                Description = "Days survived and end-state progress.",
                SortOrder = 10
            });
            ScenarioScoreRuleDefinition scoreRule = new ScenarioScoreRuleDefinition();
            scoreRule.Id = "days-survived";
            scoreRule.CategoryId = "survival";
            scoreRule.DisplayName = "Days Survived";
            scoreRule.Source = "daysSurvived";
            scoreRule.Operation = "Add";
            scoreRule.Weight = 1f;
            scoreRule.Properties.Add(new ScenarioProperty { Key = "metric", Value = "GameTime.Day" });
            definition.Scoring.Rules.Add(scoreRule);
            return definition;
        }

        private static bool ContainsIssue(ScenarioValidationResult result, string text)
        {
            if (result == null || text == null)
                return false;

            ScenarioValidationIssue[] issues = result.Issues;
            for (int i = 0; i < issues.Length; i++)
            {
                if (issues[i] != null && issues[i].Message.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static bool HasDependency(ScenarioModDependency[] dependencies, string modId, string version)
        {
            for (int i = 0; dependencies != null && i < dependencies.Length; i++)
            {
                ScenarioModDependency dependency = dependencies[i];
                if (dependency != null
                    && string.Equals(dependency.modId, modId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(dependency.version ?? string.Empty, version ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void VerifyWizInfoContent(ScenarioValidationResult result)
        {
            ScenarioEditorDefinitionSerializer serializer = new ScenarioEditorDefinitionSerializer();

            // Goal field XML round-trip + backward compatibility.
            ScenarioDefinition withGoal = new ScenarioDefinition();
            withGoal.Id = "wizinfo.goal";
            withGoal.Goal = "Reach the surface before day 30.";
            ScenarioDefinition goalRoundTrip = serializer.FromXml(serializer.ToXml(withGoal));
            Assert(goalRoundTrip != null && goalRoundTrip.Goal == withGoal.Goal, "Scenario goal did not survive XML round-trip.", result);

            ScenarioDefinition noGoal = new ScenarioDefinition();
            noGoal.Id = "wizinfo.nogoal";
            string legacyXml = serializer.ToXml(noGoal);
            Assert(legacyXml.IndexOf("<Goal", StringComparison.Ordinal) < 0, "Absent scenario goal must not be written to XML (backward compatible).", result);
            ScenarioDefinition legacyReload = serializer.FromXml(legacyXml);
            Assert(legacyReload != null && string.IsNullOrEmpty(legacyReload.Goal), "Scenario without a goal must load with an empty goal.", result);

            // Installed-copy content summary derived from a fixture definition.
            ScenarioDefinition fixture = new ScenarioDefinition();
            fixture.Id = "wizinfo.summary";
            fixture.FamilySetup.Members.Add(new FamilyMemberConfig());
            fixture.FamilySetup.Members.Add(new FamilyMemberConfig());
            fixture.ScenarioCharacters.Add(new ScenarioNpcDefinition());
            fixture.BunkerEdits.ObjectPlacements.Add(new ObjectPlacement());
            fixture.ScenarioFlow.Stages.Add(new ScenarioFlowStageDefinition());
            fixture.ScheduledActions.Add(new ScenarioScheduledActionDefinition());
            fixture.Map.Locations.Add(new ShelteredAPI.Scenarios.Domain.Map.MapLocationDefinition());
            fixture.AssetReferences.CustomSprites.Add(new SpriteRef { Id = "s", RelativePath = "Assets\\a.png" });
            fixture.ModDependencies.Add(new ScenarioModDependencyDefinition { ModId = "Req.Mod", Kind = ScenarioModDependencyKind.Required });

            ScenarioContentSummary summary = ScenarioContentSummary.Build(fixture);
            Assert(summary.WorldChanges == 1, "Content summary world-change count is wrong.", result);
            Assert(summary.Cast == 3, "Content summary cast count is wrong.", result);
            Assert(summary.StoryStages == 1, "Content summary story-stage count is wrong.", result);
            Assert(summary.TimelineEntries == 1, "Content summary timeline count is wrong.", result);
            Assert(summary.MapLocations == 1, "Content summary map-location count is wrong.", result);
            Assert(summary.AssetFiles == 1, "Content summary asset-file count is wrong.", result);
            Assert(summary.RequiredMods == 1, "Content summary required-mod count is wrong.", result);
            Assert(summary.ToCardLine().IndexOf("3 cast members", StringComparison.Ordinal) >= 0, "Content summary card line is malformed.", result);

            // Top-issue ranking: blocking error wins over warnings regardless of order.
            ScenarioValidationResult mixed = new ScenarioValidationResult();
            mixed.AddWarning("First warning about supplies.");
            mixed.AddError("Add at least one starting survivor.");
            mixed.AddWarning("Second warning about assets.");
            ScenarioAuthoringValidationSnapshot mixedSnapshot = ScenarioAuthoringValidationSnapshot.Evaluate(new StubValidator(mixed), fixture, null);
            ScenarioValidationIssue top = ShelteredScenarioEditor.Presentation.Authoring.Shell.ScenarioTopIssueResolver.ResolveTopIssue(mixedSnapshot);
            Assert(top != null && top.Severity == ScenarioIssueSeverity.Error, "Top issue must rank the blocking error first.", result);
            Assert(ShelteredScenarioEditor.Presentation.Authoring.Shell.ScenarioTopIssueResolver.BuildNextAction(top) != null, "Top issue must resolve to a fix action.", result);

            ScenarioValidationResult warningsOnly = new ScenarioValidationResult();
            warningsOnly.AddWarning("Earliest warning.");
            warningsOnly.AddWarning("Later warning.");
            ScenarioAuthoringValidationSnapshot warnSnapshot = ScenarioAuthoringValidationSnapshot.Evaluate(new StubValidator(warningsOnly), fixture, null);
            ScenarioValidationIssue topWarning = ShelteredScenarioEditor.Presentation.Authoring.Shell.ScenarioTopIssueResolver.ResolveTopIssue(warnSnapshot);
            Assert(topWarning != null && topWarning.Message == "Earliest warning.", "Warning-only ranking must return the first warning.", result);
        }

        private sealed class StubValidator : IScenarioDefinitionValidator
        {
            private readonly ScenarioValidationResult _result;
            public StubValidator(ScenarioValidationResult result) { _result = result; }
            public ScenarioValidationResult Validate(ScenarioDefinition definition, string scenarioFilePath) { return _result; }
        }

        private static void Assert(bool condition, string message, ScenarioValidationResult result)
        {
            if (!condition && result != null)
                result.AddError(message);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch
            {
            }
        }

        private sealed class VerificationFolderSource : IScenarioModFolderSource
        {
            private readonly string _root;

            public VerificationFolderSource(string root)
            {
                _root = root;
            }

            public ScenarioModFolder[] GetLoadedModFolders()
            {
                List<ScenarioModFolder> folders = new List<ScenarioModFolder>();
                string[] directories = Directory.GetDirectories(_root);
                for (int i = 0; i < directories.Length; i++)
                    folders.Add(new ScenarioModFolder(Path.GetFileName(directories[i]), directories[i]));
                return folders.ToArray();
            }
        }

        private sealed class CountingDefinitionSerializer : IScenarioDefinitionSerializer
        {
            private readonly ScenarioEditorDefinitionSerializer _inner;

            public CountingDefinitionSerializer(ScenarioEditorDefinitionSerializer inner)
            {
                _inner = inner;
            }

            public int LoadCount { get; private set; }

            public ScenarioDefinition Load(string filePath)
            {
                LoadCount++;
                return _inner.Load(filePath);
            }

            public bool TryLoadWithRecovery(string filePath, out ScenarioDefinition definition, out string recoveryMessage, out bool recovered)
            {
                LoadCount++;
                return _inner.TryLoadWithRecovery(filePath, out definition, out recoveryMessage, out recovered);
            }

            public ScenarioDefinition FromXml(string xml) { return _inner.FromXml(xml); }
            public void Save(ScenarioDefinition definition, string filePath) { _inner.Save(definition, filePath); }
            public string ToXml(ScenarioDefinition definition) { return _inner.ToXml(definition); }
            public ScenarioInfo LoadInfo(string filePath, string ownerModId) { return _inner.LoadInfo(filePath, ownerModId); }
        }
    }
}
