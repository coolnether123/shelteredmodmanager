using System;
using System.Collections.Generic;
using ModAPI.Core;
using ModAPI.Scenarios;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioSpriteSwapPlanner
    {
        private readonly IScenarioSpriteAssetResolver _assetResolver;

        public ScenarioSpriteSwapPlanner(IScenarioSpriteAssetResolver assetResolver)
        {
            _assetResolver = assetResolver;
        }

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

            string packRoot = !string.IsNullOrEmpty(scenarioFilePath) ? System.IO.Path.GetDirectoryName(scenarioFilePath) : null;
            Dictionary<string, PlannedSwap> byTarget = new Dictionary<string, PlannedSwap>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < definition.AssetReferences.SpriteSwaps.Count; i++)
            {
                SpriteSwapRule rule = definition.AssetReferences.SpriteSwaps[i];
                if (rule == null || string.IsNullOrEmpty(rule.TargetPath))
                    continue;

                int effectiveDay = rule.Day.HasValue ? Math.Max(1, rule.Day.Value) : 1;
                if (effectiveDay > currentDay)
                    continue;

                Sprite sprite = _assetResolver.ResolveSprite(
                    definition,
                    packRoot,
                    rule.SpriteId,
                    rule.RelativePath,
                    rule.RuntimeSpriteKey,
                    "sprite swap target '" + rule.TargetPath + "'");
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
