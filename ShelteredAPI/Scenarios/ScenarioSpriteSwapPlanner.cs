using System;
using System.Collections.Generic;
using System.IO;
using ModAPI.Core;
using ModAPI.Scenarios;
using ShelteredAPI.Content;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioSpriteSwapPlanner
    {
        internal sealed class PlannedSwap
        {
            public string RuleId;
            public string TargetPath;
            public ScenarioSpriteTargetComponentKind TargetComponent;
            public int Day;
            public Sprite Sprite;
        }

        public List<PlannedSwap> BuildPlan(ScenarioDefinition definition, string scenarioFilePath, int currentDay)
        {
            List<PlannedSwap> plan = new List<PlannedSwap>();
            if (definition == null
                || definition.AssetReferences == null
                || definition.AssetReferences.SpriteSwaps == null
                || definition.AssetReferences.SpriteSwaps.Count == 0)
            {
                return plan;
            }

            string packRoot = !string.IsNullOrEmpty(scenarioFilePath) ? Path.GetDirectoryName(scenarioFilePath) : null;
            Dictionary<string, PlannedSwap> byTarget = new Dictionary<string, PlannedSwap>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < definition.AssetReferences.SpriteSwaps.Count; i++)
            {
                SpriteSwapRule rule = definition.AssetReferences.SpriteSwaps[i];
                if (rule == null || string.IsNullOrEmpty(rule.TargetPath))
                    continue;

                int effectiveDay = rule.Day.HasValue ? Math.Max(1, rule.Day.Value) : 1;
                if (effectiveDay > currentDay)
                    continue;

                Sprite sprite = ResolveSprite(definition, packRoot, rule);
                if (sprite == null)
                    continue;

                PlannedSwap planned = new PlannedSwap
                {
                    RuleId = !string.IsNullOrEmpty(rule.Id) ? rule.Id : ("swap_" + i),
                    TargetPath = rule.TargetPath,
                    TargetComponent = rule.TargetComponent,
                    Day = effectiveDay,
                    Sprite = sprite
                };

                PlannedSwap existing;
                if (!byTarget.TryGetValue(planned.TargetPath, out existing)
                    || existing == null
                    || planned.Day >= existing.Day)
                {
                    byTarget[planned.TargetPath] = planned;
                }
            }

            foreach (KeyValuePair<string, PlannedSwap> pair in byTarget)
                plan.Add(pair.Value);

            plan.Sort(ComparePlannedSwap);
            return plan;
        }

        private static Sprite ResolveSprite(ScenarioDefinition definition, string packRoot, SpriteSwapRule rule)
        {
            string relativePath = ResolveRelativePath(definition, rule);
            if (string.IsNullOrEmpty(relativePath) || string.IsNullOrEmpty(packRoot))
                return null;

            try
            {
                return AssetLoader.LoadSprite(packRoot, relativePath, 100f);
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioSpriteSwapPlanner] Failed to load sprite swap asset for target '" + rule.TargetPath
                    + "': " + relativePath + " (" + ex.Message + ")");
                return null;
            }
        }

        private static string ResolveRelativePath(ScenarioDefinition definition, SpriteSwapRule rule)
        {
            if (rule == null)
                return null;

            if (!string.IsNullOrEmpty(rule.RelativePath))
                return rule.RelativePath;

            if (definition == null || definition.AssetReferences == null || definition.AssetReferences.CustomSprites == null || string.IsNullOrEmpty(rule.SpriteId))
                return null;

            for (int i = 0; i < definition.AssetReferences.CustomSprites.Count; i++)
            {
                SpriteRef sprite = definition.AssetReferences.CustomSprites[i];
                if (sprite != null && string.Equals(sprite.Id, rule.SpriteId, StringComparison.OrdinalIgnoreCase))
                    return sprite.RelativePath;
            }

            MMLog.WriteWarning("[ScenarioSpriteSwapPlanner] Sprite swap references unknown spriteId '" + rule.SpriteId
                + "' for target '" + rule.TargetPath + "'.");
            return null;
        }

        private static int ComparePlannedSwap(PlannedSwap left, PlannedSwap right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return 1;
            if (right == null) return -1;

            int path = string.Compare(left.TargetPath, right.TargetPath, StringComparison.OrdinalIgnoreCase);
            if (path != 0) return path;
            return left.Day.CompareTo(right.Day);
        }
    }
}
