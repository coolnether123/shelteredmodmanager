using System;
using System.Collections.Generic;
using ModAPI.Scenarios;

using ShelteredAPI.Content;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Public;
using ShelteredScenarioEditor.Infrastructure.Assets;
namespace ShelteredScenarioEditor.Application.Assets{
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
                if (rule == null
                    || rule.AnimationFrameIndex.HasValue
                    || !string.Equals(rule.TargetPath, targetPath, StringComparison.OrdinalIgnoreCase))
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
                if (rule != null
                    && !rule.AnimationFrameIndex.HasValue
                    && string.Equals(rule.TargetPath, targetPath, StringComparison.OrdinalIgnoreCase))
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
            ScenarioRuntimeSpriteTarget resolvedTarget,
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
            rule.AnimationFrameIndex = null;
            rule.AnimationFrameRuntimeSpriteKey = null;

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

        public static int ClearActiveRulesForTarget(ScenarioDefinition definition, string targetPath, int currentDay)
        {
            if (definition == null || definition.AssetReferences == null || definition.AssetReferences.SpriteSwaps == null)
                return 0;

            int removed = 0;
            SpriteSwapRule activeRule = FindActiveRule(definition, targetPath, currentDay);
            if (activeRule != null && definition.AssetReferences.SpriteSwaps.Remove(activeRule))
                removed++;

            removed += ClearAnimationFrameRules(definition, targetPath, currentDay);
            return removed;
        }

        public static bool HasAnimationFrameRule(
            ScenarioDefinition definition,
            string targetPath,
            int? frameIndex,
            string runtimeSpriteKey,
            int currentDay)
        {
            return FindAnimationFrameRule(definition, targetPath, frameIndex, runtimeSpriteKey, currentDay) != null;
        }

        public static SpriteSwapRule FindAnimationFrameRule(
            ScenarioDefinition definition,
            string targetPath,
            int? frameIndex,
            string runtimeSpriteKey,
            int currentDay)
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
                if (!IsActiveAnimationFrameRule(rule, targetPath, currentDay))
                    continue;

                if (FrameIdentityMatches(rule, frameIndex, runtimeSpriteKey))
                    return rule;
            }

            return null;
        }

        public static int ClearAnimationFrameRule(
            ScenarioDefinition definition,
            string targetPath,
            int? frameIndex,
            string runtimeSpriteKey,
            int currentDay)
        {
            if (definition == null || definition.AssetReferences == null || definition.AssetReferences.SpriteSwaps == null)
                return 0;

            int removed = 0;
            List<SpriteSwapRule> rules = definition.AssetReferences.SpriteSwaps;
            for (int i = rules.Count - 1; i >= 0; i--)
            {
                SpriteSwapRule rule = rules[i];
                if (!IsActiveAnimationFrameRule(rule, targetPath, currentDay))
                    continue;

                if (!FrameIdentityMatches(rule, frameIndex, runtimeSpriteKey))
                    continue;

                rules.RemoveAt(i);
                removed++;
            }

            return removed;
        }

        public static int ClearAnimationFrameRules(ScenarioDefinition definition, string targetPath, int currentDay)
        {
            if (definition == null || definition.AssetReferences == null || definition.AssetReferences.SpriteSwaps == null)
                return 0;

            int removed = 0;
            List<SpriteSwapRule> rules = definition.AssetReferences.SpriteSwaps;
            for (int i = rules.Count - 1; i >= 0; i--)
            {
                if (!IsActiveAnimationFrameRule(rules[i], targetPath, currentDay))
                    continue;

                rules.RemoveAt(i);
                removed++;
            }

            return removed;
        }

        private static bool IsActiveAnimationFrameRule(SpriteSwapRule rule, string targetPath, int currentDay)
        {
            if (rule == null
                || !rule.AnimationFrameIndex.HasValue
                || string.IsNullOrEmpty(targetPath)
                || !string.Equals(rule.TargetPath, targetPath, StringComparison.OrdinalIgnoreCase))
                return false;

            int effectiveDay = rule.Day.HasValue ? Math.Max(1, rule.Day.Value) : 1;
            return effectiveDay <= currentDay;
        }

        private static bool FrameIdentityMatches(SpriteSwapRule rule, int? frameIndex, string runtimeSpriteKey)
        {
            if (rule == null)
                return false;

            if (frameIndex.HasValue
                && rule.AnimationFrameIndex.HasValue
                && rule.AnimationFrameIndex.Value == frameIndex.Value)
            {
                return true;
            }

            return !string.IsNullOrEmpty(runtimeSpriteKey)
                && !string.IsNullOrEmpty(rule.AnimationFrameRuntimeSpriteKey)
                && string.Equals(rule.AnimationFrameRuntimeSpriteKey, runtimeSpriteKey, StringComparison.OrdinalIgnoreCase);
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
                AnimationFrameIndex = source.AnimationFrameIndex,
                AnimationFrameRuntimeSpriteKey = source.AnimationFrameRuntimeSpriteKey,
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

            if (rule.AnimationFrameIndex.HasValue)
                return "Animation frame " + (rule.AnimationFrameIndex.Value + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + " swap active.";
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
            if (rule.AnimationFrameIndex.HasValue)
                return "frame " + (rule.AnimationFrameIndex.Value + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
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
