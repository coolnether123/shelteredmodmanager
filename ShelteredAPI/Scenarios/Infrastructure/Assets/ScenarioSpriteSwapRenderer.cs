using System;
using System.Collections.Generic;
using ModAPI.Core;
using ModAPI.Scenarios;
using UnityEngine;

using ShelteredAPI.Hooks;
using ShelteredAPI.Scenarios.Definitions;
namespace ShelteredAPI.Scenarios.Infrastructure.Assets{
    internal sealed class ScenarioSpriteSwapRenderer
    {
        private sealed class BaselineState
        {
            public ScenarioSpriteTargetComponentKind Kind;
            public Sprite Sprite;
        }

        private readonly ScenarioSpriteRuntimeResolver _resolver;
        private readonly Dictionary<string, BaselineState> _baselineByTarget = new Dictionary<string, BaselineState>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ScenarioSpriteRuntimeResolver.ResolvedTarget> _targetCache = new Dictionary<string, ScenarioSpriteRuntimeResolver.ResolvedTarget>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _activeTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _activeAnimationTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        internal ScenarioSpriteSwapRenderer(ScenarioSpriteRuntimeResolver resolver)
        {
            _resolver = resolver;
        }

        public int Apply(IList<ScenarioSpriteSwapPlanner.PlannedSwap> plan, string reason)
        {
            HashSet<string> nextTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, Dictionary<string, Sprite>> animatedByTarget = new Dictionary<string, Dictionary<string, Sprite>>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, ScenarioSpriteTargetComponentKind> animatedKindByTarget = new Dictionary<string, ScenarioSpriteTargetComponentKind>(StringComparer.OrdinalIgnoreCase);
            int appliedCount = 0;
            for (int i = 0; plan != null && i < plan.Count; i++)
            {
                ScenarioSpriteSwapPlanner.PlannedSwap entry = plan[i];
                if (entry == null || entry.Sprite == null || string.IsNullOrEmpty(entry.TargetPath))
                    continue;

                if (!string.IsNullOrEmpty(entry.AnimationFrameRuntimeSpriteKey))
                {
                    Dictionary<string, Sprite> frames;
                    if (!animatedByTarget.TryGetValue(entry.TargetPath, out frames))
                    {
                        frames = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
                        animatedByTarget[entry.TargetPath] = frames;
                        animatedKindByTarget[entry.TargetPath] = entry.TargetComponent;
                    }

                    frames[entry.AnimationFrameRuntimeSpriteKey] = entry.Sprite;
                    nextTargets.Add(entry.TargetPath);
                    continue;
                }

                ScenarioSpriteRuntimeResolver.ResolvedTarget runtimeTarget;
                if (!TryResolveRuntimeTarget(entry.TargetPath, entry.TargetComponent, out runtimeTarget))
                    continue;

                CaptureBaseline(entry.TargetPath, runtimeTarget);
                if (ScenarioSpriteRuntimeMutationService.TryApply(runtimeTarget, entry.Sprite))
                    appliedCount++;

                nextTargets.Add(entry.TargetPath);
            }

            HashSet<string> nextAnimationTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, Dictionary<string, Sprite>> pair in animatedByTarget)
            {
                ScenarioSpriteTargetComponentKind targetKind;
                if (!animatedKindByTarget.TryGetValue(pair.Key, out targetKind))
                    targetKind = ScenarioSpriteTargetComponentKind.SpriteRenderer;

                ScenarioSpriteRuntimeResolver.ResolvedTarget runtimeTarget;
                if (!TryResolveRuntimeTarget(pair.Key, targetKind, out runtimeTarget))
                    continue;

                CaptureBaseline(pair.Key, runtimeTarget);
                if (ScenarioSpriteRuntimeMutationService.TryApplyAnimatedFrameSwaps(runtimeTarget, pair.Value))
                    appliedCount += pair.Value.Count;
                nextAnimationTargets.Add(pair.Key);
            }

            RestoreRemovedAnimationTargets(nextAnimationTargets);
            RestoreRemovedTargets(nextTargets, reason);
            _activeTargets.Clear();
            foreach (string targetPath in nextTargets)
                _activeTargets.Add(targetPath);
            _activeAnimationTargets.Clear();
            foreach (string targetPath in nextAnimationTargets)
                _activeAnimationTargets.Add(targetPath);
            return appliedCount;
        }

        public void Clear(string reason)
        {
            RestoreRemovedAnimationTargets(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            RestoreRemovedTargets(new HashSet<string>(StringComparer.OrdinalIgnoreCase), reason);
            _baselineByTarget.Clear();
            _targetCache.Clear();
            _activeTargets.Clear();
            _activeAnimationTargets.Clear();
        }

        private void RestoreRemovedAnimationTargets(HashSet<string> nextTargets)
        {
            List<string> toRestore = new List<string>();
            foreach (string targetPath in _activeAnimationTargets)
            {
                if (!nextTargets.Contains(targetPath))
                    toRestore.Add(targetPath);
            }

            for (int i = 0; i < toRestore.Count; i++)
            {
                ScenarioSpriteRuntimeResolver.ResolvedTarget runtimeTarget;
                if (TryResolveRuntimeTarget(toRestore[i], ScenarioSpriteTargetComponentKind.SpriteRenderer, out runtimeTarget))
                    ScenarioSpriteRuntimeMutationService.ClearAnimatedFrameSwaps(runtimeTarget);
            }
        }

        private void RestoreRemovedTargets(HashSet<string> nextTargets, string reason)
        {
            List<string> toRestore = new List<string>();
            foreach (string targetPath in _activeTargets)
            {
                if (!nextTargets.Contains(targetPath))
                    toRestore.Add(targetPath);
            }

            for (int i = 0; i < toRestore.Count; i++)
                RestoreBaseline(toRestore[i], reason);
        }

        private void RestoreBaseline(string targetPath, string reason)
        {
            BaselineState baseline;
            if (!_baselineByTarget.TryGetValue(targetPath, out baseline) || baseline == null)
                return;

            ScenarioSpriteRuntimeResolver.ResolvedTarget runtimeTarget;
            if (!TryResolveRuntimeTarget(targetPath, baseline.Kind, out runtimeTarget))
                return;

            ScenarioSpriteRuntimeMutationService.TryApply(runtimeTarget, baseline.Sprite);
        }

        private void CaptureBaseline(string targetPath, ScenarioSpriteRuntimeResolver.ResolvedTarget runtimeTarget)
        {
            if (_baselineByTarget.ContainsKey(targetPath) || runtimeTarget == null)
                return;

            _baselineByTarget[targetPath] = new BaselineState
            {
                Kind = runtimeTarget.Kind,
                Sprite = runtimeTarget.CurrentSprite
            };
        }

        private bool TryResolveRuntimeTarget(string targetPath, ScenarioSpriteTargetComponentKind preferredKind, out ScenarioSpriteRuntimeResolver.ResolvedTarget runtimeTarget)
        {
            runtimeTarget = null;
            if (string.IsNullOrEmpty(targetPath))
                return false;

            if (_targetCache.TryGetValue(targetPath, out runtimeTarget) && ScenarioSpriteRuntimeResolver.IsAlive(runtimeTarget))
                return true;

            if (!_resolver.TryResolve(targetPath, preferredKind, out runtimeTarget) || runtimeTarget == null)
            {
                MMLog.WarnOnce("ScenarioSpriteSwapRenderer.Target." + targetPath, "Sprite swap target path was not found: " + targetPath);
                return false;
            }

            _targetCache[targetPath] = runtimeTarget;
            return true;
        }
    }
}
