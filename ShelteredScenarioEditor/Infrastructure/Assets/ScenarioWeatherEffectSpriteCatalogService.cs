using ShelteredAPI.Scenarios.Public;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

using ShelteredScenarioEditor.Application.Authoring;
using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredScenarioEditor.Infrastructure.Assets{
    internal sealed class ScenarioWeatherEffectSpriteCatalogService
    {
        public const string AdapterId = "ShelteredAPI.WeatherEffects";

        private static readonly FieldInfo WeatherParticlesInspectorField =
            typeof(WeatherManager).GetField("weatherParticlesInspector", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly string[] EncounterWeatherFields = new[]
        {
            "acidRain",
            "blackRain",
            "SandStormLight",
            "SandStormMedium",
            "SandStormHeavy"
        };

        internal sealed class WeatherEffectSpriteTarget
        {
            public ScenarioAuthoringTarget Target;
            public string Group;
            public string Source;
            public string TextureName;
            public Sprite PreviewSprite;
            public Color PreviewTint;
            public bool HasPreviewTint;
        }

        public List<WeatherEffectSpriteTarget> GetTargets()
        {
            Dictionary<string, WeatherEffectSpriteTarget> byPath = new Dictionary<string, WeatherEffectSpriteTarget>(StringComparer.OrdinalIgnoreCase);
            AddWeatherManagerTargets(byPath);
            AddEncounterWeatherTargets(byPath);
            AddCloudTargets(byPath);

            List<WeatherEffectSpriteTarget> targets = new List<WeatherEffectSpriteTarget>(byPath.Values);
            targets.Sort(CompareTargets);
            return targets;
        }

        public bool TryFindTarget(string targetId, out ScenarioAuthoringTarget target)
        {
            target = null;
            if (string.IsNullOrEmpty(targetId))
                return false;

            List<WeatherEffectSpriteTarget> targets = GetTargets();
            for (int i = 0; targets != null && i < targets.Count; i++)
            {
                WeatherEffectSpriteTarget entry = targets[i];
                if (entry != null
                    && entry.Target != null
                    && string.Equals(entry.Target.Id, targetId, StringComparison.OrdinalIgnoreCase))
                {
                    target = entry.Target.Copy();
                    return true;
                }
            }

            return false;
        }

        public static bool IsWeatherEffectTarget(ScenarioAuthoringTarget target)
        {
            return target != null && string.Equals(target.AdapterId, AdapterId, StringComparison.OrdinalIgnoreCase);
        }

        private static void AddWeatherManagerTargets(Dictionary<string, WeatherEffectSpriteTarget> byPath)
        {
            WeatherManager manager = WeatherManager.Instance;
            IEnumerable weatherParticles = manager != null && WeatherParticlesInspectorField != null
                ? WeatherParticlesInspectorField.GetValue(manager) as IEnumerable
                : null;
            if (weatherParticles == null)
                return;

            foreach (object entry in weatherParticles)
            {
                WeatherManager.WeatherParticle weatherParticle = entry as WeatherManager.WeatherParticle;
                if (weatherParticle == null || weatherParticle.particle == null)
                    continue;

                AddParticleRendererTargets(
                    byPath,
                    weatherParticle.particle.gameObject,
                    "Shelter Weather",
                    weatherParticle.weather.ToString(),
                    "WeatherManager.WeatherParticle." + weatherParticle.weather);
            }
        }

        private static void AddEncounterWeatherTargets(Dictionary<string, WeatherEffectSpriteTarget> byPath)
        {
            EncounterMainPanel[] panels = Resources.FindObjectsOfTypeAll<EncounterMainPanel>();
            for (int panelIndex = 0; panels != null && panelIndex < panels.Length; panelIndex++)
            {
                EncounterMainPanel panel = panels[panelIndex];
                if (panel == null)
                    continue;

                Type type = panel.GetType();
                for (int fieldIndex = 0; fieldIndex < EncounterWeatherFields.Length; fieldIndex++)
                {
                    string fieldName = EncounterWeatherFields[fieldIndex];
                    FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                    GameObject root = field != null ? field.GetValue(panel) as GameObject : null;
                    if (root == null)
                        continue;

                    AddParticleRendererTargets(
                        byPath,
                        root,
                        "Encounter Weather",
                        FriendlyEffectName(fieldName),
                        "EncounterMainPanel." + fieldName);
                }
            }
        }

        private static void AddCloudTargets(Dictionary<string, WeatherEffectSpriteTarget> byPath)
        {
            CloudPrefabSprite[] clouds = Resources.FindObjectsOfTypeAll<CloudPrefabSprite>();
            for (int i = 0; clouds != null && i < clouds.Length; i++)
            {
                CloudPrefabSprite cloud = clouds[i];
                SpriteRenderer renderer = cloud != null ? cloud.GetComponent<SpriteRenderer>() : null;
                if (renderer == null || renderer.sprite == null)
                    continue;

                AddSpriteRendererTarget(
                    byPath,
                    renderer,
                    "Clouds",
                    "Cloud",
                    "CloudPrefabSprite.SpriteRenderer");
            }
        }

        private static void AddParticleRendererTargets(
            Dictionary<string, WeatherEffectSpriteTarget> byPath,
            GameObject root,
            string group,
            string effectName,
            string source)
        {
            ParticleSystemRenderer[] renderers = root != null ? root.GetComponentsInChildren<ParticleSystemRenderer>(true) : null;
            for (int i = 0; renderers != null && i < renderers.Length; i++)
            {
                ParticleSystemRenderer renderer = renderers[i];
                Material material = renderer != null ? renderer.sharedMaterial : null;
                Texture2D texture = material != null ? material.mainTexture as Texture2D : null;
                if (texture == null)
                    continue;

                Sprite preview = ShelteredScenarioRuntime.CreateAndRegisterRuntimeSprite(texture, texture.name);
                Color tint;
                bool hasTint = TryResolveParticleTint(renderer, material, out tint);
                AddTarget(
                    byPath,
                    renderer.transform,
                    group,
                    effectName,
                    source,
                    ScenarioSpriteTargetComponentKind.ParticleSystemRenderer,
                    preview,
                    !string.IsNullOrEmpty(texture.name) ? texture.name : "<particle texture>",
                    tint,
                    hasTint);
            }
        }

        private static void AddSpriteRendererTarget(
            Dictionary<string, WeatherEffectSpriteTarget> byPath,
            SpriteRenderer renderer,
            string group,
            string effectName,
            string source)
        {
            Color tint;
            bool hasTint = TryResolveSpriteTint(renderer, out tint);
            AddTarget(
                byPath,
                renderer != null ? renderer.transform : null,
                group,
                effectName,
                source,
                ScenarioSpriteTargetComponentKind.SpriteRenderer,
                renderer != null ? renderer.sprite : null,
                renderer != null && renderer.sprite != null && renderer.sprite.texture != null ? renderer.sprite.texture.name : null,
                tint,
                hasTint);
        }

        private static void AddTarget(
            Dictionary<string, WeatherEffectSpriteTarget> byPath,
            Transform transform,
            string group,
            string effectName,
            string source,
            ScenarioSpriteTargetComponentKind componentKind,
            Sprite preview,
            string textureName,
            Color previewTint,
            bool hasPreviewTint)
        {
            if (byPath == null || transform == null || preview == null)
                return;

            string path = ShelteredScenarioRuntime.GetTransformPath(transform);
            if (string.IsNullOrEmpty(path) || byPath.ContainsKey(path))
                return;

            string targetId = "weather_effect:" + transform.GetInstanceID();
            string display = effectName + " - " + (!string.IsNullOrEmpty(transform.name) ? transform.name : componentKind.ToString());
            byPath[path] = new WeatherEffectSpriteTarget
            {
                Group = group,
                Source = source,
                TextureName = textureName,
                PreviewSprite = preview,
                PreviewTint = previewTint,
                HasPreviewTint = hasPreviewTint,
                Target = new ScenarioAuthoringTarget
                {
                    Id = targetId,
                    Kind = ScenarioAuthoringTargetKind.Background,
                    DisplayName = display,
                    Description = source + " material texture at " + path,
                    AdapterId = AdapterId,
                    GameObjectName = transform.name,
                    TransformPath = path,
                    RuntimeObject = transform.gameObject,
                    HighlightObject = transform.gameObject,
                    WorldPosition = transform.position,
                    SupportsInspect = true,
                    SupportsReplace = true
                }
            };
        }

        private static bool TryResolveParticleTint(ParticleSystemRenderer renderer, Material material, out Color tint)
        {
            tint = Color.white;
            bool found = TryResolveMaterialTint(material, out tint);
            ParticleSystem particles = renderer != null ? renderer.GetComponent<ParticleSystem>() : null;
            if (particles != null)
            {
#pragma warning disable 0618 // Sheltered's Unity compatibility floor predates ParticleSystem.main.
                tint = Multiply(tint, particles.startColor);
#pragma warning restore 0618
                found = true;
            }
            return found;
        }

        private static bool TryResolveSpriteTint(SpriteRenderer renderer, out Color tint)
        {
            tint = Color.white;
            if (renderer == null)
                return false;

            Color materialTint;
            bool foundMaterial = TryResolveMaterialTint(renderer.sharedMaterial, out materialTint);
            tint = Multiply(renderer.color, foundMaterial ? materialTint : Color.white);
            return foundMaterial || renderer.color != Color.white;
        }

        private static bool TryResolveMaterialTint(Material material, out Color tint)
        {
            tint = Color.white;
            if (material == null)
                return false;

            string[] colorProperties = { "_TintColor", "_Color" };
            for (int i = 0; i < colorProperties.Length; i++)
            {
                if (!material.HasProperty(colorProperties[i]))
                    continue;
                tint = material.GetColor(colorProperties[i]);
                return true;
            }
            return false;
        }

        private static Color Multiply(Color left, Color right)
        {
            return new Color(left.r * right.r, left.g * right.g, left.b * right.b, left.a * right.a);
        }


        private static string FriendlyEffectName(string fieldName)
        {
            switch (fieldName)
            {
                case "acidRain": return "Rain";
                case "blackRain": return "Black Rain";
                case "SandStormLight": return "Light Sand";
                case "SandStormMedium": return "Medium Sand";
                case "SandStormHeavy": return "Heavy Sand";
                default: return fieldName;
            }
        }

        private static int CompareTargets(WeatherEffectSpriteTarget left, WeatherEffectSpriteTarget right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return 1;
            if (right == null) return -1;

            int group = string.Compare(left.Group, right.Group, StringComparison.OrdinalIgnoreCase);
            if (group != 0) return group;

            string leftLabel = left.Target != null ? left.Target.DisplayName : null;
            string rightLabel = right.Target != null ? right.Target.DisplayName : null;
            return string.Compare(leftLabel, rightLabel, StringComparison.OrdinalIgnoreCase);
        }
    }
}
