using System;
using System.Collections.Generic;
using ModAPI.Scenarios;
using UnityEngine;
using UnityEngine.SceneManagement;

using ShelteredAPI.Hooks;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Definitions;
namespace ShelteredAPI.Scenarios.Infrastructure.Assets{
    internal sealed class ScenarioSpriteRuntimeResolver
    {
        private static readonly Dictionary<string, Transform> ExternalRootCache =
            new Dictionary<string, Transform>(StringComparer.Ordinal);

        internal sealed class ResolvedTarget
        {
            public string TargetPath;
            public Transform Transform;
            public ScenarioSpriteTargetComponentKind Kind;
            public SpriteRenderer SpriteRenderer;
            public UI2DSprite Ui2DSprite;
            public ParticleSystemRenderer ParticleRenderer;

            public Sprite CurrentSprite
            {
                get
                {
                    if (Kind == ScenarioSpriteTargetComponentKind.SpriteRenderer && SpriteRenderer != null)
                        return SpriteRenderer.sprite;
                    if (Kind == ScenarioSpriteTargetComponentKind.UI2DSprite && Ui2DSprite != null)
                        return Ui2DSprite.sprite2D;
                    if (Kind == ScenarioSpriteTargetComponentKind.ParticleSystemRenderer && ParticleRenderer != null)
                    {
                        Material material = ParticleRenderer.material != null ? ParticleRenderer.material : ParticleRenderer.sharedMaterial;
                        Texture2D texture = material != null ? material.mainTexture as Texture2D : null;
                        return ScenarioSpriteReferenceLibrary.CreateFullTextureSprite(texture, texture != null ? texture.name : null);
                    }
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

            // TransformPath is the durable authoring identity. RuntimeObject can
            // be a transient hit/renderer proxy below that logical target, so it
            // is only a fallback when the persisted path is not currently live.
            Transform transform = FindTransformByPath(targetPath);
            if (transform == null)
            {
                Transform runtimeTransform = ResolveTransform(authoringTarget);
                Transform current = runtimeTransform;
                while (current != null)
                {
                    if (string.Equals(BuildTransformPath(current), targetPath, StringComparison.Ordinal))
                    {
                        transform = current;
                        break;
                    }
                    current = current.parent;
                }

                if (transform == null)
                    transform = runtimeTransform;
            }

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
            if (target.Kind == ScenarioSpriteTargetComponentKind.ParticleSystemRenderer)
                return target.ParticleRenderer != null;
            return false;
        }

        private static ResolvedTarget CreateResolvedTarget(Transform transform, string targetPath, ScenarioSpriteTargetComponentKind preferredKind)
        {
            if (transform == null)
                return null;

            SpriteRenderer spriteRenderer = null;
            UI2DSprite ui2DSprite = null;
            ParticleSystemRenderer particleRenderer = null;

            if (preferredKind == ScenarioSpriteTargetComponentKind.Auto || preferredKind == ScenarioSpriteTargetComponentKind.SpriteRenderer)
            {
                spriteRenderer = transform.GetComponent<SpriteRenderer>();
                if (spriteRenderer == null)
                {
                    SpriteRenderer[] candidates = transform.GetComponentsInChildren<SpriteRenderer>(true);
                    float largestActiveArea = -1f;
                    int nearestActiveDepth = int.MaxValue;
                    for (int i = 0; candidates != null && i < candidates.Length; i++)
                    {
                        SpriteRenderer candidate = candidates[i];
                        if (candidate == null || !candidate.gameObject.activeInHierarchy || candidate.sprite == null)
                            continue;

                        int candidateDepth = 0;
                        Transform candidateParent = candidate.transform;
                        while (candidateParent != null && candidateParent != transform)
                        {
                            candidateDepth++;
                            candidateParent = candidateParent.parent;
                        }
                        if (candidateParent == null)
                            continue;

                        Bounds candidateBounds = candidate.bounds;
                        float candidateArea = Mathf.Abs(candidateBounds.size.x * candidateBounds.size.y);
                        if (spriteRenderer == null
                            || candidateDepth < nearestActiveDepth
                            || (candidateDepth == nearestActiveDepth && candidateArea > largestActiveArea))
                        {
                            spriteRenderer = candidate;
                            largestActiveArea = candidateArea;
                            nearestActiveDepth = candidateDepth;
                        }
                    }

                    if (spriteRenderer == null && candidates != null && candidates.Length > 0)
                        spriteRenderer = candidates[0];
                }
            }
            if (preferredKind == ScenarioSpriteTargetComponentKind.Auto || preferredKind == ScenarioSpriteTargetComponentKind.UI2DSprite)
                ui2DSprite = transform.GetComponent<UI2DSprite>() ?? transform.GetComponentInChildren<UI2DSprite>(true);
            if (preferredKind == ScenarioSpriteTargetComponentKind.Auto || preferredKind == ScenarioSpriteTargetComponentKind.ParticleSystemRenderer)
                particleRenderer = transform.GetComponent<ParticleSystemRenderer>() ?? transform.GetComponentInChildren<ParticleSystemRenderer>(true);

            if (spriteRenderer != null)
            {
                return new ResolvedTarget
                {
                    TargetPath = BuildTransformPath(spriteRenderer.transform),
                    Transform = spriteRenderer.transform,
                    Kind = ScenarioSpriteTargetComponentKind.SpriteRenderer,
                    SpriteRenderer = spriteRenderer
                };
            }

            if (ui2DSprite != null)
            {
                return new ResolvedTarget
                {
                    TargetPath = BuildTransformPath(ui2DSprite.transform),
                    Transform = ui2DSprite.transform,
                    Kind = ScenarioSpriteTargetComponentKind.UI2DSprite,
                    Ui2DSprite = ui2DSprite
                };
            }

            if (particleRenderer != null)
            {
                return new ResolvedTarget
                {
                    TargetPath = BuildTransformPath(particleRenderer.transform),
                    Transform = particleRenderer.transform,
                    Kind = ScenarioSpriteTargetComponentKind.ParticleSystemRenderer,
                    ParticleRenderer = particleRenderer
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

            // Sheltered keeps some live world roots outside the active scene
            // (notably object_manager during authoring reloads). Locate each
            // external root once, then traverse its children without repeating
            // a global object/path scan for every authoring document build.
            Transform externalRoot;
            if (!ExternalRootCache.TryGetValue(segments[0], out externalRoot) || externalRoot == null)
            {
                externalRoot = null;
                Transform[] loadedTransforms = Resources.FindObjectsOfTypeAll<Transform>();
                for (int i = 0; loadedTransforms != null && i < loadedTransforms.Length; i++)
                {
                    Transform candidate = loadedTransforms[i];
                    if (candidate == null
                        || candidate.parent != null
                        || !string.Equals(candidate.name, segments[0], StringComparison.Ordinal))
                    {
                        continue;
                    }

                    externalRoot = candidate;
                    if (candidate.gameObject.activeInHierarchy)
                        break;
                }

                ExternalRootCache[segments[0]] = externalRoot;
            }

            if (externalRoot != null)
            {
                Transform current = externalRoot;
                for (int segmentIndex = 1; segmentIndex < segments.Length && current != null; segmentIndex++)
                    current = FindChildByName(current, segments[segmentIndex]);
                if (current != null)
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
