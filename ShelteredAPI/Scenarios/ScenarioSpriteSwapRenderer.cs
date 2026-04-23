using System;
using System.Collections.Generic;
using ModAPI.Core;
using ModAPI.Scenarios;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioSpriteSwapRenderer
    {
        private sealed class BaselineState
        {
            public ScenarioSpriteTargetComponentKind Kind;
            public Sprite Sprite;
        }

        private readonly ScenarioSpriteRuntimeResolver _resolver = new ScenarioSpriteRuntimeResolver();
        private readonly Dictionary<string, BaselineState> _baselineByTarget = new Dictionary<string, BaselineState>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ScenarioSpriteRuntimeResolver.ResolvedTarget> _targetCache = new Dictionary<string, ScenarioSpriteRuntimeResolver.ResolvedTarget>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _activeTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public int Apply(IList<ScenarioSpriteSwapPlanner.PlannedSwap> plan, string reason)
        {
            HashSet<string> nextTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int appliedCount = 0;
            for (int i = 0; plan != null && i < plan.Count; i++)
            {
                ScenarioSpriteSwapPlanner.PlannedSwap entry = plan[i];
                if (entry == null || entry.Sprite == null || string.IsNullOrEmpty(entry.TargetPath))
                    continue;

                ScenarioSpriteRuntimeResolver.ResolvedTarget runtimeTarget;
                if (!TryResolveRuntimeTarget(entry.TargetPath, entry.TargetComponent, out runtimeTarget))
                    continue;

                CaptureBaseline(entry.TargetPath, runtimeTarget);
                if (ScenarioSpriteRuntimeMutationService.TryApply(runtimeTarget, entry.Sprite))
                    appliedCount++;

                nextTargets.Add(entry.TargetPath);
            }

            RestoreRemovedTargets(nextTargets, reason);
            _activeTargets.Clear();
            foreach (string targetPath in nextTargets)
                _activeTargets.Add(targetPath);
            return appliedCount;
        }

        public void Clear(string reason)
        {
            RestoreRemovedTargets(new HashSet<string>(StringComparer.OrdinalIgnoreCase), reason);
            _baselineByTarget.Clear();
            _targetCache.Clear();
            _activeTargets.Clear();
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
