using System;
using System.Collections.Generic;
using ModAPI.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioSpriteSwapRenderer
    {
        private sealed class BaselineState
        {
            public ScenarioSpriteTargetComponentKind Kind;
            public Sprite Sprite;
        }

        private sealed class RuntimeTarget
        {
            public ScenarioSpriteTargetComponentKind Kind;
            public SpriteRenderer SpriteRenderer;
            public UI2DSprite Ui2DSprite;
        }

        private readonly Dictionary<string, BaselineState> _baselineByTarget = new Dictionary<string, BaselineState>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, RuntimeTarget> _targetCache = new Dictionary<string, RuntimeTarget>(StringComparer.OrdinalIgnoreCase);
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

                RuntimeTarget runtimeTarget;
                if (!TryResolveRuntimeTarget(entry.TargetPath, entry.TargetComponent, out runtimeTarget))
                    continue;

                CaptureBaseline(entry.TargetPath, runtimeTarget);
                if (ApplySprite(runtimeTarget, entry.Sprite))
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

            RuntimeTarget runtimeTarget;
            if (!TryResolveRuntimeTarget(targetPath, baseline.Kind, out runtimeTarget))
                return;

            if (baseline.Kind == ScenarioSpriteTargetComponentKind.SpriteRenderer && runtimeTarget.SpriteRenderer != null)
                runtimeTarget.SpriteRenderer.sprite = baseline.Sprite;
            else if (baseline.Kind == ScenarioSpriteTargetComponentKind.UI2DSprite && runtimeTarget.Ui2DSprite != null)
                runtimeTarget.Ui2DSprite.sprite2D = baseline.Sprite;
        }

        private void CaptureBaseline(string targetPath, RuntimeTarget runtimeTarget)
        {
            if (_baselineByTarget.ContainsKey(targetPath) || runtimeTarget == null)
                return;

            Sprite baselineSprite = null;
            if (runtimeTarget.Kind == ScenarioSpriteTargetComponentKind.SpriteRenderer && runtimeTarget.SpriteRenderer != null)
                baselineSprite = runtimeTarget.SpriteRenderer.sprite;
            else if (runtimeTarget.Kind == ScenarioSpriteTargetComponentKind.UI2DSprite && runtimeTarget.Ui2DSprite != null)
                baselineSprite = runtimeTarget.Ui2DSprite.sprite2D;

            _baselineByTarget[targetPath] = new BaselineState
            {
                Kind = runtimeTarget.Kind,
                Sprite = baselineSprite
            };
        }

        private static bool ApplySprite(RuntimeTarget runtimeTarget, Sprite sprite)
        {
            if (runtimeTarget == null || sprite == null)
                return false;

            if (runtimeTarget.Kind == ScenarioSpriteTargetComponentKind.SpriteRenderer && runtimeTarget.SpriteRenderer != null)
            {
                runtimeTarget.SpriteRenderer.sprite = sprite;
                return true;
            }

            if (runtimeTarget.Kind == ScenarioSpriteTargetComponentKind.UI2DSprite && runtimeTarget.Ui2DSprite != null)
            {
                runtimeTarget.Ui2DSprite.sprite2D = sprite;
                return true;
            }

            return false;
        }

        private bool TryResolveRuntimeTarget(string targetPath, ScenarioSpriteTargetComponentKind preferredKind, out RuntimeTarget runtimeTarget)
        {
            runtimeTarget = null;
            if (string.IsNullOrEmpty(targetPath))
                return false;

            if (_targetCache.TryGetValue(targetPath, out runtimeTarget) && IsTargetAlive(runtimeTarget))
                return true;

            Transform target = FindTransformByPath(targetPath);
            if (target == null)
            {
                MMLog.WarnOnce("ScenarioSpriteSwapRenderer.Target." + targetPath, "Sprite swap target path was not found: " + targetPath);
                return false;
            }

            runtimeTarget = CreateRuntimeTarget(target, preferredKind);
            if (runtimeTarget == null)
            {
                MMLog.WarnOnce("ScenarioSpriteSwapRenderer.Component." + targetPath, "Sprite swap target has no supported sprite component: " + targetPath);
                return false;
            }

            _targetCache[targetPath] = runtimeTarget;
            return true;
        }

        private static RuntimeTarget CreateRuntimeTarget(Transform target, ScenarioSpriteTargetComponentKind preferredKind)
        {
            if (target == null)
                return null;

            SpriteRenderer spriteRenderer = null;
            UI2DSprite ui2DSprite = null;

            if (preferredKind == ScenarioSpriteTargetComponentKind.Auto || preferredKind == ScenarioSpriteTargetComponentKind.SpriteRenderer)
                spriteRenderer = target.GetComponent<SpriteRenderer>() ?? target.GetComponentInChildren<SpriteRenderer>(true);
            if (preferredKind == ScenarioSpriteTargetComponentKind.Auto || preferredKind == ScenarioSpriteTargetComponentKind.UI2DSprite)
                ui2DSprite = target.GetComponent<UI2DSprite>() ?? target.GetComponentInChildren<UI2DSprite>(true);

            if (spriteRenderer != null)
            {
                return new RuntimeTarget
                {
                    Kind = ScenarioSpriteTargetComponentKind.SpriteRenderer,
                    SpriteRenderer = spriteRenderer
                };
            }

            if (ui2DSprite != null)
            {
                return new RuntimeTarget
                {
                    Kind = ScenarioSpriteTargetComponentKind.UI2DSprite,
                    Ui2DSprite = ui2DSprite
                };
            }

            return null;
        }

        private static bool IsTargetAlive(RuntimeTarget runtimeTarget)
        {
            if (runtimeTarget == null)
                return false;

            if (runtimeTarget.Kind == ScenarioSpriteTargetComponentKind.SpriteRenderer)
                return runtimeTarget.SpriteRenderer != null;
            if (runtimeTarget.Kind == ScenarioSpriteTargetComponentKind.UI2DSprite)
                return runtimeTarget.Ui2DSprite != null;
            return false;
        }

        private static Transform FindTransformByPath(string targetPath)
        {
            if (string.IsNullOrEmpty(targetPath))
                return null;

            string[] segments = targetPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
                return null;

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
                return null;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root == null || !string.Equals(root.name, segments[0], StringComparison.Ordinal))
                    continue;

                Transform current = root.transform;
                bool matched = true;
                for (int segmentIndex = 1; segmentIndex < segments.Length; segmentIndex++)
                {
                    current = FindChildByName(current, segments[segmentIndex]);
                    if (current == null)
                    {
                        matched = false;
                        break;
                    }
                }

                if (matched && current != null)
                    return current;
            }

            return null;
        }

        private static Transform FindChildByName(Transform parent, string name)
        {
            if (parent == null || string.IsNullOrEmpty(name))
                return null;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child != null && string.Equals(child.name, name, StringComparison.Ordinal))
                    return child;
            }

            return null;
        }
    }
}
