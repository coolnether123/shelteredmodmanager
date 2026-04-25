using System;
using System.Collections.Generic;
using System.Globalization;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioObjectIdentityAssignmentService
    {
        public ScenarioObjectIdentityAssignmentSummary AssignMissingIds(ScenarioDefinition definition)
        {
            ScenarioObjectIdentityAssignmentSummary summary = new ScenarioObjectIdentityAssignmentSummary();
            if (definition == null)
                return summary;

            Dictionary<string, int> collisions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            AssignBunkerObjects(definition, collisions, summary);
            AssignSceneSprites(definition, collisions, summary);
            return summary;
        }

        public ScenarioObjectIdentityAssignmentSummary AssignMissingIds(ScenarioEditorSession session)
        {
            ScenarioObjectIdentityAssignmentSummary summary = AssignMissingIds(session != null ? session.WorkingDefinition : null);
            if (summary.AssignedCount > 0 && session != null && !session.DirtyFlags.Contains(ScenarioDirtySection.Bunker))
                session.DirtyFlags.Add(ScenarioDirtySection.Bunker);
            if (summary.AssignedAssetCount > 0 && session != null && !session.DirtyFlags.Contains(ScenarioDirtySection.Assets))
                session.DirtyFlags.Add(ScenarioDirtySection.Assets);
            return summary;
        }

        private static void AssignBunkerObjects(ScenarioDefinition definition, Dictionary<string, int> collisions, ScenarioObjectIdentityAssignmentSummary summary)
        {
            for (int i = 0; definition.BunkerEdits != null && definition.BunkerEdits.ObjectPlacements != null && i < definition.BunkerEdits.ObjectPlacements.Count; i++)
            {
                ObjectPlacement placement = definition.BunkerEdits.ObjectPlacements[i];
                if (placement == null)
                    continue;

                string id = BuildUniqueId(collisions, "object", placement.DefinitionReference ?? placement.PrefabReference, placement.Position, i);
                if (string.IsNullOrEmpty(placement.ScenarioObjectId))
                {
                    placement.ScenarioObjectId = id;
                    summary.AssignedCount++;
                    summary.Messages.Add("Assigned object id " + id + ".");
                }
                if (string.IsNullOrEmpty(placement.RuntimeBindingKey))
                {
                    placement.RuntimeBindingKey = "binding:" + placement.ScenarioObjectId;
                    summary.AssignedRuntimeBindingCount++;
                }
            }
        }

        private static void AssignSceneSprites(ScenarioDefinition definition, Dictionary<string, int> collisions, ScenarioObjectIdentityAssignmentSummary summary)
        {
            for (int i = 0; definition.AssetReferences != null && definition.AssetReferences.SceneSpritePlacements != null && i < definition.AssetReferences.SceneSpritePlacements.Count; i++)
            {
                SceneSpritePlacement placement = definition.AssetReferences.SceneSpritePlacements[i];
                if (placement == null)
                    continue;

                string id = BuildUniqueId(collisions, "scene_sprite", placement.SpriteId ?? placement.RuntimeSpriteKey ?? placement.RelativePath, placement.Position, i);
                if (string.IsNullOrEmpty(placement.ScenarioObjectId))
                {
                    placement.ScenarioObjectId = id;
                    summary.AssignedCount++;
                    summary.AssignedAssetCount++;
                    summary.Messages.Add("Assigned scene sprite id " + id + ".");
                }
                if (string.IsNullOrEmpty(placement.Id))
                    placement.Id = placement.ScenarioObjectId;
                if (string.IsNullOrEmpty(placement.RuntimeBindingKey))
                {
                    placement.RuntimeBindingKey = "binding:" + placement.ScenarioObjectId;
                    summary.AssignedRuntimeBindingCount++;
                }
            }
        }

        private static string BuildUniqueId(Dictionary<string, int> collisions, string kind, string content, ScenarioVector3 position, int fallbackOrdinal)
        {
            string baseId = Sanitize(kind) + "." + Sanitize(content);
            if (position != null)
                baseId += "." + Format(position.X) + "." + Format(position.Y) + "." + Format(position.Z);
            if (baseId.Length < kind.Length + 4)
                baseId += "." + fallbackOrdinal.ToString(CultureInfo.InvariantCulture);

            int ordinal;
            if (!collisions.TryGetValue(baseId, out ordinal))
            {
                collisions.Add(baseId, 1);
                return baseId;
            }
            collisions[baseId] = ordinal + 1;
            return baseId + "." + ordinal.ToString(CultureInfo.InvariantCulture);
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "unknown";
            string result = string.Empty;
            for (int i = 0; i < value.Length; i++)
            {
                char c = char.ToLowerInvariant(value[i]);
                result += char.IsLetterOrDigit(c) ? c : '_';
            }
            return result.Trim('_');
        }

        private static string Format(float value)
        {
            return Math.Round(value, 2).ToString("0.##", CultureInfo.InvariantCulture).Replace('.', '_').Replace('-', 'n');
        }
    }

    internal sealed class ScenarioObjectIdentityAssignmentSummary
    {
        public ScenarioObjectIdentityAssignmentSummary()
        {
            Messages = new List<string>();
        }

        public int AssignedCount { get; set; }
        public int AssignedRuntimeBindingCount { get; set; }
        public int AssignedAssetCount { get; set; }
        public List<string> Messages { get; private set; }
    }
}
