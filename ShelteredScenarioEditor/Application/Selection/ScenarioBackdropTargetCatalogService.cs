using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

using ShelteredScenarioEditor.Application.Authoring;

namespace ShelteredScenarioEditor.Application.Selection
{
    /// <summary>
    /// Discovers the editable backdrop layers in the currently loaded shelter scene.
    /// The per-frame scene snapshot refreshes automatically so a Standard, Stasis,
    /// or Surrounded base reload exposes the backdrop objects belonging to that scene.
    /// </summary>
    internal sealed class ScenarioBackdropTargetCatalogService
    {
        private readonly ScenarioAuthoringSelectionService _selectionService;
        private readonly List<ScenarioAuthoringTarget> _cachedTargets = new List<ScenarioAuthoringTarget>();
        private int _cachedFrame = -1;
        private string _cachedScene;

        public ScenarioBackdropTargetCatalogService(ScenarioAuthoringSelectionService selectionService)
        {
            _selectionService = selectionService;
        }

        public List<ScenarioAuthoringTarget> GetTargets()
        {
            string scene = SceneManager.GetActiveScene().name ?? string.Empty;
            if (_cachedFrame == Time.frameCount && string.Equals(_cachedScene, scene, StringComparison.Ordinal))
                return CopyTargets(_cachedTargets);

            List<ScenarioAuthoringTarget> targets = new List<ScenarioAuthoringTarget>();
            HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            SpriteRenderer[] renderers = UnityEngine.Object.FindObjectsOfType<SpriteRenderer>();
            for (int i = 0; renderers != null && i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null
                    || renderer.sprite == null
                    || !renderer.enabled
                    || renderer.gameObject == null
                    || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                ScenarioAuthoringTarget target;
                if (_selectionService == null
                    || !_selectionService.TryCreateTarget(renderer.gameObject, out target)
                    || target == null
                    || target.Kind != ScenarioAuthoringTargetKind.Background
                    || !paths.Add(target.TransformPath))
                {
                    continue;
                }

                targets.Add(target);
            }

            targets.Sort(CompareTargets);
            _cachedTargets.Clear();
            for (int i = 0; i < targets.Count; i++)
                _cachedTargets.Add(targets[i].Copy());
            _cachedFrame = Time.frameCount;
            _cachedScene = scene;
            return CopyTargets(_cachedTargets);
        }

        private static List<ScenarioAuthoringTarget> CopyTargets(List<ScenarioAuthoringTarget> targets)
        {
            List<ScenarioAuthoringTarget> copy = new List<ScenarioAuthoringTarget>();
            for (int i = 0; targets != null && i < targets.Count; i++)
            {
                if (targets[i] != null)
                    copy.Add(targets[i].Copy());
            }
            return copy;
        }

        private static int CompareTargets(ScenarioAuthoringTarget left, ScenarioAuthoringTarget right)
        {
            int name = string.Compare(
                left != null ? left.DisplayName : null,
                right != null ? right.DisplayName : null,
                StringComparison.OrdinalIgnoreCase);
            if (name != 0)
                return name;

            return string.Compare(
                left != null ? left.TransformPath : null,
                right != null ? right.TransformPath : null,
                StringComparison.OrdinalIgnoreCase);
        }

    }
}
