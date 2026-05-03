using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioSpritePlacementPolicy
    {
        public ScenarioPlaceableAssetClassification ClassifyRuntimeSprite(ScenarioSpriteReferenceLibrary.LoadedSpriteReference reference)
        {
            string text = BuildSearchText(
                reference != null ? reference.RuntimeSpriteKey : null,
                reference != null ? reference.SpriteName : null,
                reference != null ? reference.TextureName : null,
                reference != null && reference.Sprite != null && reference.Sprite.texture != null ? reference.Sprite.texture.name : null);

            if (ContainsAny(text, "survivor", "character", "char_", "family", "npc", "raider", "merchant", "trader", "_head", "head_", "_torso", "torso_", "_legs", "legs_", "_hair", "hair_"))
                return Block(ScenarioPlaceableAssetKind.Person, "People must be authored through survivor/family setup so stats, traits, schedules, and AI bindings are preserved.");

            if (ContainsAny(text, "movement", "path", "roam", "wander", "agent", "pet", "worm", "horse", "deer", "bird"))
                return Block(ScenarioPlaceableAssetKind.PathfindingActor, "Pathfinding actors need spawned gameplay objects so movement, collision, and AI state are tracked.");

            if (ContainsAny(text, "workbench", "generator", "filter", "door", "ladder", "bed", "toilet", "shower", "stove", "radio", "clipboard", "computer", "switch", "storage", "pantry", "freezer", "locker", "craft", "object", "prefab", "vehicle", "van", "trap"))
                return Block(ScenarioPlaceableAssetKind.InteractiveObject, "Interactive assets must use object/structure placement so interactions, uses, support, and collision are inserted into the game.");

            if (ContainsAny(text, "inventory", "item", "weapon", "food", "water", "medicine", "quest", "condition", "icon", "ui_"))
                return Block(ScenarioPlaceableAssetKind.GameplayAsset, "Gameplay assets must be added through inventory, quest, schedule, or UI-specific authoring instead of visual scene placement.");

            return AllowVisualOnly();
        }

        public ScenarioPlaceableAssetClassification ClassifyCustomSprite(Sprite sprite)
        {
            return AllowVisualOnly();
        }

        private static ScenarioPlaceableAssetClassification AllowVisualOnly()
        {
            return new ScenarioPlaceableAssetClassification
            {
                Kind = ScenarioPlaceableAssetKind.VisualOnly,
                CanPlaceAsSceneSprite = true,
                Label = "Visual-only",
                Guidance = "This can be placed as scene dressing. It will not create gameplay interactions, pathfinding, inventory, or survivor state."
            };
        }

        private static ScenarioPlaceableAssetClassification Block(ScenarioPlaceableAssetKind kind, string guidance)
        {
            return new ScenarioPlaceableAssetClassification
            {
                Kind = kind,
                CanPlaceAsSceneSprite = false,
                Label = FormatKind(kind),
                Guidance = guidance
            };
        }

        private static string FormatKind(ScenarioPlaceableAssetKind kind)
        {
            switch (kind)
            {
                case ScenarioPlaceableAssetKind.Person:
                    return "Person";
                case ScenarioPlaceableAssetKind.InteractiveObject:
                    return "Interactive object";
                case ScenarioPlaceableAssetKind.PathfindingActor:
                    return "Pathfinding actor";
                case ScenarioPlaceableAssetKind.GameplayAsset:
                    return "Gameplay asset";
                default:
                    return "Visual-only";
            }
        }

        private static string BuildSearchText(params string[] parts)
        {
            if (parts == null)
                return string.Empty;

            string result = string.Empty;
            for (int i = 0; i < parts.Length; i++)
            {
                if (!string.IsNullOrEmpty(parts[i]))
                    result += " " + parts[i].ToLowerInvariant();
            }

            return result;
        }

        private static bool ContainsAny(string value, params string[] parts)
        {
            if (string.IsNullOrEmpty(value) || parts == null)
                return false;

            for (int i = 0; i < parts.Length; i++)
            {
                if (!string.IsNullOrEmpty(parts[i]) && value.IndexOf(parts[i], System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }
    }
}
