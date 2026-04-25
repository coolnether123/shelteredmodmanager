using System;
using ModAPI.Core;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioModDependencyDetector : IScenarioModContentResolver
    {
        public ScenarioModCompatibilityReport BuildReport(ScenarioDefinition definition)
        {
            ScenarioModReferenceIndex index = new ScenarioModReferenceIndex();
            AddManual(definition, index);
            AddInventory(definition, index);
            AddAssets(definition, index);
            AddBunkerObjects(definition, index);
            AddSurvivorContent(definition, index);
            AddScheduledEffects(definition, index);

            ScenarioModCompatibilityReport report = new ScenarioModCompatibilityReport();
            for (int i = 0; i < index.Dependencies.Count; i++)
            {
                ScenarioModDependencyDefinition dependency = index.Dependencies[i];
                if (dependency == null)
                    continue;
                if (dependency.Kind == ScenarioModDependencyKind.Optional)
                    report.OptionalMods.Add(dependency);
                else if (dependency.Kind == ScenarioModDependencyKind.Required)
                    report.RequiredMods.Add(dependency);

                if (dependency.Kind == ScenarioModDependencyKind.Required && !IsLoaded(dependency.ModId))
                    report.MissingRequiredMods.Add(dependency);
                else if (!string.IsNullOrEmpty(dependency.Version) && !string.Equals(GetLoadedVersion(dependency.ModId) ?? string.Empty, dependency.Version, StringComparison.OrdinalIgnoreCase))
                    report.VersionMismatches.Add(dependency);
            }
            for (int i = 0; i < index.UnknownReferences.Count; i++)
                report.UnknownReferences.Add(index.UnknownReferences[i]);
            return report;
        }

        public bool TryResolveOwner(string contentId, out string modId, out string version)
        {
            modId = null;
            version = null;
            if (string.IsNullOrEmpty(contentId))
                return false;

            int separator = contentId.IndexOf(':');
            if (separator <= 0)
                return false;

            modId = contentId.Substring(0, separator);
            ModEntry mod = ModRegistry.GetMod(modId);
            version = mod != null ? mod.Version : null;
            return true;
        }

        public bool IsLoaded(string modId)
        {
            return !string.IsNullOrEmpty(modId) && ModRegistry.GetMod(modId) != null;
        }

        public string GetLoadedVersion(string modId)
        {
            ModEntry mod = !string.IsNullOrEmpty(modId) ? ModRegistry.GetMod(modId) : null;
            return mod != null ? mod.Version : null;
        }

        private void AddManual(ScenarioDefinition definition, ScenarioModReferenceIndex index)
        {
            for (int i = 0; definition != null && definition.ModDependencies != null && i < definition.ModDependencies.Count; i++)
            {
                ScenarioModDependencyDefinition dependency = definition.ModDependencies[i];
                if (dependency != null)
                    index.Add(dependency.ModId, dependency.Version, dependency.Kind, ScenarioModReferenceReason.ExplicitDependency, "manual", true);
            }
            for (int i = 0; definition != null && definition.Dependencies != null && i < definition.Dependencies.Count; i++)
            {
                string modId;
                string version;
                ParseLegacyDependency(definition.Dependencies[i], out modId, out version);
                index.Add(modId, version, ScenarioModDependencyKind.Required, ScenarioModReferenceReason.ExplicitDependency, definition.Dependencies[i], true);
            }
        }

        private void AddInventory(ScenarioDefinition definition, ScenarioModReferenceIndex index)
        {
            for (int i = 0; definition != null && definition.StartingInventory != null && definition.StartingInventory.Items != null && i < definition.StartingInventory.Items.Count; i++)
                AddContent(index, definition.StartingInventory.Items[i] != null ? definition.StartingInventory.Items[i].ItemId : null, ScenarioModReferenceReason.InventoryItem);
            for (int i = 0; definition != null && definition.StartingInventory != null && definition.StartingInventory.ScheduledChanges != null && i < definition.StartingInventory.ScheduledChanges.Count; i++)
                AddContent(index, definition.StartingInventory.ScheduledChanges[i] != null ? definition.StartingInventory.ScheduledChanges[i].ItemId : null, ScenarioModReferenceReason.InventoryItem);
        }

        private void AddAssets(ScenarioDefinition definition, ScenarioModReferenceIndex index)
        {
            for (int i = 0; definition != null && definition.AssetReferences != null && definition.AssetReferences.CustomSprites != null && i < definition.AssetReferences.CustomSprites.Count; i++)
                AddContent(index, definition.AssetReferences.CustomSprites[i] != null ? definition.AssetReferences.CustomSprites[i].Id : null, ScenarioModReferenceReason.SpriteOrAsset);
            for (int i = 0; definition != null && definition.AssetReferences != null && definition.AssetReferences.SceneSpritePlacements != null && i < definition.AssetReferences.SceneSpritePlacements.Count; i++)
                AddContent(index, definition.AssetReferences.SceneSpritePlacements[i] != null ? definition.AssetReferences.SceneSpritePlacements[i].SpriteId : null, ScenarioModReferenceReason.SpriteOrAsset);
        }

        private void AddBunkerObjects(ScenarioDefinition definition, ScenarioModReferenceIndex index)
        {
            for (int i = 0; definition != null && definition.BunkerEdits != null && definition.BunkerEdits.ObjectPlacements != null && i < definition.BunkerEdits.ObjectPlacements.Count; i++)
            {
                ObjectPlacement placement = definition.BunkerEdits.ObjectPlacements[i];
                AddContent(index, placement != null ? placement.PrefabReference : null, ScenarioModReferenceReason.BunkerObject);
                AddContent(index, placement != null ? placement.DefinitionReference : null, ScenarioModReferenceReason.BunkerObject);
            }
        }

        private void AddSurvivorContent(ScenarioDefinition definition, ScenarioModReferenceIndex index)
        {
            for (int i = 0; definition != null && definition.FamilySetup != null && definition.FamilySetup.Members != null && i < definition.FamilySetup.Members.Count; i++)
                AddMember(index, definition.FamilySetup.Members[i]);
            for (int i = 0; definition != null && definition.FamilySetup != null && definition.FamilySetup.FutureSurvivors != null && i < definition.FamilySetup.FutureSurvivors.Count; i++)
                AddMember(index, definition.FamilySetup.FutureSurvivors[i] != null ? definition.FamilySetup.FutureSurvivors[i].Survivor : null);
        }

        private void AddScheduledEffects(ScenarioDefinition definition, ScenarioModReferenceIndex index)
        {
            for (int i = 0; definition != null && definition.ScheduledActions != null && i < definition.ScheduledActions.Count; i++)
            {
                ScenarioScheduledActionDefinition action = definition.ScheduledActions[i];
                for (int e = 0; action != null && action.Effects != null && e < action.Effects.Count; e++)
                    AddContent(index, action.Effects[e] != null ? action.Effects[e].ItemId : null, ScenarioModReferenceReason.EffectKind);
            }
        }

        private void AddMember(ScenarioModReferenceIndex index, FamilyMemberConfig member)
        {
            for (int i = 0; member != null && member.Traits != null && i < member.Traits.Count; i++)
                AddContent(index, member.Traits[i], ScenarioModReferenceReason.SurvivorTraitOrStat);
            for (int i = 0; member != null && member.Stats != null && i < member.Stats.Count; i++)
                AddContent(index, member.Stats[i] != null ? member.Stats[i].StatId : null, ScenarioModReferenceReason.SurvivorTraitOrStat);
        }

        private void AddContent(ScenarioModReferenceIndex index, string contentId, ScenarioModReferenceReason reason)
        {
            string modId;
            string version;
            if (TryResolveOwner(contentId, out modId, out version))
                index.Add(modId, version, ScenarioModDependencyKind.Required, reason, contentId, false);
        }

        private static void ParseLegacyDependency(string value, out string modId, out string version)
        {
            modId = value;
            version = null;
            if (string.IsNullOrEmpty(value))
                return;
            int at = value.IndexOf('@');
            if (at > 0)
            {
                modId = value.Substring(0, at);
                version = value.Substring(at + 1);
            }
        }
    }
}
