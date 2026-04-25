using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioTargetClassifier
    {
        public ScenarioTargetClassification Classify(ScenarioAuthoringTarget target)
        {
            if (target == null)
                return Unknown("No target.", "fallback");

            GameObject gameObject = ResolveGameObject(target);
            string path = (target.TransformPath ?? string.Empty).ToLowerInvariant();
            string name = ((target.GameObjectName ?? target.DisplayName) ?? string.Empty).ToLowerInvariant();
            string adapter = (target.AdapterId ?? string.Empty).ToLowerInvariant();
            string combined = path + " " + name + " " + adapter;

            ScenarioTargetClassification componentClassification = ClassifyByComponents(target, gameObject, combined);
            if (componentClassification != null)
                return componentClassification;

            ScenarioTargetClassification kindClassification = ClassifyByKind(target, combined);
            if (kindClassification != null)
                return kindClassification;

            ScenarioTargetClassification layerClassification = ClassifyByRenderer(gameObject, combined);
            if (layerClassification != null)
                return layerClassification;

            ScenarioTargetClassification pathClassification = ClassifyByText(combined, "transform path");
            if (pathClassification != null)
                return pathClassification;

            return Unknown("No reliable scope metadata matched this target.", "fallback");
        }

        public string FormatScopeLabel(ScenarioTargetClassification classification)
        {
            if (classification == null)
                return "Unknown";

            string label = FormatScopeLabel(classification.PrimaryScope);
            if (classification.SecondaryScopes == null || classification.SecondaryScopes.Length == 0)
                return label;

            List<string> secondary = new List<string>();
            for (int i = 0; i < classification.SecondaryScopes.Length; i++)
            {
                if (classification.SecondaryScopes[i] != ScenarioTargetScope.Unknown)
                    secondary.Add(FormatScopeLabel(classification.SecondaryScopes[i]));
            }

            return secondary.Count > 0 ? label + " + " + string.Join(", ", secondary.ToArray()) : label;
        }

        public static string FormatScopeLabel(ScenarioTargetScope scope)
        {
            switch (scope)
            {
                case ScenarioTargetScope.BunkerBackground: return "Background";
                case ScenarioTargetScope.BunkerSurface: return "Surface";
                case ScenarioTargetScope.BunkerInside: return "Inside";
                case ScenarioTargetScope.Inventory: return "Inventory";
                case ScenarioTargetScope.People: return "People";
                case ScenarioTargetScope.Events: return "Events";
                case ScenarioTargetScope.Quests: return "Quests";
                case ScenarioTargetScope.Map: return "Map";
                default: return "Unknown";
            }
        }

        private static ScenarioTargetClassification ClassifyByComponents(
            ScenarioAuthoringTarget target,
            GameObject gameObject,
            string combined)
        {
            if (gameObject == null)
                return null;

            if (gameObject.GetComponentInParent<FamilyMember>() != null
                || gameObject.GetComponentInParent<NpcVisitor>() != null
                || gameObject.GetComponentInParent<BaseCharacter>() != null)
            {
                return Create(
                    ScenarioTargetScope.People,
                    0.95f,
                    "Character component hierarchy.",
                    "component",
                    ScenarioTargetScope.BunkerInside);
            }

            Obj_Base obj = gameObject.GetComponentInParent<Obj_Base>();
            if (obj != null)
            {
                if (ScenarioTargetScopeTextMatcher.ContainsSurfaceToken(combined))
                    return Create(ScenarioTargetScope.BunkerSurface, 0.9f, "Shelter object on a surface/exterior path.", "component");

                return Create(ScenarioTargetScope.BunkerInside, 0.9f, "Shelter object component.", "component");
            }

            ScenarioSceneSpritePlacementMarker marker = gameObject.GetComponentInParent<ScenarioSceneSpritePlacementMarker>();
            if (marker != null)
            {
                ScenarioTargetClassification text = ClassifyByText(combined, "authored placement metadata");
                return text ?? Create(ScenarioTargetScope.BunkerInside, 0.62f, "Authored scene sprite placement without stronger layer metadata.", "component");
            }

            Component[] components = gameObject.GetComponents<Component>();
            for (int i = 0; components != null && i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null)
                    continue;

                string typeName = component.GetType().Name.ToLowerInvariant();
                if (ScenarioTargetScopeTextMatcher.ContainsBackgroundToken(typeName))
                    return Create(ScenarioTargetScope.BunkerBackground, 0.82f, "Background-like component type " + component.GetType().Name + ".", "component");
                if (ScenarioTargetScopeTextMatcher.ContainsSurfaceToken(typeName))
                    return Create(ScenarioTargetScope.BunkerSurface, 0.8f, "Surface-like component type " + component.GetType().Name + ".", "component");
                if (ScenarioTargetScopeTextMatcher.ContainsInteriorToken(typeName))
                    return Create(ScenarioTargetScope.BunkerInside, 0.8f, "Interior-like component type " + component.GetType().Name + ".", "component");
            }

            return null;
        }

        private static ScenarioTargetClassification ClassifyByKind(ScenarioAuthoringTarget target, string combined)
        {
            switch (target.Kind)
            {
                case ScenarioAuthoringTargetKind.Character:
                    return Create(ScenarioTargetScope.People, 0.9f, "Target kind is character.", "object kind", ScenarioTargetScope.BunkerInside);
                case ScenarioAuthoringTargetKind.Room:
                case ScenarioAuthoringTargetKind.Tile:
                    return Create(ScenarioTargetScope.BunkerInside, 0.84f, "Target kind is an interior bunker structure.", "object kind");
                case ScenarioAuthoringTargetKind.PlaceableObject:
                    if (ScenarioTargetScopeTextMatcher.ContainsSurfaceToken(combined))
                        return Create(ScenarioTargetScope.BunkerSurface, 0.78f, "Placeable target has surface/exterior metadata.", "object kind");
                    return Create(ScenarioTargetScope.BunkerInside, 0.76f, "Placeable shelter object defaults to inside scope.", "object kind");
                case ScenarioAuthoringTargetKind.Wall:
                case ScenarioAuthoringTargetKind.Wire:
                case ScenarioAuthoringTargetKind.Light:
                    if (ScenarioTargetScopeTextMatcher.ContainsBackgroundToken(combined))
                        return Create(ScenarioTargetScope.BunkerBackground, 0.76f, "Non-interactive wall visual path.", "object kind");
                    return Create(ScenarioTargetScope.BunkerInside, 0.72f, "Interior wall/wiring/light target.", "object kind");
                case ScenarioAuthoringTargetKind.Vehicle:
                    return Create(ScenarioTargetScope.BunkerSurface, 0.72f, "Vehicle/exterior object kind.", "object kind", ScenarioTargetScope.BunkerInside);
                case ScenarioAuthoringTargetKind.Background:
                    return Create(ScenarioTargetScope.BunkerBackground, 0.86f, "Target kind is background.", "object kind");
                case ScenarioAuthoringTargetKind.SceneSprite:
                    return ClassifyByText(combined, "transform path")
                        ?? Create(ScenarioTargetScope.BunkerInside, 0.58f, "Scene sprite fallback defaults to interior until metadata is authored.", "fallback");
                default:
                    return null;
            }
        }

        private static ScenarioTargetClassification ClassifyByRenderer(GameObject gameObject, string combined)
        {
            if (gameObject == null)
                return null;

            SpriteRenderer renderer = gameObject.GetComponent<SpriteRenderer>() ?? gameObject.GetComponentInChildren<SpriteRenderer>(true);
            if (renderer == null)
                return null;

            string layerName = SortingLayer.IDToName(renderer.sortingLayerID);
            string layerText = ((layerName ?? string.Empty) + " " + combined).ToLowerInvariant();
            if (renderer.sortingOrder < 0 || ScenarioTargetScopeTextMatcher.ContainsBackgroundToken(layerText))
                return Create(ScenarioTargetScope.BunkerBackground, 0.74f, "Sprite sorting layer/order looks like backdrop art.", "sprite layer");

            if (ScenarioTargetScopeTextMatcher.ContainsSurfaceToken(layerText))
                return Create(ScenarioTargetScope.BunkerSurface, 0.68f, "Sprite sorting layer/name looks like surface art.", "sprite layer");

            return null;
        }

        private static ScenarioTargetClassification ClassifyByText(string value, string source)
        {
            ScenarioTargetScope scope = ScenarioTargetScopeTextMatcher.MatchBunkerScope(value);
            switch (scope)
            {
                case ScenarioTargetScope.BunkerBackground:
                    return Create(scope, 0.62f, "Path/name matches background scope terms.", source);
                case ScenarioTargetScope.BunkerSurface:
                    return Create(scope, 0.62f, "Path/name matches surface scope terms.", source);
                case ScenarioTargetScope.BunkerInside:
                    return Create(scope, 0.6f, "Path/name matches interior scope terms.", source);
                default:
                    return null;
            }
        }

        private static ScenarioTargetClassification Create(
            ScenarioTargetScope primary,
            float confidence,
            string reason,
            string source,
            params ScenarioTargetScope[] secondary)
        {
            return new ScenarioTargetClassification
            {
                PrimaryScope = primary,
                SecondaryScopes = secondary ?? new ScenarioTargetScope[0],
                Confidence = confidence,
                Reason = reason,
                Source = source
            };
        }

        private static ScenarioTargetClassification Unknown(string reason, string source)
        {
            return Create(ScenarioTargetScope.Unknown, 0f, reason, source);
        }

        private static GameObject ResolveGameObject(ScenarioAuthoringTarget target)
        {
            if (target == null || target.RuntimeObject == null)
                return null;

            GameObject gameObject = target.RuntimeObject as GameObject;
            if (gameObject != null)
                return gameObject;

            Component component = target.RuntimeObject as Component;
            return component != null ? component.gameObject : null;
        }

    }
}
