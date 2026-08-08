using System;
using UnityEngine;

using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Public;
using ShelteredScenarioEditor.Application.Authoring;

namespace ShelteredScenarioEditor.Infrastructure.Assets
{
    /// <summary>
    /// Adds the editor's transient selection fallback to the shared runtime-target
    /// resolver. Component selection and path traversal remain single-owned.
    /// </summary>
    internal sealed class ScenarioEditorSpriteRuntimeResolver
    {
        public bool TryResolve(
            string targetPath,
            ScenarioSpriteTargetComponentKind preferredKind,
            out ScenarioRuntimeSpriteTarget target)
        {
            return ShelteredScenarioRuntime.TryResolveRuntimeSpriteTarget(targetPath, preferredKind, out target);
        }

        public bool TryResolve(ScenarioAuthoringTarget authoringTarget, out ScenarioRuntimeSpriteTarget target)
        {
            target = null;
            if (authoringTarget == null)
                return false;

            string targetPath = ResolveTargetPath(authoringTarget);
            if (string.IsNullOrEmpty(targetPath))
                return false;

            // TransformPath is the durable authoring identity. RuntimeObject can be
            // a transient child hit proxy, so use it only when the persisted path is
            // not currently available and reconcile it back through its ancestors.
            if (ShelteredScenarioRuntime.TryResolveRuntimeSpriteTarget(
                targetPath,
                ScenarioSpriteTargetComponentKind.Auto,
                out target))
            {
                return true;
            }

            Transform runtimeTransform = ResolveTransform(authoringTarget);
            Transform transform = null;
            Transform current = runtimeTransform;
            while (current != null)
            {
                if (string.Equals(ShelteredScenarioRuntime.GetTransformPath(current), targetPath, StringComparison.Ordinal))
                {
                    transform = current;
                    break;
                }
                current = current.parent;
            }
            if (transform == null)
                transform = runtimeTransform;
            if (transform == null)
                return false;

            return ShelteredScenarioRuntime.TryResolveRuntimeSpriteTarget(
                transform,
                ScenarioSpriteTargetComponentKind.Auto,
                out target);
        }

        internal static bool IsAlive(ScenarioRuntimeSpriteTarget target)
        {
            return target != null && target.IsAlive;
        }

        private static Transform ResolveTransform(ScenarioAuthoringTarget authoringTarget)
        {
            if (authoringTarget == null || authoringTarget.RuntimeObject == null)
                return null;

            GameObject gameObject = authoringTarget.RuntimeObject as GameObject;
            if (gameObject != null)
                return gameObject.transform;

            Component component = authoringTarget.RuntimeObject as Component;
            return component != null ? component.transform : null;
        }

        private static string ResolveTargetPath(ScenarioAuthoringTarget authoringTarget)
        {
            if (authoringTarget == null)
                return null;
            if (!string.IsNullOrEmpty(authoringTarget.TransformPath))
                return authoringTarget.TransformPath;
            return ShelteredScenarioRuntime.GetTransformPath(ResolveTransform(authoringTarget));
        }
    }
}
