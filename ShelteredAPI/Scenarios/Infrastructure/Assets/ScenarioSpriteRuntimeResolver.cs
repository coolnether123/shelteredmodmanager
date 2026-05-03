using System;
using ModAPI.Scenarios;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioSpriteRuntimeResolver
    {
        internal sealed class ResolvedTarget
        {
            public string TargetPath;
            public Transform Transform;
            public ScenarioSpriteTargetComponentKind Kind;
            public SpriteRenderer SpriteRenderer;
            public UI2DSprite Ui2DSprite;

            public Sprite CurrentSprite
            {
                get
                {
                    if (Kind == ScenarioSpriteTargetComponentKind.SpriteRenderer && SpriteRenderer != null)
                        return SpriteRenderer.sprite;
                    if (Kind == ScenarioSpriteTargetComponentKind.UI2DSprite && Ui2DSprite != null)
                        return Ui2DSprite.sprite2D;
                    return null;
                }
            }

            public string SpriteName
            {
                get
                {
                    Sprite sprite = CurrentSprite;
                    return sprite != null && !string.IsNullOrEmpty(sprite.name) ? sprite.name : null;
                }
            }

            public string TextureName
            {
                get
                {
                    Sprite sprite = CurrentSprite;
                    Texture2D texture = sprite != null ? sprite.texture : null;
                    return texture != null && !string.IsNullOrEmpty(texture.name) ? texture.name : null;
                }
            }
        }

        public bool TryResolve(string targetPath, ScenarioSpriteTargetComponentKind preferredKind, out ResolvedTarget target)
        {
            target = null;
            if (string.IsNullOrEmpty(targetPath))
                return false;

            Transform transform = FindTransformByPath(targetPath);
            if (transform == null)
                return false;

            target = CreateResolvedTarget(transform, targetPath, preferredKind);
            return target != null;
        }

        public bool TryResolve(ScenarioAuthoringTarget authoringTarget, out ResolvedTarget target)
        {
            target = null;
            if (authoringTarget == null)
                return false;

            string targetPath = ResolveTargetPath(authoringTarget);
            if (string.IsNullOrEmpty(targetPath))
                return false;

            Transform transform = ResolveTransform(authoringTarget);
            if (transform == null)
                transform = FindTransformByPath(targetPath);

            if (transform == null)
                return false;

            target = CreateResolvedTarget(transform, targetPath, ScenarioSpriteTargetComponentKind.Auto);
            return target != null;
        }

        internal static bool IsAlive(ResolvedTarget target)
        {
            if (target == null)
                return false;

            if (target.Kind == ScenarioSpriteTargetComponentKind.SpriteRenderer)
                return target.SpriteRenderer != null;
            if (target.Kind == ScenarioSpriteTargetComponentKind.UI2DSprite)
                return target.Ui2DSprite != null;
            return false;
        }

        private static ResolvedTarget CreateResolvedTarget(Transform transform, string targetPath, ScenarioSpriteTargetComponentKind preferredKind)
        {
            if (transform == null)
                return null;

            SpriteRenderer spriteRenderer = null;
            UI2DSprite ui2DSprite = null;

            if (preferredKind == ScenarioSpriteTargetComponentKind.Auto || preferredKind == ScenarioSpriteTargetComponentKind.SpriteRenderer)
                spriteRenderer = transform.GetComponent<SpriteRenderer>() ?? transform.GetComponentInChildren<SpriteRenderer>(true);
            if (preferredKind == ScenarioSpriteTargetComponentKind.Auto || preferredKind == ScenarioSpriteTargetComponentKind.UI2DSprite)
                ui2DSprite = transform.GetComponent<UI2DSprite>() ?? transform.GetComponentInChildren<UI2DSprite>(true);

            if (spriteRenderer != null)
            {
                return new ResolvedTarget
                {
                    TargetPath = targetPath,
                    Transform = transform,
                    Kind = ScenarioSpriteTargetComponentKind.SpriteRenderer,
                    SpriteRenderer = spriteRenderer
                };
            }

            if (ui2DSprite != null)
            {
                return new ResolvedTarget
                {
                    TargetPath = targetPath,
                    Transform = transform,
                    Kind = ScenarioSpriteTargetComponentKind.UI2DSprite,
                    Ui2DSprite = ui2DSprite
                };
            }

            return null;
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

            Transform transform = ResolveTransform(authoringTarget);
            return BuildTransformPath(transform);
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

        private static string BuildTransformPath(Transform transform)
        {
            if (transform == null)
                return null;

            System.Collections.Generic.List<string> names = new System.Collections.Generic.List<string>();
            Transform current = transform;
            while (current != null)
            {
                names.Add(current.name);
                current = current.parent;
            }

            names.Reverse();
            return string.Join("/", names.ToArray());
        }
    }
}
