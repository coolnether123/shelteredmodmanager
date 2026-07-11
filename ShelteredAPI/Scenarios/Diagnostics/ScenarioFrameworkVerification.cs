using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using ModAPI.Core;
using ModAPI.Scenarios;
using ShelteredAPI.Content;
using ShelteredAPI.Infrastructure;
using ShelteredAPI.Saves;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Authoring.Supplies;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Effects;
using ShelteredAPI.Scenarios.Domain.Compatibility;
using ShelteredAPI.Scenarios.Domain.Map;
using ShelteredAPI.Scenarios.Domain.Runtime;
using ShelteredAPI.Scenarios.Domain.Scheduling;
using ShelteredAPI.Scenarios.Application.Scheduling;
using ShelteredAPI.Scenarios.Infrastructure.Runtime;
using ShelteredAPI.Scenarios.Infrastructure.Serialization;
namespace ShelteredAPI.Scenarios.Diagnostics{
    /// <summary>
    /// Executable verification harness for the scenario framework. This follows the
    /// existing smoke-test style and avoids a test framework so it can run under the
    /// .NET Framework 3.5 game runtime.
    /// </summary>
    internal static class ScenarioFrameworkVerification
    {
        public static ScenarioValidationResult Run()
        {
            ScenarioValidationResult result = new ScenarioValidationResult();
            string root = Path.Combine(Path.GetTempPath(), "SMMScenarioFrameworkVerification_" + Guid.NewGuid().ToString("N"));

            try
            {
                Directory.CreateDirectory(root);
                VerifyRoundTripAndCatalog(root, result);
                VerifyScoringValidation(result);
                VerifyDependencies(result);
                VerifyAssetEscapes(root, result);
                VerifySecureXmlParsing(result);
                VerifyScenarioSaveIdGuards(result);
                VerifyAtomicScenarioWrites(root, result);
                VerifyDraftDeleteDurability(root, result);
                VerifyMissingDefinitionRefreshRetry(result);
                VerifyInventoryProjectionReconciliation(result);
                VerifySuppliesAuthoring(result);
                VerifyMapLootProjectionContracts(result);
                VerifySchedulePolicyWindows(result);
                VerifySeamGuardContracts(result);
                VerifyWizInfoContent(result);
                ScenarioStarterTemplateVerification.Verify(result);
                ScenarioTimelineUxVerification.Verify(result);
                ScenarioAssetInventoryVerification.Verify(root, result);
                ScenarioAuthoringShortcutHelpVerification.Verify(result);
                ScenarioAuthorTestChecklistVerification.Verify(root, result);
                ScenarioAuthoringActionCoverageVerification.Verify(result);
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

        private static void VerifyRoundTripAndCatalog(string root, ScenarioValidationResult result)
        {
            string scenarioFile = CreateScenarioPack(root, "PackOne", "Scenario.PackOne", "Assets\\icon.png");
            ScenarioDefinitionSerializer serializer = new ScenarioDefinitionSerializer();
            ScenarioDefinition loaded = serializer.Load(scenarioFile);
            string xml = serializer.ToXml(loaded);
            ScenarioDefinition roundTrip = serializer.FromXml(xml);

            Assert(ScenarioDefinitionComparer.AreEquivalent(loaded, roundTrip), "Scenario XML round-trip changed the definition.", result);
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
            ScenarioValidationResult launchValidation = new ScenarioValidator(new VerificationDependencyResolver(null, null)).Validate(launchPolicy, null);
            Assert(ContainsIssue(launchValidation, "Unknown launch difficulty category"),
                "Unknown launch difficulty categories were not reported as validation warnings.", result);

            ScenarioDefinition familyChoice = CreateDefinition("Scenario.BaseFamilyChoice");
            familyChoice.BaseFamilyChoice = ScenarioBaseFamilyChoices.KeepCurrentCast;
            ScenarioDefinition familyChoiceRoundTrip = serializer.FromXml(serializer.ToXml(familyChoice));
            Assert(string.Equals(familyChoiceRoundTrip.BaseFamilyChoice, ScenarioBaseFamilyChoices.KeepCurrentCast, StringComparison.Ordinal),
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
            backendRoundTrip.BaseGameMode = ScenarioBaseGameMode.Stasis;
            ScenarioBackendWorldMaterializer.MaterializeCurrentWorld(backendRoundTrip, ScenarioBaseGameMode.Stasis);
            Assert(backendRoundTrip.BunkerEdits.ObjectPlacements.Count == 1
                && string.Equals(backendRoundTrip.BunkerEdits.ObjectPlacements[0].DefinitionReference, "Bed", StringComparison.Ordinal),
                "Inactive backend world did not materialize as the current world.", result);
            Assert(backendRoundTrip.AssetReferences.SceneSpritePlacements.Count == 1
                && string.Equals(backendRoundTrip.AssetReferences.SceneSpritePlacements[0].Id, "stasis_sprite", StringComparison.Ordinal),
                "Backend scene sprite placements did not materialize as the current world.", result);

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

            ScenarioCatalog catalog = new ScenarioCatalog(new VerificationFolderSource(root), serializer);
            catalog.Refresh();
            ScenarioInfo[] scenarios = catalog.ListAll();
            Assert(scenarios.Length == 1, "Scenario catalog did not discover exactly one scenario.xml pack.", result);
            Assert(scenarios.Length == 0 || string.Equals(scenarios[0].Id, "Scenario.PackOne", StringComparison.OrdinalIgnoreCase),
                "Scenario catalog discovered the wrong scenario id.", result);
        }

        private static void VerifyInventoryProjectionReconciliation(ScenarioValidationResult result)
        {
            Dictionary<string, int> previous = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            previous["Water"] = 2;
            previous["Food"] = 1;

            Dictionary<string, int> authored = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            authored["Water"] = 5;

            List<InventoryProjectionDelta> deltas = InventoryApplyService.PlanProjectionDeltas(previous, authored);
            Assert(FindDelta(deltas, "Water") == 3, "Inventory projection did not compute the authored add delta.", result);
            Assert(FindDelta(deltas, "Food") == -1, "Inventory projection did not compute the authored removal delta.", result);

            List<InventoryProjectionDelta> idempotent = InventoryApplyService.PlanProjectionDeltas(authored, authored);
            Assert(idempotent.Count == 0, "Inventory projection is not idempotent for an unchanged authored stockpile.", result);

            Dictionary<string, int> live = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            live["Water"] = 7;
            live["Food"] = 4;
            Dictionary<string, int> seed = InventoryApplyService.BuildProjectionSeed(authored, live);
            Assert(seed.ContainsKey("Water") && seed["Water"] == 5, "Inventory projection seed should cap live authored items at the authored quantity.", result);
            Assert(!seed.ContainsKey("Food"), "Inventory projection seed should not claim extra live-only items as projected draft items.", result);

            Dictionary<string, int> liveAdd = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            liveAdd["Water"] = 5;
            liveAdd["Food"] = 2;
            List<InventoryProjectionDelta> liveAddDeltas = InventoryApplyService.PlanProjectionDeltas(authored, liveAdd);
            Assert(FindDelta(liveAddDeltas, "Food") == 2, "Live-truth reverse reconciliation did not detect a live add into the draft.", result);

            Dictionary<string, int> liveRemove = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            liveRemove["Water"] = 3;
            List<InventoryProjectionDelta> liveRemoveDeltas = InventoryApplyService.PlanProjectionDeltas(authored, liveRemove);
            Assert(FindDelta(liveRemoveDeltas, "Water") == -2, "Live-truth reverse reconciliation did not detect a live removal into the draft.", result);
            Assert(InventoryApplyService.SnapshotsEqual(authored, authored), "Live-truth reconciliation should treat matching draft/live snapshots as no-op to avoid feedback loops.", result);

            StartingInventoryDefinition inventory = new StartingInventoryDefinition();
            inventory.OverrideRandomStart = true;
            Assert(InventoryApplyService.ShouldApplyRandomStartOverride(inventory), "OverrideRandomStart should only suppress vanilla random-start pools during projection/apply.", result);
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
            ScenarioValidationResult validation = new ScenarioValidator(new VerificationDependencyResolver("Required.Mod", "1.3.0")).Validate(invalid, null);
            Assert(ContainsIssue(validation, "cannot replace generated loot without a lootTableId"), "Map validation did not reject replaceGeneratedLoot without a loot table.", result);

            badLocation.LootTableId = "weighted";
            badLocation.VisibleAtStart = true;
            badLocation.HiddenUntilDiscovered = true;
            invalid.Map.LootTables.Add(table);
            validation = new ScenarioValidator(new VerificationDependencyResolver("Required.Mod", "1.3.0")).Validate(invalid, null);
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

        private static void VerifySeamGuardContracts(ScenarioValidationResult result)
        {
            SeamGuard.ResetForTests();
            try
            {
                bool recoveryFired = false;
                string message;
                bool ok = SeamGuard.Run(
                    "verification.throwing-seam",
                    SeamRecoveryPolicy.RestoreState,
                    delegate { throw new InvalidOperationException("verification boom"); },
                    "Verification seam unavailable - scenario still playable.",
                    delegate { recoveryFired = true; },
                    out message);

                SeamHealthSnapshot snapshot = FindSeamSnapshot("verification.throwing-seam");
                Assert(!ok, "SeamGuard should return false when the wrapped call throws.", result);
                Assert(recoveryFired, "SeamGuard restore-state policy did not fire recovery.", result);
                Assert(snapshot != null && snapshot.FailureCount == 1, "SeamGuard did not record the wrapped failure.", result);
                Assert(snapshot != null && snapshot.Degraded, "SeamGuard did not mark the throwing seam degraded.", result);
                Assert(string.Equals(message, "Verification seam unavailable - scenario still playable.", StringComparison.Ordinal),
                    "SeamGuard did not return the editor-facing degradation message.", result);
                Assert(SeamGuard.BuildSystemHealthLine().IndexOf("Verification seam unavailable", StringComparison.Ordinal) >= 0,
                    "SeamGuard did not expose the degradation through the system health line.", result);
            }
            finally
            {
                SeamGuard.ResetForTests();
            }
        }

        private static SeamHealthSnapshot FindSeamSnapshot(string name)
        {
            SeamHealthSnapshot[] snapshots = SeamGuard.GetHealthSnapshots();
            for (int i = 0; snapshots != null && i < snapshots.Length; i++)
            {
                if (snapshots[i] != null && string.Equals(snapshots[i].Name, name, StringComparison.OrdinalIgnoreCase))
                    return snapshots[i];
            }

            return null;
        }

        private static void VerifyDependencies(ScenarioValidationResult result)
        {
            ScenarioModDependency parsed = ScenarioDependencyManifest.ParseDependency("Required.Mod@1.3.0");
            Assert(parsed != null && parsed.modId == "Required.Mod" && parsed.version == "1.3.0",
                "Dependency parser did not split mod id and version.", result);

            ScenarioDefinition definition = CreateDefinition("Scenario.Dependency");
            definition.Dependencies.Add("Required.Mod@1.3.0");

            ScenarioValidationResult matched = new ScenarioValidator(new VerificationDependencyResolver("Required.Mod", "1.3.0")).Validate(definition, null);
            Assert(matched.IsValid, "Matched required dependency was reported invalid.", result);

            ScenarioValidationResult missing = new ScenarioValidator(new VerificationDependencyResolver(null, null)).Validate(definition, null);
            Assert(ContainsIssue(missing, "not loaded"), "Missing required dependency was not reported.", result);

            ScenarioValidationResult mismatched = new ScenarioValidator(new VerificationDependencyResolver("Required.Mod", "2.0.0")).Validate(definition, null);
            Assert(ContainsIssue(mismatched, "version mismatch"), "Version-mismatched dependency was not reported.", result);

            ScenarioDefinition typedDefinition = CreateDefinition("Scenario.TypedDependency");
            typedDefinition.ModDependencies.Add(new ScenarioModDependencyDefinition
            {
                ModId = "Typed.Required.Mod",
                Version = "4.0.0",
                Kind = ScenarioModDependencyKind.Required,
                Manual = true
            });
            typedDefinition.ModDependencies.Add(new ScenarioModDependencyDefinition
            {
                ModId = "Typed.Optional.Mod",
                Version = "1.0.0",
                Kind = ScenarioModDependencyKind.Optional,
                Manual = true
            });

            ScenarioDependencyService dependencyService = new ScenarioDependencyService(new VerificationDefinitionReader(typedDefinition));
            ScenarioModDependency[] dependencies = dependencyService.LoadDefinitionDependencies(typedDefinition.Id);
            Assert(HasDependency(dependencies, "Typed.Required.Mod", "4.0.0"),
                "Typed required ModDependency was not included in the scenario dependency manifest.", result);
            Assert(!HasDependency(dependencies, "Typed.Optional.Mod", "1.0.0"),
                "Typed optional ModDependency should not lock scenario startup.", result);
        }

        private static void VerifyScoringValidation(ScenarioValidationResult result)
        {
            ScenarioDefinition enabledWithoutRules = CreateDefinition("Scenario.ScoringNoRules");
            enabledWithoutRules.Scoring.Rules.Clear();
            ScenarioValidationResult noRules = new ScenarioValidator(new VerificationDependencyResolver("Required.Mod", "1.3.0")).Validate(enabledWithoutRules, null);
            Assert(ContainsIssue(noRules, "no score rules"), "Enabled scoring without rules did not produce a warning.", result);

            ScenarioDefinition invalid = CreateDefinition("Scenario.ScoringInvalid");
            invalid.Scoring.Categories.Add(new ScenarioScoreCategoryDefinition { Id = "survival", DisplayName = "Duplicate" });
            invalid.Scoring.Rules[0].CategoryId = "missing";
            invalid.Scoring.Rules[0].Source = string.Empty;
            ScenarioValidationResult validation = new ScenarioValidator(new VerificationDependencyResolver("Required.Mod", "1.3.0")).Validate(invalid, null);
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

            ScenarioDefinitionSerializer serializer = new ScenarioDefinitionSerializer();
            ScenarioDefinition definition = serializer.Load(pack1File);
            definition.AssetReferences.CustomIcons.Clear();
            definition.AssetReferences.CustomIcons.Add(new IconRef { Id = "escaped", RelativePath = "..\\Pack2\\file.png" });

            ScenarioValidationResult validation = new ScenarioValidator(new VerificationDependencyResolver("Required.Mod", "1.3.0")).Validate(definition, pack1File);
            Assert(ContainsIssue(validation, "escapes the scenario pack folder"), "Sibling-prefix asset escape was not blocked.", result);
        }

        private static void VerifySecureXmlParsing(ScenarioValidationResult result)
        {
            try
            {
                new ScenarioDefinitionSerializer().FromXml("<!DOCTYPE Scenario [<!ENTITY xxe SYSTEM \"file:///C:/Windows/win.ini\">]><Scenario><Meta><Id>&xxe;</Id><DisplayName>Invalid</DisplayName></Meta></Scenario>");
                Assert(false, "Scenario XML parser allowed a DTD/external entity declaration.", result);
            }
            catch (XmlException)
            {
            }
        }

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
                AssertThrowsReserved(delegate { ScenarioSaveIdGuards.RequireCustomScenarioId(reservedId, "ScenarioFrameworkVerification"); },
                    "Reserved scenario save id was accepted by the custom scenario guard: " + reservedId,
                    result);
            }

            string customId = "com.example.scenario.valid";
            Assert(!ScenarioSaveIdGuards.IsReservedStorageId(customId), "Valid custom scenario save id was treated as reserved.", result);
            Assert(ScenarioSaveIdGuards.RequireCustomScenarioId(customId, "ScenarioFrameworkVerification") == customId,
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

        private static void VerifyAtomicScenarioWrites(string root, ScenarioValidationResult result)
        {
            string scenarioFile = Path.Combine(Path.Combine(root, "Atomic"), ScenarioDefinitionSerializer.DefaultFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(scenarioFile));

            ScenarioDefinitionSerializer serializer = new ScenarioDefinitionSerializer();
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

        private static void VerifyDraftDeleteDurability(string root, ScenarioValidationResult result)
        {
            string draftsRoot = Path.Combine(root, "DraftDeleteDurability");
            string fixtureDraft = Path.Combine(draftsRoot, "Slot_7");
            string preservedDraft = Path.Combine(draftsRoot, "Slot_23");
            string historyRoot = Path.Combine(fixtureDraft, ".history");
            Directory.CreateDirectory(historyRoot);
            Directory.CreateDirectory(preservedDraft);
            File.WriteAllText(Path.Combine(fixtureDraft, ScenarioDefinitionSerializer.DefaultFileName), "fixture scenario");
            File.WriteAllText(Path.Combine(fixtureDraft, ScenarioDefinitionSerializer.DefaultFileName + ".bak"), "fixture backup");
            File.WriteAllText(Path.Combine(historyRoot, "autosave.xml"), "fixture autosave");
            File.WriteAllText(Path.Combine(preservedDraft, ScenarioDefinitionSerializer.DefaultFileName), "keepsake scenario");

            bool deleted = ScenarioAuthoringDraftRepository.DeleteDraftDirectory(draftsRoot, fixtureDraft);
            Assert(deleted, "Fixture draft delete did not report durable success.", result);
            Assert(!Directory.Exists(fixtureDraft), "Deleted fixture draft folder remains in the draft root.", result);
            Assert(!File.Exists(Path.Combine(fixtureDraft, ScenarioDefinitionSerializer.DefaultFileName)), "Deleted fixture scenario.xml remains on disk.", result);
            Assert(!File.Exists(Path.Combine(fixtureDraft, ScenarioDefinitionSerializer.DefaultFileName + ".bak")), "Deleted fixture scenario.xml.bak remains on disk.", result);
            Assert(!File.Exists(Path.Combine(historyRoot, "autosave.xml")), "Deleted fixture autosave history remains on disk.", result);
            Assert(Directory.Exists(preservedDraft), "Draft delete touched an unrelated Slot_23 fixture.", result);

            // A restart-safe catalog simulation deliberately performs a fresh disk
            // scan: a deleted draft must have neither a catalog source file nor a
            // quarantined scenario.xml that could be rediscovered by a future scan.
            string[] remainingScenarioFiles = Directory.GetFiles(draftsRoot, ScenarioDefinitionSerializer.DefaultFileName, SearchOption.AllDirectories);
            Assert(remainingScenarioFiles.Length == 1
                && string.Equals(remainingScenarioFiles[0], Path.Combine(preservedDraft, ScenarioDefinitionSerializer.DefaultFileName), StringComparison.OrdinalIgnoreCase),
                "Fresh draft catalog scan rediscovered deleted fixture data.", result);
        }

        private static void VerifyMissingDefinitionRefreshRetry(ScenarioValidationResult result)
        {
            string scenarioId = "Scenario.RetryAfterCatalogRefresh";
            VerificationDefinitionCatalogService catalog = new VerificationDefinitionCatalogService(scenarioId);
            VerificationRuntimeBindingService bindings = new VerificationRuntimeBindingService(new ScenarioRuntimeBinding
            {
                ScenarioId = scenarioId,
                VersionApplied = "1.0.0",
                IsActive = true
            });
            VerificationScenarioApplier applier = new VerificationScenarioApplier();

            ScenarioRuntimeOrchestrator orchestrator = new ScenarioRuntimeOrchestrator(
                new VerificationLifecycleService(),
                new VerificationCustomScenarioRegistry(),
                new VerificationDependencyVerifier(),
                new VerificationDefinitionFactory(),
                catalog,
                bindings,
                new VerificationRuntimeDefinitionOverrideProvider(),
                applier,
                new VerificationSpriteSwapEngine(),
                new VerificationSceneSpritePlacementEngine(),
                new VerificationVanillaScenarioRuntime());

            orchestrator.UpdateActiveScenarioApply();
            Assert(catalog.TryLoadCount == 1, "Missing definition was not checked during the first active binding apply.", result);
            Assert(applier.ApplyCount == 0, "Missing definition unexpectedly applied.", result);

            orchestrator.UpdateActiveScenarioApply();
            Assert(catalog.TryLoadCount == 1, "Blocked missing definition retried before the catalog changed.", result);

            ScenarioDefinition restored = CreateDefinition(scenarioId);
            catalog.RestoreDefinition(restored, Path.Combine(Path.GetTempPath(), "scenario.xml"));
            catalog.RefreshDefinitionCatalog();
            orchestrator.UpdateActiveScenarioApply();

            Assert(applier.ApplyCount == 1, "Catalog refresh did not cause the blocked active binding to retry and apply.", result);
            Assert(object.ReferenceEquals(applier.LastDefinition, restored), "Active binding retry applied the wrong restored scenario definition.", result);
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

            string scenarioFile = Path.Combine(packRoot, ScenarioDefinitionSerializer.DefaultFileName);
            new ScenarioDefinitionSerializer().Save(definition, scenarioFile);
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

            ConditionDef condition = new ConditionDef();
            condition.Id = "survive-7-days";
            condition.Type = "surviveDays";
            condition.Properties.Add(new ScenarioProperty { Key = "days", Value = "7" });
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
            ScenarioDefinitionSerializer serializer = new ScenarioDefinitionSerializer();

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
            ScenarioValidationIssue top = ShelteredAPI.Scenarios.Presentation.Authoring.Shell.ScenarioTopIssueResolver.ResolveTopIssue(mixedSnapshot);
            Assert(top != null && top.Severity == ScenarioIssueSeverity.Error, "Top issue must rank the blocking error first.", result);
            Assert(ShelteredAPI.Scenarios.Presentation.Authoring.Shell.ScenarioTopIssueResolver.BuildNextAction(top) != null, "Top issue must resolve to a fix action.", result);

            ScenarioValidationResult warningsOnly = new ScenarioValidationResult();
            warningsOnly.AddWarning("Earliest warning.");
            warningsOnly.AddWarning("Later warning.");
            ScenarioAuthoringValidationSnapshot warnSnapshot = ScenarioAuthoringValidationSnapshot.Evaluate(new StubValidator(warningsOnly), fixture, null);
            ScenarioValidationIssue topWarning = ShelteredAPI.Scenarios.Presentation.Authoring.Shell.ScenarioTopIssueResolver.ResolveTopIssue(warnSnapshot);
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

        private sealed class VerificationDefinitionCatalogService : IScenarioDefinitionCatalogService
        {
            private readonly string _scenarioId;
            private ScenarioDefinition _definition;
            private string _scenarioFilePath;

            public VerificationDefinitionCatalogService(string scenarioId)
            {
                _scenarioId = scenarioId;
            }

            public int CatalogRevision { get; private set; }
            public int TryLoadCount { get; private set; }

            public void RestoreDefinition(ScenarioDefinition definition, string scenarioFilePath)
            {
                _definition = definition;
                _scenarioFilePath = scenarioFilePath;
            }

            public void RefreshDefinitionCatalog()
            {
                CatalogRevision++;
            }

            public ScenarioInfo[] ListDefinitions()
            {
                return new ScenarioInfo[0];
            }

            public ScenarioValidationResult ValidateDefinition(string scenarioId)
            {
                ScenarioDefinition definition;
                string scenarioFilePath;
                ScenarioValidationResult validation;
                TryLoadDefinition(scenarioId, out definition, out scenarioFilePath, out validation);
                return validation;
            }

            public bool TryLoadDefinition(string scenarioId, out ScenarioDefinition definition, out string scenarioFilePath, out ScenarioValidationResult validation)
            {
                TryLoadCount++;
                definition = null;
                scenarioFilePath = null;
                validation = new ScenarioValidationResult();

                if (_definition == null || !string.Equals(scenarioId, _scenarioId, StringComparison.OrdinalIgnoreCase))
                {
                    validation.AddError("Scenario is not indexed: " + scenarioId);
                    return false;
                }

                definition = _definition;
                scenarioFilePath = _scenarioFilePath;
                return true;
            }
        }

        private sealed class VerificationRuntimeBindingService : IScenarioRuntimeBindingService
        {
            private ScenarioRuntimeBinding _binding;
            private int _revision;

            public VerificationRuntimeBindingService(ScenarioRuntimeBinding binding)
            {
                _binding = binding;
            }

            public ScenarioRuntimeBinding CurrentBinding { get { return _binding; } }
            public int CurrentRevision { get { return _revision; } }
            public void EnsureHooked() { }

            public void SetBinding(ScenarioRuntimeBinding binding)
            {
                _binding = binding;
                _revision++;
            }

            public void ConvertToNormalSave() { }
            public ScenarioRuntimeBinding GetActiveBindingForStartup() { return _binding; }
        }

        private sealed class VerificationScenarioApplier : IScenarioApplier
        {
            public int ApplyCount { get; private set; }
            public ScenarioDefinition LastDefinition { get; private set; }

            public ScenarioApplyResult ApplyAll(ScenarioDefinition definition)
            {
                return ApplyAll(definition, null);
            }

            public ScenarioApplyResult ApplyAll(ScenarioDefinition definition, string scenarioFilePath)
            {
                ApplyCount++;
                LastDefinition = definition;
                return new ScenarioApplyResult();
            }
        }

        private sealed class VerificationRuntimeDefinitionOverrideProvider : IScenarioRuntimeDefinitionOverrideProvider
        {
            public bool TryGetDefinitionOverride(string scenarioId, out ScenarioDefinition definition, out string scenarioFilePath)
            {
                definition = null;
                scenarioFilePath = null;
                return false;
            }
        }

        private sealed class VerificationVanillaScenarioRuntime : IVanillaScenarioRuntime
        {
            public bool IsWorldReady(out string blockingReason)
            {
                blockingReason = null;
                return true;
            }

            public bool TrySpawnScenario(ScenarioDef definition, out QuestInstance instance, out string reason)
            {
                instance = null;
                reason = "Not used by verification.";
                return false;
            }

            public bool TryStartQuest(string questId, out string reason)
            {
                reason = "Not used by verification.";
                return false;
            }

            public bool TryGetQuestInstance(int instanceId, out QuestInstance instance, out string reason)
            {
                instance = null;
                reason = "Not used by verification.";
                return false;
            }

            public List<QuestInstance> GetCurrentQuests()
            {
                return new List<QuestInstance>();
            }

            public bool TryFinishQuest(QuestInstance instance, bool success, out string reason)
            {
                reason = "Not used by verification.";
                return false;
            }
        }

        private sealed class VerificationLifecycleService : ICustomScenarioLifecycleService
        {
            public CustomScenarioState CurrentState { get { return null; } }
            public bool MarkSelected(string scenarioId) { return false; }
            public bool MarkSpawned(string scenarioId) { return false; }
            public void ClearState() { }
        }

        private sealed class VerificationCustomScenarioRegistry : ICustomScenarioRegistry
        {
            public bool TryGet(string scenarioId, out CustomScenarioInfo scenario)
            {
                scenario = null;
                return false;
            }

            public CustomScenarioInfo[] List()
            {
                return new CustomScenarioInfo[0];
            }
        }

        private sealed class VerificationDependencyVerifier : IScenarioDependencyVerifier
        {
            public SlotManifest CreateDependencyManifest(CustomScenarioInfo info)
            {
                return new SlotManifest();
            }

            public ScenarioDependencyVerificationState VerifyDependencies(CustomScenarioInfo info)
            {
                return ScenarioDependencyVerificationState.Match;
            }
        }

        private sealed class VerificationDefinitionFactory : IScenarioDefinitionFactory
        {
            public bool TryCreateDefinition(string scenarioId, CustomScenarioBuildContext context, out object definition, out string errorMessage)
            {
                definition = null;
                errorMessage = "Not used by verification.";
                return false;
            }

            public bool TryCreateScenarioDef(string scenarioId, CustomScenarioBuildContext context, out ScenarioDef definition, out string errorMessage)
            {
                definition = null;
                errorMessage = "Not used by verification.";
                return false;
            }

            public ScenarioDef BuildScenarioDefFromDefinition(string scenarioId)
            {
                return null;
            }
        }

        private sealed class VerificationSpriteSwapEngine : IScenarioSpriteSwapEngine
        {
            public void Activate(ScenarioDefinition definition, string scenarioFilePath, ScenarioApplyResult result) { }
            public void Update() { }
            public void Clear(string reason) { }
        }

        private sealed class VerificationSceneSpritePlacementEngine : IScenarioSceneSpritePlacementEngine
        {
            public int Activate(ScenarioDefinition definition, string scenarioFilePath, ScenarioApplyResult result)
            {
                return 0;
            }

            public void Clear(string reason) { }
        }

        private sealed class VerificationDependencyResolver : IScenarioDependencyVersionResolver
        {
            private readonly string _loadedModId;
            private readonly string _loadedVersion;

            public VerificationDependencyResolver(string loadedModId, string loadedVersion)
            {
                _loadedModId = loadedModId;
                _loadedVersion = loadedVersion;
            }

            public bool IsLoaded(string modId)
            {
                return !string.IsNullOrEmpty(modId)
                    && !string.IsNullOrEmpty(_loadedModId)
                    && string.Equals(modId, _loadedModId, StringComparison.OrdinalIgnoreCase);
            }

            public string GetLoadedVersion(string modId)
            {
                return IsLoaded(modId) ? _loadedVersion : null;
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

        private sealed class VerificationDefinitionReader : IScenarioDefinitionReader
        {
            private readonly ScenarioDefinition _definition;

            public VerificationDefinitionReader(ScenarioDefinition definition)
            {
                _definition = definition;
            }

            public ScenarioInfo[] ListAll()
            {
                return new ScenarioInfo[0];
            }

            public bool TryGetInfo(string scenarioId, out ScenarioInfo info)
            {
                info = null;
                return false;
            }

            public ScenarioValidationResult Validate(string scenarioId)
            {
                return new ScenarioValidationResult();
            }

            public bool TryLoad(string scenarioId, out ScenarioDefinition definition, out string scenarioFilePath, out ScenarioValidationResult validation)
            {
                definition = _definition;
                scenarioFilePath = null;
                validation = new ScenarioValidationResult();
                return definition != null;
            }

            public bool TryLoadUnchecked(string scenarioId, out ScenarioDefinition definition, out string scenarioFilePath, out string errorMessage)
            {
                definition = _definition;
                scenarioFilePath = null;
                errorMessage = definition == null ? "No verification definition." : null;
                return definition != null;
            }
        }
    }
}
