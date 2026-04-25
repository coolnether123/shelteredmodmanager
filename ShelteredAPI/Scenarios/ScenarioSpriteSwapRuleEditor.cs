using System;
using System.Collections.Generic;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    // Pure, stateless helpers for reading and mutating SpriteSwap rules. Split out of
    // ScenarioSpriteSwapAuthoringService so history, clipboard, and apply paths can
    // share the exact same rule semantics.
    internal static class ScenarioSpriteSwapRuleEditor
    {
        public static SpriteSwapRule FindActiveRule(ScenarioDefinition definition, string targetPath, int currentDay)
        {
            if (definition == null
                || definition.AssetReferences == null
                || definition.AssetReferences.SpriteSwaps == null
                || string.IsNullOrEmpty(targetPath))
                return null;

            SpriteSwapRule selected = null;
            int selectedDay = int.MinValue;
            List<SpriteSwapRule> rules = definition.AssetReferences.SpriteSwaps;
            for (int i = 0; i < rules.Count; i++)
            {
                SpriteSwapRule rule = rules[i];
                if (rule == null || !string.Equals(rule.TargetPath, targetPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                int effectiveDay = rule.Day.HasValue ? Math.Max(1, rule.Day.Value) : 1;
                if (effectiveDay > currentDay || effectiveDay < selectedDay)
                    continue;

                selected = rule;
                selectedDay = effectiveDay;
            }

            return selected;
        }

        public static SpriteSwapRule FindFirstRule(ScenarioDefinition definition, string targetPath)
        {
            if (definition == null
                || definition.AssetReferences == null
                || definition.AssetReferences.SpriteSwaps == null
                || string.IsNullOrEmpty(targetPath))
                return null;

            List<SpriteSwapRule> rules = definition.AssetReferences.SpriteSwaps;
            for (int i = 0; i < rules.Count; i++)
            {
                SpriteSwapRule rule = rules[i];
                if (rule != null && string.Equals(rule.TargetPath, targetPath, StringComparison.OrdinalIgnoreCase))
                    return rule;
            }

            return null;
        }

        public static SpriteSwapRule FindEditableRule(ScenarioDefinition definition, string targetPath, int currentDay)
        {
            return FindActiveRule(definition, targetPath, currentDay) ?? FindFirstRule(definition, targetPath);
        }

        public static bool RuleMatchesCandidate(SpriteSwapRule rule, ScenarioSpriteCatalogService.SpriteCandidate candidate)
        {
            if (rule == null || candidate == null)
                return false;

            if (candidate.SourceKind == ScenarioSpriteCatalogService.SpriteCandidateSourceKind.VanillaRuntime)
                return string.Equals(rule.RuntimeSpriteKey, candidate.RuntimeSpriteKey, StringComparison.OrdinalIgnoreCase);

            bool spriteIdMatch = !string.IsNullOrEmpty(candidate.SpriteId)
                && string.Equals(rule.SpriteId, candidate.SpriteId, StringComparison.OrdinalIgnoreCase);
            bool relativePathMatch = !string.IsNullOrEmpty(candidate.RelativePath)
                && string.Equals(rule.RelativePath, candidate.RelativePath, StringComparison.OrdinalIgnoreCase);
            return spriteIdMatch || relativePathMatch;
        }

        public static void EnsureAssetReferences(ScenarioDefinition definition)
        {
            if (definition == null)
                return;

            if (definition.AssetReferences == null)
                definition.AssetReferences = new AssetReferencesDefinition();
        }

        public static SpriteSwapRule ApplyCandidate(
            ScenarioDefinition definition,
            ScenarioSpriteRuntimeResolver.ResolvedTarget resolvedTarget,
            ScenarioSpriteCatalogService.SpriteCandidate candidate,
            int currentDay)
        {
            if (definition == null || resolvedTarget == null || candidate == null)
                return null;

            EnsureAssetReferences(definition);
            SpriteSwapRule rule = FindEditableRule(definition, resolvedTarget.TargetPath, currentDay);
            if (rule == null)
            {
                rule = new SpriteSwapRule
                {
                    Id = BuildRuleId(resolvedTarget.TargetPath),
                    TargetPath = resolvedTarget.TargetPath,
                    Day = 1
                };
                definition.AssetReferences.SpriteSwaps.Add(rule);
            }

            rule.TargetPath = resolvedTarget.TargetPath;
            rule.TargetComponent = resolvedTarget.Kind;
            rule.SpriteId = null;
            rule.RelativePath = null;
            rule.RuntimeSpriteKey = null;

            if (candidate.SourceKind == ScenarioSpriteCatalogService.SpriteCandidateSourceKind.VanillaRuntime)
            {
                rule.RuntimeSpriteKey = candidate.RuntimeSpriteKey;
            }
            else
            {
                rule.SpriteId = candidate.SpriteId;
                rule.RelativePath = candidate.RelativePath;
            }

            return rule;
        }

        public static bool ClearActiveRule(ScenarioDefinition definition, string targetPath, int currentDay)
        {
            if (definition == null || definition.AssetReferences == null || definition.AssetReferences.SpriteSwaps == null)
                return false;

            SpriteSwapRule activeRule = FindActiveRule(definition, targetPath, currentDay);
            if (activeRule == null)
                return false;

            definition.AssetReferences.SpriteSwaps.Remove(activeRule);
            return true;
        }

        public static SpriteSwapRule CloneRule(SpriteSwapRule source)
        {
            if (source == null)
                return null;

            return new SpriteSwapRule
            {
                Id = source.Id,
                TargetPath = source.TargetPath,
                SpriteId = source.SpriteId,
                RelativePath = source.RelativePath,
                RuntimeSpriteKey = source.RuntimeSpriteKey,
                Day = source.Day,
                TargetComponent = source.TargetComponent
            };
        }

        public static List<SpriteSwapRule> SnapshotRules(ScenarioDefinition definition)
        {
            List<SpriteSwapRule> snapshot = new List<SpriteSwapRule>();
            if (definition == null || definition.AssetReferences == null || definition.AssetReferences.SpriteSwaps == null)
                return snapshot;

            List<SpriteSwapRule> source = definition.AssetReferences.SpriteSwaps;
            for (int i = 0; i < source.Count; i++)
            {
                SpriteSwapRule clone = CloneRule(source[i]);
                if (clone != null)
                    snapshot.Add(clone);
            }

            return snapshot;
        }

        public static void RestoreRules(ScenarioDefinition definition, List<SpriteSwapRule> snapshot)
        {
            if (definition == null)
                return;

            EnsureAssetReferences(definition);
            definition.AssetReferences.SpriteSwaps.Clear();
            if (snapshot == null)
                return;

            for (int i = 0; i < snapshot.Count; i++)
            {
                SpriteSwapRule clone = CloneRule(snapshot[i]);
                if (clone != null)
                    definition.AssetReferences.SpriteSwaps.Add(clone);
            }
        }

        public static string DescribeRule(SpriteSwapRule rule)
        {
            if (rule == null)
                return "No active sprite swap.";

            if (!string.IsNullOrEmpty(rule.RuntimeSpriteKey))
                return "Vanilla/runtime sprite swap active.";
            if (!string.IsNullOrEmpty(rule.SpriteId))
                return "Modded sprite '" + rule.SpriteId + "' active.";
            if (!string.IsNullOrEmpty(rule.RelativePath))
                return "Modded sprite '" + rule.RelativePath + "' active.";
            return "Sprite swap active.";
        }

        public static string DescribeRuleShort(SpriteSwapRule rule)
        {
            if (rule == null)
                return "<none>";
            if (!string.IsNullOrEmpty(rule.RuntimeSpriteKey))
                return rule.RuntimeSpriteKey;
            if (!string.IsNullOrEmpty(rule.SpriteId))
                return rule.SpriteId;
            if (!string.IsNullOrEmpty(rule.RelativePath))
                return rule.RelativePath;
            return "<rule>";
        }

        public static string BuildRuleId(string targetPath)
        {
            string safe = string.IsNullOrEmpty(targetPath) ? "target" : targetPath.Replace('/', '_').Replace('\\', '_');
            return "sprite_swap_" + safe.ToLowerInvariant();
        }
    }
}
