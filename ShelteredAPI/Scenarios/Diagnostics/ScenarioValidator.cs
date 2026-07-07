using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ModAPI.Core;

using ModAPI.Scenarios;

using ShelteredAPI.Content;
using ShelteredAPI.Hooks;
using ShelteredAPI.Persistence;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Domain.Assets;
using ShelteredAPI.Scenarios.Domain.Compatibility;
using ShelteredAPI.Scenarios.Domain.Conditions;
using ShelteredAPI.Scenarios.Domain.Scheduling;
using ShelteredAPI.Scenarios.Domain.Validation;
using ShelteredAPI.Scenarios.Infrastructure.Runtime;
using ShelteredAPI.Scenarios.Shared;
namespace ShelteredAPI.Scenarios.Diagnostics{
    internal interface IScenarioDependencyResolver
    {
        bool IsLoaded(string modId);
    }

    internal interface IScenarioDependencyVersionResolver : IScenarioDependencyResolver
    {
        string GetLoadedVersion(string modId);
    }

    internal sealed class ModRegistryScenarioDependencyResolver : IScenarioDependencyVersionResolver
    {
        public bool IsLoaded(string modId)
        {
            return !string.IsNullOrEmpty(modId) && ModRegistry.GetMod(modId) != null;
        }

        public string GetLoadedVersion(string modId)
        {
            ModEntry loaded = !string.IsNullOrEmpty(modId) ? ModRegistry.GetMod(modId) : null;
            return loaded != null ? loaded.Version : null;
        }
    }

    internal sealed class ScenarioValidator
    {
        private const double DerivativePatchWarningThreshold = 0.80d;
        private readonly IScenarioDependencyResolver _dependencyResolver;
        private readonly ScenarioValidationPipeline _pipeline;

        public ScenarioValidator()
            : this(new ModRegistryScenarioDependencyResolver())
        {
        }

        public ScenarioValidator(IScenarioDependencyResolver dependencyResolver)
        {
            _dependencyResolver = dependencyResolver;
            _pipeline = new ScenarioValidationPipeline(new IScenarioValidationRule[]
            {
                new CoreScenarioRule(),
                new ScenarioCharacterValidationRule(),
                new ScenarioStoryFlowValidationRule(),
                new DependencyValidationRule(this),
                new AssetValidationRule(),
                new FamilyValidationRule(),
                new InventoryValidationRule(),
                new BunkerValidationRule(),
                new QuestMapValidationRule(),
                new MapValidationRule(),
                new SchedulingValidationRule(),
                new WinLossValidationRule(),
                new ScoringValidationRule(),
                new ObjectStartStateValidationRule(),
                new BunkerDependencyValidationRule(),
                new GateConditionValidationRule()
            });
        }

        public ScenarioValidationResult Validate(ScenarioDefinition definition, string scenarioFilePath)
        {
            return _pipeline.ValidateLegacy(definition, scenarioFilePath);
        }

        private void ValidateDependencies(ScenarioDefinition definition, ScenarioValidationResult result)
        {
            if (definition.Dependencies == null || _dependencyResolver == null)
            {
                ValidateExplicitModDependencies(definition, result);
                return;
            }

            for (int i = 0; i < definition.Dependencies.Count; i++)
            {
                ScenarioModDependency dependency = ScenarioDependencyManifest.ParseDependency(definition.Dependencies[i]);
                if (dependency == null)
                    continue;

                if (!_dependencyResolver.IsLoaded(dependency.modId))
                {
                    result.AddError("Required dependency mod is not loaded: " + dependency.modId);
                    continue;
                }

                if (!string.IsNullOrEmpty(dependency.version))
                {
                    string activeVersion = GetLoadedVersion(dependency.modId);
                    if (!string.Equals(activeVersion ?? string.Empty, dependency.version, StringComparison.OrdinalIgnoreCase))
                        result.AddError("Required dependency mod version mismatch: " + dependency.modId
                            + " requires " + dependency.version + " but active version is " + (activeVersion ?? "<unknown>") + ".");
                }
            }

            ValidateExplicitModDependencies(definition, result);
            ValidateAutoDetectedModReferences(definition, result);
        }

        private void ValidateExplicitModDependencies(ScenarioDefinition definition, ScenarioValidationResult result)
        {
            if (definition == null || definition.ModDependencies == null || _dependencyResolver == null)
                return;

            for (int i = 0; i < definition.ModDependencies.Count; i++)
            {
                ScenarioModDependencyDefinition dependency = definition.ModDependencies[i];
                if (dependency == null || string.IsNullOrEmpty(dependency.ModId))
                {
                    result.AddError("Manual scenario mod dependency #" + i + " is missing mod id.");
                    continue;
                }

                if (dependency.Kind == ScenarioModDependencyKind.Required && !_dependencyResolver.IsLoaded(dependency.ModId))
                {
                    result.AddError("Required scenario mod dependency is not loaded: " + dependency.ModId);
                    continue;
                }

                if (dependency.Kind == ScenarioModDependencyKind.Required && !string.IsNullOrEmpty(dependency.Version))
                {
                    string activeVersion = GetLoadedVersion(dependency.ModId);
                    if (!string.Equals(activeVersion ?? string.Empty, dependency.Version, StringComparison.OrdinalIgnoreCase))
                        result.AddError("Required scenario mod dependency version mismatch: " + dependency.ModId
                            + " requires " + dependency.Version + " but active version is " + (activeVersion ?? "<unknown>") + ".");
                }
            }
        }

        private void ValidateAutoDetectedModReferences(ScenarioDefinition definition, ScenarioValidationResult result)
        {
            if (definition == null || _dependencyResolver == null)
                return;

            ValidateContentOwner(definition, result, definition.StartingInventory != null ? definition.StartingInventory.Items : null);
            for (int i = 0; definition.StartingInventory != null && definition.StartingInventory.ScheduledChanges != null && i < definition.StartingInventory.ScheduledChanges.Count; i++)
                ValidateModOwnedContent(result, definition.StartingInventory.ScheduledChanges[i] != null ? definition.StartingInventory.ScheduledChanges[i].ItemId : null, "scheduled inventory");
            for (int i = 0; definition.BunkerEdits != null && definition.BunkerEdits.ObjectPlacements != null && i < definition.BunkerEdits.ObjectPlacements.Count; i++)
            {
                ObjectPlacement placement = definition.BunkerEdits.ObjectPlacements[i];
                ValidateModOwnedContent(result, placement != null ? placement.PrefabReference : null, "bunker object prefab");
                ValidateModOwnedContent(result, placement != null ? placement.DefinitionReference : null, "bunker object definition");
            }
            for (int i = 0; definition.AssetReferences != null && definition.AssetReferences.CustomSprites != null && i < definition.AssetReferences.CustomSprites.Count; i++)
                ValidateModOwnedContent(result, definition.AssetReferences.CustomSprites[i] != null ? definition.AssetReferences.CustomSprites[i].Id : null, "sprite asset");
            for (int i = 0; definition.ScheduledActions != null && i < definition.ScheduledActions.Count; i++)
            {
                ScenarioScheduledActionDefinition action = definition.ScheduledActions[i];
                for (int e = 0; action != null && action.Effects != null && e < action.Effects.Count; e++)
                    ValidateModOwnedContent(result, action.Effects[e] != null ? action.Effects[e].ItemId : null, "scheduled effect");
            }
        }

        private void ValidateContentOwner(ScenarioDefinition definition, ScenarioValidationResult result, System.Collections.Generic.List<ItemEntry> items)
        {
            for (int i = 0; items != null && i < items.Count; i++)
                ValidateModOwnedContent(result, items[i] != null ? items[i].ItemId : null, "starting inventory");
        }

        private void ValidateModOwnedContent(ScenarioValidationResult result, string contentId, string scope)
        {
            string modId = ExtractOwnerPrefix(contentId);
            if (modId == null)
                return;
            if (!_dependencyResolver.IsLoaded(modId))
                result.AddError("Referenced mod content is unavailable: " + scope + " '" + contentId + "' requires missing mod '" + modId + "'.");
        }

        private static string ExtractOwnerPrefix(string contentId)
        {
            if (string.IsNullOrEmpty(contentId))
                return null;
            int separator = contentId.IndexOf(':');
            return separator > 0 ? contentId.Substring(0, separator) : null;
        }

        private string GetLoadedVersion(string modId)
        {
            IScenarioDependencyVersionResolver versionResolver = _dependencyResolver as IScenarioDependencyVersionResolver;
            if (versionResolver != null)
                return versionResolver.GetLoadedVersion(modId);

            ModEntry loaded = !string.IsNullOrEmpty(modId) ? ModRegistry.GetMod(modId) : null;
            return loaded != null ? loaded.Version : null;
        }

        private static void ValidateAssets(ScenarioDefinition definition, string scenarioFilePath, ScenarioValidationResult result)
        {
            string packRoot = !string.IsNullOrEmpty(scenarioFilePath) ? Path.GetDirectoryName(scenarioFilePath) : null;
            if (string.IsNullOrEmpty(packRoot) || definition.AssetReferences == null)
                return;

            for (int i = 0; i < definition.AssetReferences.CustomSprites.Count; i++)
            {
                SpriteRef sprite = definition.AssetReferences.CustomSprites[i];
                if (sprite == null)
                    continue;

                if (TrimToNull(sprite.RelativePath) != null)
                    ValidateAssetPath(packRoot, sprite.RelativePath, "sprite", result);

                if (TrimToNull(sprite.PatchId) != null && !HasSpritePatch(definition.AssetReferences, sprite.PatchId))
                    result.AddError("Custom sprite '" + (sprite.Id ?? ("#" + i.ToString(CultureInfo.InvariantCulture)))
                        + "' references unknown patchId '" + sprite.PatchId + "'.");
            }

            for (int i = 0; i < definition.AssetReferences.CustomIcons.Count; i++)
            {
                IconRef icon = definition.AssetReferences.CustomIcons[i];
                ValidateAssetPath(packRoot, icon != null ? icon.RelativePath : null, "icon", result);
            }

            ValidateSpritePatches(definition.AssetReferences, packRoot, result);

            ValidateSpriteSwaps(definition.AssetReferences, packRoot, result);
            ValidateSceneSpritePlacements(definition.AssetReferences, packRoot, result);
        }

        private static void ValidateInventory(ScenarioDefinition definition, ScenarioValidationResult result)
        {
            if (definition.StartingInventory == null || definition.StartingInventory.Items == null)
                return;

            for (int i = 0; i < definition.StartingInventory.Items.Count; i++)
            {
                ItemEntry item = definition.StartingInventory.Items[i];
                if (item == null || TrimToNull(item.ItemId) == null)
                    result.AddError("Starting inventory item #" + i + " is missing itemId.");
                else if (item.Quantity <= 0)
                    result.AddError("Starting inventory item '" + item.ItemId + "' must have quantity greater than zero.");
                else
                {
                    ItemManager.ItemType itemType;
                    if (!ContentInjector.ResolveItemType(item.ItemId, out itemType))
                        result.AddError("Starting inventory item '" + item.ItemId + "' is not a known item id.");
                }
            }
        }

        private static void ValidateFamily(ScenarioDefinition definition, string scenarioFilePath, ScenarioValidationResult result)
        {
            string playStartReason;
            if (!new ScenarioPlayStartReadiness().CanStartPlay(definition, out playStartReason))
                result.AddWarning(ScenarioPlayStartReadiness.EmptyCastWarning);

            if (definition == null || definition.FamilySetup == null || definition.FamilySetup.Members == null)
                return;

            string packRoot = !string.IsNullOrEmpty(scenarioFilePath) ? Path.GetDirectoryName(scenarioFilePath) : null;
            for (int i = 0; i < definition.FamilySetup.Members.Count; i++)
            {
                FamilyMemberConfig member = definition.FamilySetup.Members[i];
                ValidateFamilyMemberConfig(member, "family survivor #" + i.ToString(CultureInfo.InvariantCulture), result);
                FamilyMemberAppearanceConfig appearance = member != null ? member.Appearance : null;
                if (appearance == null || string.IsNullOrEmpty(packRoot))
                    continue;

                if (TrimToNull(appearance.HeadTexturePath) != null)
                    ValidateAssetPath(packRoot, appearance.HeadTexturePath, "family head texture", result);
                if (TrimToNull(appearance.TorsoTexturePath) != null)
                    ValidateAssetPath(packRoot, appearance.TorsoTexturePath, "family torso texture", result);
                if (TrimToNull(appearance.LegTexturePath) != null)
                    ValidateAssetPath(packRoot, appearance.LegTexturePath, "family leg texture", result);
            }

            for (int i = 0; definition.FamilySetup.FutureSurvivors != null && i < definition.FamilySetup.FutureSurvivors.Count; i++)
            {
                FutureSurvivorDefinition future = definition.FamilySetup.FutureSurvivors[i];
                ValidateFamilyMemberConfig(
                    future != null ? future.Survivor : null,
                    "future survivor #" + i.ToString(CultureInfo.InvariantCulture),
                    result);
            }
        }

        private static void ValidateFamilyMemberConfig(FamilyMemberConfig member, string fallbackLabel, ScenarioValidationResult result)
        {
            if (member == null || result == null)
                return;

            string survivorName = TrimToNull(member.Name) ?? fallbackLabel ?? "survivor";
            for (int i = 0; member.Stats != null && i < member.Stats.Count; i++)
            {
                StatOverride stat = member.Stats[i];
                if (stat == null)
                    continue;

                if (stat.Value < 0 || stat.Value > 20)
                {
                    result.AddWarning("Family survivor '" + survivorName + "' has stat '" + (stat.StatId ?? string.Empty)
                        + "' outside the supported 0-20 range: " + stat.Value.ToString(CultureInfo.InvariantCulture)
                        + ". It will be clamped to 0-20 at runtime.");
                }
            }

            Traits.Strength strength;
            Traits.Weakness weakness;
            if (ScenarioFamilyMemberFactory.HasConflictingTraitPair(member, out strength, out weakness))
            {
                result.AddWarning("Family survivor '" + survivorName + "' has conflicting trait pair: Strength:"
                    + strength + " and Weakness:" + weakness
                    + ". The conflicting weakness will be removed at runtime.");
            }
        }

        private static void ValidateScenarioCharacters(ScenarioDefinition definition, ScenarioValidationResult result)
        {
            if (definition == null || definition.ScenarioCharacters == null || result == null)
                return;

            HashSet<string> characterIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < definition.ScenarioCharacters.Count; i++)
            {
                ScenarioNpcDefinition character = definition.ScenarioCharacters[i];
                string label = "Scenario character #" + i.ToString(CultureInfo.InvariantCulture);
                if (character == null)
                {
                    result.AddError(label + " is null.");
                    continue;
                }

                string id = TrimToNull(character.CharacterId);
                if (id == null)
                {
                    result.AddError(label + " is missing characterId.");
                }
                else if (characterIds.Contains(id))
                {
                    result.AddError("Duplicate scenario character id: " + id);
                }
                else
                {
                    characterIds.Add(id);
                    label = "Scenario character '" + id + "'";
                }

                ValidateNpcItem(character.WeaponItemId, label, "weapon", result, false);
                ValidateNpcItem(character.EquippedItem1Id, label, "equipped item 1", result, true);
                ValidateNpcItem(character.EquippedItem2Id, label, "equipped item 2", result, true);
                ValidateItemEntries(character.CarriedItems, label + " carried item", result);
                ValidateNpcStats(character.Stats, label, result);
                ValidateNpcEnums(character, label, result);

                if (character.NumRandomItems < 0)
                    result.AddError(label + " numRandomItems cannot be negative.");
            }
        }

        private static void ValidateNpcItem(string itemId, string label, string field, ScenarioValidationResult result, bool allowUndefined)
        {
            string id = TrimToNull(itemId);
            if (id == null || (allowUndefined && string.Equals(id, "Undefined", StringComparison.OrdinalIgnoreCase)))
                return;

            ItemManager.ItemType itemType;
            if (!ContentInjector.ResolveItemType(id, out itemType))
                result.AddError(label + " references unknown " + field + " item id '" + id + "'.");
        }

        private static void ValidateItemEntries(List<ItemEntry> items, string label, ScenarioValidationResult result)
        {
            for (int i = 0; items != null && i < items.Count; i++)
            {
                ItemEntry item = items[i];
                string itemId = TrimToNull(item != null ? item.ItemId : null);
                if (itemId == null)
                {
                    result.AddError(label + " #" + i.ToString(CultureInfo.InvariantCulture) + " is missing itemId.");
                    continue;
                }

                if (item.Quantity <= 0)
                    result.AddError(label + " '" + itemId + "' must have quantity greater than zero.");
                ValidateNpcItem(itemId, label, "carried", result, false);
            }
        }

        private static void ValidateNpcStats(ScenarioNpcStatsDefinition stats, string label, ScenarioValidationResult result)
        {
            if (stats == null)
                return;

            ValidateNpcStat(stats.Strength, label, "Strength", result);
            ValidateNpcStat(stats.Dexterity, label, "Dexterity", result);
            ValidateNpcStat(stats.Charisma, label, "Charisma", result);
            ValidateNpcStat(stats.Perception, label, "Perception", result);
            ValidateNpcStat(stats.Intelligence, label, "Intelligence", result);
        }

        private static void ValidateNpcStat(int value, string label, string statName, ScenarioValidationResult result)
        {
            if (value < 0 || value > 20)
            {
                result.AddWarning(label + " has " + statName + " outside the supported 0-20 range: "
                    + value.ToString(CultureInfo.InvariantCulture) + ". It will be clamped or ignored by runtime stat setup.");
            }
        }

        private static void ValidateNpcEnums(ScenarioNpcDefinition character, string label, ScenarioValidationResult result)
        {
            string statSetting = TrimToNull(character.StatSetting);
            if (statSetting == null)
                return;

            try
            {
                object parsed = Enum.Parse(typeof(QuestDefBase.QuestCharacter.StatSetting), statSetting, true);
                if (parsed == null || !Enum.IsDefined(typeof(QuestDefBase.QuestCharacter.StatSetting), parsed))
                    result.AddError(label + " has invalid statSetting '" + statSetting + "'.");
            }
            catch
            {
                result.AddError(label + " has invalid statSetting '" + statSetting + "'.");
            }
        }

        private static void ValidateBunker(ScenarioDefinition definition, ScenarioValidationResult result)
        {
            if (definition == null || definition.BunkerEdits == null)
                return;

            if (definition.BunkerEdits.RoomChanges != null)
            {
                for (int i = 0; i < definition.BunkerEdits.RoomChanges.Count; i++)
                {
                    RoomEdit room = definition.BunkerEdits.RoomChanges[i];
                    if (room == null)
                        continue;
                    if (room.GridX < 0 || room.GridY < 0)
                        result.AddError("Room edit #" + i + " has negative grid coordinates.");
                    if (room.WallSpriteIndex.HasValue && room.WallSpriteIndex.Value < 0)
                        result.AddError("Room edit #" + i + " has negative wallSpriteIndex.");
                    if (room.WireSpriteIndex.HasValue && room.WireSpriteIndex.Value < 0)
                        result.AddError("Room edit #" + i + " has negative wireSpriteIndex.");
                }
            }

            if (definition.BunkerEdits.ObjectPlacements == null)
                return;

            for (int i = 0; i < definition.BunkerEdits.ObjectPlacements.Count; i++)
            {
                ObjectPlacement placement = definition.BunkerEdits.ObjectPlacements[i];
                if (placement == null)
                {
                    result.AddError("Object placement #" + i + " is null.");
                    continue;
                }

                if (TrimToNull(placement.PrefabReference) == null && TrimToNull(placement.DefinitionReference) == null)
                    result.AddError("Object placement #" + i + " must define prefab or definition.");

                AddUnsupportedObjectPlacementWarnings(placement, i, result);

                ScenarioPlacementDefinitionKind kind;
                if (ScenarioPlacementDefinitions.TryParseSpecialKind(placement.DefinitionReference, out kind))
                    ValidateSpecialPlacement(placement, i, kind, result);
            }
        }

        private static void AddUnsupportedObjectPlacementWarnings(ObjectPlacement placement, int index, ScenarioValidationResult result)
        {
            if (placement == null || result == null)
                return;

            if (TrimToNull(placement.PrefabReference) != null)
                result.AddWarning("Object placement #" + index + " PrefabReference is not applied at runtime yet.");
            if (TrimToNull(placement.RequiredFoundationId) != null)
                result.AddWarning("Object placement #" + index + " RequiredFoundationId is not applied at runtime yet.");
            if (TrimToNull(placement.RequiredBunkerExpansionId) != null)
                result.AddWarning("Object placement #" + index + " RequiredBunkerExpansionId is not applied at runtime yet.");
            if (TrimToNull(placement.UnlockGateId) != null && TrimToNull(placement.ScheduledActivationId) == null)
                result.AddWarning("Object placement #" + index + " UnlockGateId is not applied at runtime yet.");
        }

        private static void ValidateQuests(ScenarioDefinition definition, ScenarioValidationResult result)
        {
            if (definition == null)
                return;

            Dictionary<string, bool> questIds = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            if (definition.Quests != null && definition.Quests.Quests != null)
            {
                for (int i = 0; i < definition.Quests.Quests.Count; i++)
                {
                    QuestDefinition quest = definition.Quests.Quests[i];
                    string id = TrimToNull(quest != null ? quest.Id : null);
                    if (id == null)
                    {
                        result.AddError("Quest #" + i.ToString(CultureInfo.InvariantCulture) + " is missing id.");
                        continue;
                    }

                    if (questIds.ContainsKey(id))
                        result.AddError("Duplicate quest id: " + id);
                    else
                        questIds[id] = true;

                    string startTriggerId = TrimToNull(quest.StartTriggerId);
                    if (startTriggerId != null && !ScenarioDefinitionLookup.HasTrigger(definition.TriggersAndEvents, startTriggerId))
                        result.AddError("Quest '" + id + "' references unknown startTriggerId '" + startTriggerId + "'.");

                    string completionConditionId = TrimToNull(quest.CompletionConditionId);
                    if (completionConditionId != null && !ScenarioDefinitionLookup.HasCondition(definition.WinLossConditions, completionConditionId))
                        result.AddError("Quest '" + id + "' references unknown completionConditionId '" + completionConditionId + "'.");
                }
            }
        }

        private static void ValidateSpecialPlacement(
            ObjectPlacement placement,
            int index,
            ScenarioPlacementDefinitionKind kind,
            ScenarioValidationResult result)
        {
            int gridX;
            int gridY;
            bool hasGrid = TryGetGridCoordinates(placement, out gridX, out gridY);
            if (hasGrid && (gridX < 0 || gridY < 0))
                result.AddError("Object placement #" + index + " has negative grid coordinates.");

            switch (kind)
            {
                case ScenarioPlacementDefinitionKind.Room:
                    if (!hasGrid && placement.Position == null)
                        result.AddError("Room placement #" + index + " must include grid coordinates or a position.");
                    break;

                case ScenarioPlacementDefinitionKind.Ladder:
                    if (!hasGrid && placement.Position == null)
                        result.AddError("Ladder placement #" + index + " must include grid coordinates or a position.");

                    float horizontalPos;
                    if (ScenarioPropertyBag.TryGetFloat(placement.CustomProperties, ScenarioPlacementDefinitions.PropertyHorizontalPos, out horizontalPos)
                        && (horizontalPos < 0f || horizontalPos > 1f))
                    {
                        result.AddError("Ladder placement #" + index + " has horizontalPos outside the 0..1 range.");
                    }
                    break;

                case ScenarioPlacementDefinitionKind.RoomLight:
                    if (!hasGrid && placement.Position == null)
                        result.AddError("Room light placement #" + index + " must include grid coordinates or a position.");
                    break;
            }
        }

        private static void ValidateSpriteSwaps(AssetReferencesDefinition assets, string packRoot, ScenarioValidationResult result)
        {
            if (assets == null || assets.SpriteSwaps == null)
                return;

            for (int i = 0; i < assets.SpriteSwaps.Count; i++)
            {
                SpriteSwapRule swap = assets.SpriteSwaps[i];
                if (swap == null)
                {
                    result.AddError("Sprite swap #" + i + " is null.");
                    continue;
                }

                if (TrimToNull(swap.TargetPath) == null)
                    result.AddError("Sprite swap #" + i + " is missing targetPath.");

                bool hasSpriteId = TrimToNull(swap.SpriteId) != null;
                bool hasRelativePath = TrimToNull(swap.RelativePath) != null;
                bool hasRuntimeSpriteKey = TrimToNull(swap.RuntimeSpriteKey) != null;
                if (!hasSpriteId && !hasRelativePath && !hasRuntimeSpriteKey)
                    result.AddError("Sprite swap #" + i + " must specify spriteId, path, or runtimeSpriteKey.");

                if (swap.Day.HasValue && swap.Day.Value < 1)
                    result.AddError("Sprite swap #" + i + " has day less than 1.");

                if (!Enum.IsDefined(typeof(ScenarioSpriteTargetComponentKind), swap.TargetComponent))
                    result.AddError("Sprite swap #" + i + " has invalid targetComponent '" + swap.TargetComponent + "'.");

                if (hasSpriteId && !HasSpriteReference(assets, swap.SpriteId))
                    result.AddError("Sprite swap #" + i + " references unknown spriteId '" + swap.SpriteId + "'.");

                if (hasRelativePath)
                    ValidateAssetPath(packRoot, swap.RelativePath, "sprite swap", result);
            }
        }

        private static void ValidateSpritePatches(AssetReferencesDefinition assets, string packRoot, ScenarioValidationResult result)
        {
            if (assets == null || assets.SpritePatches == null)
                return;

            for (int i = 0; i < assets.SpritePatches.Count; i++)
            {
                SpritePatchDefinition patch = assets.SpritePatches[i];
                if (patch == null)
                {
                    result.AddError("Sprite patch #" + i + " is null.");
                    continue;
                }

                if (TrimToNull(patch.Id) == null)
                    result.AddError("Sprite patch #" + i + " is missing id.");

                bool hasBaseSpriteId = TrimToNull(patch.BaseSpriteId) != null;
                bool hasBaseRelativePath = TrimToNull(patch.BaseRelativePath) != null;
                bool hasRuntimeSpriteKey = TrimToNull(patch.BaseRuntimeSpriteKey) != null;
                if (!hasBaseSpriteId && !hasBaseRelativePath && !hasRuntimeSpriteKey)
                    result.AddError("Sprite patch #" + i + " must define a base sprite reference.");

                if (hasBaseRelativePath)
                    ValidateAssetPath(packRoot, patch.BaseRelativePath, "sprite patch base", result);

                for (int operationIndex = 0; operationIndex < patch.Operations.Count; operationIndex++)
                {
                    SpritePatchOperation operation = patch.Operations[operationIndex];
                    if (operation == null)
                    {
                        result.AddError("Sprite patch '" + (patch.Id ?? ("#" + i)) + "' has a null operation.");
                        continue;
                    }

                    if (operation.Kind == SpritePatchOperationKind.Pixels && (operation.Runs == null || operation.Runs.Count == 0))
                        result.AddError("Sprite patch '" + (patch.Id ?? ("#" + i)) + "' has a pixel operation with no runs.");

                    for (int runIndex = 0; operation.Runs != null && runIndex < operation.Runs.Count; runIndex++)
                    {
                        SpritePatchDeltaRun run = operation.Runs[runIndex];
                        if (run == null || !run.IsValid())
                            result.AddError("Sprite patch '" + (patch.Id ?? ("#" + i)) + "' has an invalid delta run #" + runIndex + ".");
                    }
                }

                AddDerivativePatchWarning(patch, result);
            }
        }

        private static void AddDerivativePatchWarning(SpritePatchDefinition patch, ScenarioValidationResult result)
        {
            if (patch == null || result == null || patch.Width <= 0 || patch.Height <= 0)
                return;

            int totalPixels;
            try
            {
                totalPixels = checked(patch.Width * patch.Height);
            }
            catch
            {
                return;
            }

            if (totalPixels <= 0)
                return;

            int changedPixels = CountChangedPatchPixels(patch, totalPixels);
            if (changedPixels <= 0)
                return;

            double ratio = (double)changedPixels / (double)totalPixels;
            if (ratio < DerivativePatchWarningThreshold)
                return;

            int percent = (int)Math.Round(ratio * 100d);
            result.AddWarning("This patch changes " + percent.ToString(CultureInfo.InvariantCulture)
                + "% of the base sprite and may function as a derivative asset. Patch: '"
                + (patch.Id ?? "<unknown>") + "'.");
        }

        private static int CountChangedPatchPixels(SpritePatchDefinition patch, int totalPixels)
        {
            bool[] changed = new bool[totalPixels];
            int count = 0;
            for (int operationIndex = 0; patch.Operations != null && operationIndex < patch.Operations.Count; operationIndex++)
            {
                SpritePatchOperation operation = patch.Operations[operationIndex];
                if (operation == null)
                    continue;

                if (operation.Kind == SpritePatchOperationKind.Clear)
                    return totalPixels;

                if (operation.Kind != SpritePatchOperationKind.Pixels)
                    continue;

                for (int runIndex = 0; operation.Runs != null && runIndex < operation.Runs.Count; runIndex++)
                {
                    SpritePatchDeltaRun run = operation.Runs[runIndex];
                    if (run == null || !run.IsValid() || run.Y < 0 || run.Y >= patch.Height)
                        continue;

                    int start = Math.Max(0, run.X);
                    long rawEnd = (long)run.X + (long)run.Length;
                    int end = rawEnd > patch.Width ? patch.Width : (int)rawEnd;
                    for (int x = start; x < end; x++)
                    {
                        int index = x + (run.Y * patch.Width);
                        if (index < 0 || index >= changed.Length || changed[index])
                            continue;

                        changed[index] = true;
                        count++;
                    }
                }
            }

            return count;
        }

        private static void ValidateSceneSpritePlacements(AssetReferencesDefinition assets, string packRoot, ScenarioValidationResult result)
        {
            if (assets == null || assets.SceneSpritePlacements == null)
                return;

            for (int i = 0; i < assets.SceneSpritePlacements.Count; i++)
            {
                SceneSpritePlacement placement = assets.SceneSpritePlacements[i];
                if (placement == null)
                {
                    result.AddError("Scene sprite placement #" + i + " is null.");
                    continue;
                }

                if (TrimToNull(placement.Id) == null)
                    result.AddError("Scene sprite placement #" + i + " is missing id.");

                bool hasSpriteId = TrimToNull(placement.SpriteId) != null;
                bool hasRelativePath = TrimToNull(placement.RelativePath) != null;
                bool hasRuntimeSpriteKey = TrimToNull(placement.RuntimeSpriteKey) != null;
                if (!hasSpriteId && !hasRelativePath && !hasRuntimeSpriteKey)
                    result.AddError("Scene sprite placement #" + i + " must specify spriteId, path, or runtimeSpriteKey.");

                if (hasSpriteId && !HasSpriteReference(assets, placement.SpriteId))
                    result.AddError("Scene sprite placement #" + i + " references unknown spriteId '" + placement.SpriteId + "'.");

                if (hasRelativePath)
                    ValidateAssetPath(packRoot, placement.RelativePath, "scene sprite placement", result);

                if (placement.SnapToGrid && (!placement.GridX.HasValue || !placement.GridY.HasValue))
                    result.AddError("Scene sprite placement #" + i + " is snapToGrid but missing gridX/gridY.");

                if (placement.GridX.HasValue && placement.GridX.Value < 0)
                    result.AddError("Scene sprite placement #" + i + " has negative gridX.");

                if (placement.GridY.HasValue && placement.GridY.Value < 0)
                    result.AddError("Scene sprite placement #" + i + " has negative gridY.");

                AddUnsupportedSceneSpritePlacementWarnings(placement, i, result);
            }
        }

        private static void AddUnsupportedSceneSpritePlacementWarnings(SceneSpritePlacement placement, int index, ScenarioValidationResult result)
        {
            if (placement == null || result == null)
                return;

            if (TrimToNull(placement.RequiredFoundationId) != null)
                result.AddWarning("Scene sprite placement #" + index + " RequiredFoundationId is not applied at runtime yet.");
            if (TrimToNull(placement.RequiredBunkerExpansionId) != null)
                result.AddWarning("Scene sprite placement #" + index + " RequiredBunkerExpansionId is not applied at runtime yet.");
            if (TrimToNull(placement.UnlockGateId) != null)
                result.AddWarning("Scene sprite placement #" + index + " UnlockGateId is not applied at runtime yet.");
            if (TrimToNull(placement.ScheduledActivationId) != null)
                result.AddWarning("Scene sprite placement #" + index + " ScheduledActivationId is not applied at runtime yet.");
        }

        private static bool HasSpriteReference(AssetReferencesDefinition assets, string spriteId)
        {
            if (assets == null || assets.CustomSprites == null || string.IsNullOrEmpty(spriteId))
                return false;

            for (int i = 0; i < assets.CustomSprites.Count; i++)
            {
                SpriteRef sprite = assets.CustomSprites[i];
                if (sprite != null && string.Equals(sprite.Id, spriteId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool HasSpritePatch(AssetReferencesDefinition assets, string patchId)
        {
            if (assets == null || assets.SpritePatches == null || string.IsNullOrEmpty(patchId))
                return false;

            for (int i = 0; i < assets.SpritePatches.Count; i++)
            {
                SpritePatchDefinition patch = assets.SpritePatches[i];
                if (patch != null && string.Equals(patch.Id, patchId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static void ValidateAssetPath(string packRoot, string relativePath, string assetKind, ScenarioValidationResult result)
        {
            string trimmed = TrimToNull(relativePath);
            if (trimmed == null)
            {
                result.AddError("Scenario " + assetKind + " reference is missing a relative path.");
                return;
            }

            string fullPath = Path.GetFullPath(Path.Combine(packRoot, trimmed));
            string fullRoot = EnsureTrailingDirectorySeparator(Path.GetFullPath(packRoot));
            if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            {
                result.AddError("Scenario " + assetKind + " path escapes the scenario pack folder: " + trimmed);
                return;
            }

            if (!File.Exists(fullPath))
                result.AddError("Scenario " + assetKind + " file does not exist: " + trimmed);
        }

        private static bool TryGetGridCoordinates(ObjectPlacement placement, out int gridX, out int gridY)
        {
            gridX = -1;
            gridY = -1;
            int parsedGridX;
            int parsedGridY;
            if (!ScenarioPropertyBag.TryGetInt(placement != null ? placement.CustomProperties : null, ScenarioPlacementDefinitions.PropertyGridX, out parsedGridX)
                || !ScenarioPropertyBag.TryGetInt(placement != null ? placement.CustomProperties : null, ScenarioPlacementDefinitions.PropertyGridY, out parsedGridY))
            {
                return false;
            }

            gridX = parsedGridX;
            gridY = parsedGridY;
            return true;
        }

        private static string EnsureTrailingDirectorySeparator(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            char last = path[path.Length - 1];
            if (last == Path.DirectorySeparatorChar || last == Path.AltDirectorySeparatorChar)
                return path;

            return path + Path.DirectorySeparatorChar;
        }

        private sealed class CoreScenarioRule : IScenarioValidationRule
        {
            public void Validate(ScenarioDefinition definition, string scenarioFilePath, ValidationSummary summary)
            {
                if (summary == null)
                    return;

                if (definition == null)
                {
                    summary.AddError("core.definition.null", "Scenario definition is null.");
                    return;
                }

                if (TrimToNull(definition.Id) == null)
                    summary.AddError("core.meta.id_required", "Scenario Id is required.");
                if (TrimToNull(definition.DisplayName) == null)
                    summary.AddError("core.meta.display_name_required", "Scenario DisplayName is required.");
                if (!Enum.IsDefined(typeof(ScenarioBaseGameMode), definition.BaseGameMode))
                    summary.AddError("core.meta.invalid_base_mode", "Scenario BaseMode is invalid: " + definition.BaseGameMode);
                ValidateSelectionRules(definition, summary);
            }

            private static void ValidateSelectionRules(ScenarioDefinition definition, ValidationSummary summary)
            {
                ScenarioSelectionRulesDefinition rules = definition != null ? definition.SelectionRules : null;
                if (rules == null)
                    return;

                if (rules.Weight < 0f)
                    summary.AddError("core.selection.invalid_weight", "Scenario selection weight cannot be negative.");
                if (rules.StartDay < 0)
                    summary.AddError("core.selection.invalid_start_day", "Scenario selection start day cannot be negative.");
                if (rules.TimeoutDays < 0)
                    summary.AddError("core.selection.invalid_timeout", "Scenario selection timeout cannot be negative.");
                if (rules.MaxSimultaneousInstances < 0)
                    summary.AddError("core.selection.invalid_max_instances", "Scenario max simultaneous instances cannot be negative.");
                if (rules.Availability == null
                    || (!rules.Availability.Survival && !rules.Availability.Surrounded && !rules.Availability.Stasis))
                {
                    summary.AddError("core.selection.no_modes", "Scenario selection must be available in at least one game mode.");
                }
            }

            private static void ValidateScenarioFlow(ScenarioDefinition definition, ValidationSummary summary)
            {
                if (definition == null || definition.ScenarioFlow == null || definition.ScenarioFlow.Stages == null)
                    return;

                HashSet<string> stageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                HashSet<string> characterIds = BuildScenarioCharacterIds(definition);
                for (int i = 0; i < definition.ScenarioFlow.Stages.Count; i++)
                {
                    ScenarioFlowStageDefinition stage = definition.ScenarioFlow.Stages[i];
                    if (stage == null)
                        continue;

                    if (TrimToNull(stage.Id) == null)
                        summary.AddError("core.flow.stage_id_required", "Scenario flow stage #" + i.ToString() + " requires an id.");
                    else if (!stageIds.Add(stage.Id))
                        summary.AddError("core.flow.duplicate_stage", "Scenario flow stage id is duplicated: " + stage.Id);

                    if (stage.UnansweredNextDays < 0)
                        summary.AddError("core.flow.invalid_unanswered_delay", "Scenario flow stage '" + (stage.Id ?? ("#" + i.ToString())) + "' has a negative unanswered delay.");

                    for (int c = 0; stage.CharacterIds != null && c < stage.CharacterIds.Count; c++)
                    {
                        string characterId = TrimToNull(stage.CharacterIds[c]);
                        if (characterId == null)
                            summary.AddError("core.flow.character_required", "Scenario flow stage '" + (stage.Id ?? ("#" + i.ToString())) + "' has an empty character id.");
                        else if (!IsKnownScenarioCharacter(characterIds, characterId))
                            summary.AddError("core.flow.unknown_character", "Scenario flow stage '" + (stage.Id ?? ("#" + i.ToString())) + "' references unknown character id '" + characterId + "'.");
                    }
                }

                for (int i = 0; i < definition.ScenarioFlow.Stages.Count; i++)
                {
                    ScenarioFlowStageDefinition stage = definition.ScenarioFlow.Stages[i];
                    if (stage == null || TrimToNull(stage.UnansweredNextStage) == null)
                        continue;

                    if (!stageIds.Contains(stage.UnansweredNextStage))
                        summary.AddError("core.flow.missing_unanswered_stage", "Scenario flow stage '" + (stage.Id ?? ("#" + i.ToString())) + "' routes unanswered intercoms to missing stage '" + stage.UnansweredNextStage + "'.");
                }

                for (int i = 0; i < definition.ScenarioFlow.Stages.Count; i++)
                    ValidateIntercomStages(definition.ScenarioFlow.Stages[i], i, stageIds, characterIds, summary);
            }

            private static void ValidateIntercomStages(ScenarioFlowStageDefinition stage, int stageIndex, HashSet<string> stageIds, HashSet<string> characterIds, ValidationSummary summary)
            {
                if (stage == null || stage.IntercomStages == null)
                    return;

                HashSet<string> intercomIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < stage.IntercomStages.Count; i++)
                {
                    ScenarioIntercomStageDefinition intercom = stage.IntercomStages[i];
                    string id = TrimToNull(intercom != null ? intercom.Id : null);
                    if (id == null)
                        summary.AddError("core.flow.intercom_id_required", "Scenario flow stage '" + (stage.Id ?? ("#" + stageIndex.ToString())) + "' intercom #" + i.ToString() + " requires an id.");
                    else if (!intercomIds.Add(id))
                        summary.AddError("core.flow.duplicate_intercom", "Scenario flow stage '" + (stage.Id ?? ("#" + stageIndex.ToString())) + "' has duplicate intercom id '" + id + "'.");
                }

                for (int i = 0; i < stage.IntercomStages.Count; i++)
                {
                    ScenarioIntercomStageDefinition intercom = stage.IntercomStages[i];
                    if (intercom == null)
                        continue;
                    string label = stage.Id + "/" + (intercom.Id ?? ("#" + i.ToString()));
                    ValidateIntercomTarget(summary, intercomIds, intercom.NextId, label, "NextId");
                    ValidateIntercomTarget(summary, intercomIds, intercom.AlternateNextId, label, "AlternateNextId");
                    for (int r = 0; intercom.RandomizedNextIds != null && r < intercom.RandomizedNextIds.Count; r++)
                        ValidateIntercomTarget(summary, intercomIds, intercom.RandomizedNextIds[r], label, "RandomizedNextIds");
                    for (int o = 0; intercom.Options != null && o < intercom.Options.Count; o++)
                    {
                        ScenarioDialogueOptionDefinition option = intercom.Options[o];
                        if (TrimToNull(option != null ? option.TextKey : null) == null)
                            summary.AddError("core.flow.option_key_required", "Scenario flow option #" + o.ToString() + " in '" + label + "' has an empty text key.");
                        ValidateIntercomTarget(summary, intercomIds, option != null ? option.NextId : null, label, "Option NextId");
                    }
                    for (int d = 0; intercom.Dialogue != null && d < intercom.Dialogue.Count; d++)
                    {
                        ScenarioDialogueLineDefinition line = intercom.Dialogue[d];
                        if (TrimToNull(line != null ? line.TextKey : null) == null)
                            summary.AddError("core.flow.dialogue_key_required", "Scenario flow dialogue #" + d.ToString() + " in '" + label + "' has an empty text key.");
                    }
                    if (intercom.StageChange != null && TrimToNull(intercom.StageChange.Id) != null && !stageIds.Contains(intercom.StageChange.Id))
                        summary.AddError("core.flow.missing_stage_change", "Scenario flow intercom '" + label + "' changes to missing stage '" + intercom.StageChange.Id + "'.");
                    for (int c = 0; intercom.CharacterIdsToRecruit != null && c < intercom.CharacterIdsToRecruit.Count; c++)
                    {
                        string characterId = TrimToNull(intercom.CharacterIdsToRecruit[c]);
                        if (characterId != null && !IsKnownScenarioCharacter(characterIds, characterId))
                            summary.AddError("core.flow.unknown_recruit", "Scenario flow intercom '" + label + "' recruits unknown character id '" + characterId + "'.");
                    }
                    ValidateItemEntries(summary, intercom.Items, "reward item", label);
                    ValidateItemEntries(summary, intercom.ItemsToRemove, "removal item", label);
                    if (intercom.EndOptions != null)
                    {
                        ValidateItemEntries(summary, intercom.EndOptions.RewardItems, "end reward item", label);
                        ValidateItemEntries(summary, intercom.EndOptions.TradeItems, "trade item", label);
                    }
                }
            }

            private static void ValidateIntercomTarget(ValidationSummary summary, HashSet<string> intercomIds, string value, string label, string field)
            {
                string id = TrimToNull(value);
                if (id != null && !intercomIds.Contains(id))
                    summary.AddError("core.flow.missing_intercom_target", "Scenario flow intercom '" + label + "' " + field + " references missing intercom '" + id + "'.");
            }

            private static void ValidateItemEntries(ValidationSummary summary, List<ItemEntry> items, string label, string owner)
            {
                for (int i = 0; items != null && i < items.Count; i++)
                {
                    ItemEntry item = items[i];
                    string itemId = TrimToNull(item != null ? item.ItemId : null);
                    if (itemId == null)
                    {
                        summary.AddError("core.flow.item_id_required", "Scenario flow " + label + " #" + i.ToString() + " in '" + owner + "' is missing item id.");
                        continue;
                    }
                    if (item.Quantity <= 0)
                        summary.AddError("core.flow.item_quantity_invalid", "Scenario flow " + label + " '" + itemId + "' in '" + owner + "' must have quantity greater than zero.");
                    ItemManager.ItemType itemType;
                    if (!ContentInjector.ResolveItemType(itemId, out itemType))
                        summary.AddError("core.flow.item_unknown", "Scenario flow " + label + " '" + itemId + "' in '" + owner + "' is not a known item id.");
                }
            }

            private static HashSet<string> BuildScenarioCharacterIds(ScenarioDefinition definition)
            {
                HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; definition != null && definition.ScenarioCharacters != null && i < definition.ScenarioCharacters.Count; i++)
                {
                    string id = TrimToNull(definition.ScenarioCharacters[i] != null ? definition.ScenarioCharacters[i].CharacterId : null);
                    if (id != null)
                        ids.Add(id);
                }
                ids.Add("LeadNpc");
                ids.Add("Npc2");
                ids.Add("Npc3");
                ids.Add("Npc4");
                ids.Add("BackgroundNpc");
                return ids;
            }

            private static bool IsKnownScenarioCharacter(HashSet<string> characterIds, string id)
            {
                return characterIds != null && characterIds.Contains(id);
            }
        }

        private sealed class DependencyValidationRule : IScenarioValidationRule
        {
            private readonly ScenarioValidator _owner;

            public DependencyValidationRule(ScenarioValidator owner)
            {
                _owner = owner;
            }

            public void Validate(ScenarioDefinition definition, string scenarioFilePath, ValidationSummary summary)
            {
                ScenarioValidationResult legacy = new ScenarioValidationResult();
                _owner.ValidateDependencies(definition, legacy);
                CopyIssues(legacy, summary);
            }
        }

        private sealed class WinLossValidationRule : IScenarioValidationRule
        {
            public void Validate(ScenarioDefinition definition, string scenarioFilePath, ValidationSummary summary)
            {
                if (definition == null || summary == null)
                    return;

                WinLossConditionsDefinition winLoss = definition.WinLossConditions;
                int winCount = Count(winLoss != null ? winLoss.WinConditions : null);
                int lossCount = Count(winLoss != null ? winLoss.LossConditions : null);
                if (winCount + lossCount == 0)
                {
                    summary.AddWarning("win_loss.no_end_state", "[Victory] No victory or failure condition is defined; the scenario can run forever.");
                    return;
                }

                ValidateConditions(winLoss != null ? winLoss.WinConditions : null, "victory", summary);
                ValidateConditions(winLoss != null ? winLoss.LossConditions : null, "failure", summary);
            }

            private static void ValidateConditions(List<ConditionDef> conditions, string label, ValidationSummary summary)
            {
                HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; conditions != null && i < conditions.Count; i++)
                {
                    ConditionDef condition = conditions[i];
                    string scope = "[Victory] " + label + " condition #" + i.ToString(CultureInfo.InvariantCulture);
                    if (condition == null)
                    {
                        summary.AddError("win_loss.condition.null", scope + " is null.");
                        continue;
                    }

                    string id = TrimToNull(condition.Id);
                    if (id == null)
                        summary.AddError("win_loss.condition.id_required", scope + " requires an id.");
                    else if (!ids.Add(id))
                        summary.AddError("win_loss.condition.duplicate", "[Victory] " + label + " condition id is duplicated: " + id);

                    ScenarioWinLossConditionDescriptor descriptor;
                    if (!ScenarioWinLossConditionSupport.TryGetDescriptor(condition.Type, out descriptor))
                    {
                        summary.AddError("win_loss.condition.unsupported_type", scope + " uses unsupported condition type '" + (condition.Type ?? string.Empty) + "'.");
                        continue;
                    }

                    ValidateDescriptorFields(condition, descriptor, scope, summary);
                }
            }

            private static void ValidateDescriptorFields(
                ConditionDef condition,
                ScenarioWinLossConditionDescriptor descriptor,
                string scope,
                ValidationSummary summary)
            {
                if (descriptor.FieldKind == ScenarioWinLossConditionFieldKind.Time)
                {
                    int day = ScenarioPropertyBag.GetInt(condition.Properties, "day", ScenarioPropertyBag.GetInt(condition.Properties, "days", 0));
                    int hour = ScenarioPropertyBag.GetInt(condition.Properties, "hour", 0);
                    int minute = ScenarioPropertyBag.GetInt(condition.Properties, "minute", 0);
                    if (day <= 0 || hour < 0 || hour > 23 || minute < 0 || minute > 59)
                        summary.AddError("win_loss.condition.invalid_time", scope + " has invalid day/hour/minute values.");
                    return;
                }

                if (descriptor.FieldKind == ScenarioWinLossConditionFieldKind.Quantity)
                {
                    string itemId = ScenarioPropertyBag.FirstString(condition.Properties, "itemId", "targetId");
                    int quantity = ScenarioPropertyBag.GetInt(condition.Properties, "quantity", 1);
                    if (TrimToNull(itemId) == null)
                        summary.AddError("win_loss.condition.item_required", scope + " requires an itemId property.");
                    if (quantity <= 0)
                        summary.AddError("win_loss.condition.quantity", scope + " quantity must be greater than zero.");
                    return;
                }

                if (descriptor.FieldKind == ScenarioWinLossConditionFieldKind.Flag)
                {
                    string flagId = ScenarioPropertyBag.FirstString(condition.Properties, "flagId", "targetId");
                    if (TrimToNull(flagId) == null)
                        summary.AddError("win_loss.condition.flag_required", scope + " requires a flagId property.");
                    return;
                }

                if (descriptor.FieldKind == ScenarioWinLossConditionFieldKind.Target)
                {
                    string target = ScenarioPropertyBag.FirstString(condition.Properties, "questId", "survivorId", "name", "bunkerExpansionId", "technologyId", "triggerId", "targetId");
                    if (TrimToNull(target) == null)
                        summary.AddError("win_loss.condition.target_required", scope + " requires a target id property.");
                }
            }

            private static int Count(List<ConditionDef> conditions)
            {
                return conditions != null ? conditions.Count : 0;
            }
        }

        private sealed class ScoringValidationRule : IScenarioValidationRule
        {
            public void Validate(ScenarioDefinition definition, string scenarioFilePath, ValidationSummary summary)
            {
                if (definition == null || definition.Scoring == null || summary == null)
                    return;

                ScenarioScoringDefinition scoring = definition.Scoring;
                HashSet<string> categoryIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; scoring.Categories != null && i < scoring.Categories.Count; i++)
                {
                    ScenarioScoreCategoryDefinition category = scoring.Categories[i];
                    if (category == null)
                        continue;

                    string categoryId = TrimToNull(category.Id);
                    if (categoryId == null)
                    {
                        summary.AddError("scoring.category.id_required", "Score category #" + i.ToString(CultureInfo.InvariantCulture) + " requires an id.");
                        continue;
                    }

                    if (!categoryIds.Add(categoryId))
                        summary.AddError("scoring.category.duplicate", "Score category id is duplicated: " + categoryId);
                }

                int ruleCount = 0;
                HashSet<string> ruleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; scoring.Rules != null && i < scoring.Rules.Count; i++)
                {
                    ScenarioScoreRuleDefinition rule = scoring.Rules[i];
                    if (rule == null)
                        continue;

                    ruleCount++;
                    string ruleId = TrimToNull(rule.Id);
                    if (ruleId == null)
                        summary.AddError("scoring.rule.id_required", "Score rule #" + i.ToString(CultureInfo.InvariantCulture) + " requires an id.");
                    else if (!ruleIds.Add(ruleId))
                        summary.AddError("scoring.rule.duplicate", "Score rule id is duplicated: " + ruleId);

                    if (TrimToNull(rule.Source) == null)
                        summary.AddError("scoring.rule.source_required", "Score rule '" + (ruleId ?? ("#" + i.ToString(CultureInfo.InvariantCulture))) + "' requires a source.");
                    if (TrimToNull(rule.Operation) == null)
                        summary.AddError("scoring.rule.operation_required", "Score rule '" + (ruleId ?? ("#" + i.ToString(CultureInfo.InvariantCulture))) + "' requires an operation.");

                    string categoryId = TrimToNull(rule.CategoryId);
                    if (categoryId != null && categoryIds.Count > 0 && !categoryIds.Contains(categoryId))
                        summary.AddError("scoring.rule.unknown_category", "Score rule '" + (ruleId ?? ("#" + i.ToString(CultureInfo.InvariantCulture))) + "' references unknown category '" + categoryId + "'.");
                }

                if (scoring.Enabled && ruleCount == 0)
                    summary.AddWarning("scoring.enabled_without_rules", "Scoring is enabled but no score rules are defined.");
            }
        }

        private sealed class AssetValidationRule : IScenarioValidationRule
        {
            public void Validate(ScenarioDefinition definition, string scenarioFilePath, ValidationSummary summary)
            {
                ScenarioValidationResult legacy = new ScenarioValidationResult();
                ValidateAssets(definition, scenarioFilePath, legacy);
                CopyIssues(legacy, summary);
            }
        }

        private sealed class ScenarioCharacterValidationRule : IScenarioValidationRule
        {
            public void Validate(ScenarioDefinition definition, string scenarioFilePath, ValidationSummary summary)
            {
                ScenarioValidationResult legacy = new ScenarioValidationResult();
                ValidateScenarioCharacters(definition, legacy);
                CopyIssues(legacy, summary);
            }
        }

        private sealed class FamilyValidationRule : IScenarioValidationRule
        {
            public void Validate(ScenarioDefinition definition, string scenarioFilePath, ValidationSummary summary)
            {
                ScenarioValidationResult legacy = new ScenarioValidationResult();
                ValidateFamily(definition, scenarioFilePath, legacy);
                CopyIssues(legacy, summary);
            }
        }

        private sealed class InventoryValidationRule : IScenarioValidationRule
        {
            public void Validate(ScenarioDefinition definition, string scenarioFilePath, ValidationSummary summary)
            {
                ScenarioValidationResult legacy = new ScenarioValidationResult();
                ValidateInventory(definition, legacy);
                CopyIssues(legacy, summary);
            }
        }

        private sealed class BunkerValidationRule : IScenarioValidationRule
        {
            public void Validate(ScenarioDefinition definition, string scenarioFilePath, ValidationSummary summary)
            {
                ScenarioValidationResult legacy = new ScenarioValidationResult();
                ValidateBunker(definition, legacy);
                CopyIssues(legacy, summary);
            }
        }

        private sealed class QuestMapValidationRule : IScenarioValidationRule
        {
            public void Validate(ScenarioDefinition definition, string scenarioFilePath, ValidationSummary summary)
            {
                ScenarioValidationResult legacy = new ScenarioValidationResult();
                ValidateQuests(definition, legacy);
                CopyIssues(legacy, summary);
            }
        }

        private static void CopyIssues(ScenarioValidationResult source, ValidationSummary target)
        {
            if (source == null || target == null)
                return;

            ScenarioValidationIssue[] issues = source.Issues;
            for (int i = 0; i < issues.Length; i++)
            {
                ScenarioValidationIssue issue = issues[i];
                if (issue == null)
                    continue;

                if (issue.Severity == ScenarioIssueSeverity.Error)
                    target.AddError("legacy.error", issue.Message);
                else
                    target.AddWarning("legacy.warning", issue.Message);
            }
        }

        private static string TrimToNull(string value)
        {
            if (string.IsNullOrEmpty(value))
                return null;
            string trimmed = value.Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }
    }
}
