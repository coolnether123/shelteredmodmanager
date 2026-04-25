using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioSpriteFamilyMatcher
    {
        internal sealed class FamilyProfile
        {
            public string PrimaryKey;
            public string DisplayLabel;
            public string KindKey;
            public bool HasStrongIdentity;
            public HashSet<string> MatchKeys;
            public HashSet<string> Tokens;
        }

        private static readonly string[] StopWords = new[]
        {
            "obj", "object", "sprite", "sprites", "image", "images", "icon", "icons",
            "map", "atlas", "texture", "textures", "panel", "render", "renderer", "root",
            "gameobject", "clone", "variant", "level", "default", "runtime", "custom",
            "scenario", "initial", "spawned", "shelter", "scene", "item", "items",
            "background", "foreground", "room", "tile", "grid"
        };

        private static Dictionary<string, FamilyProfile> _cachedRuntimeProfiles;
        private static int _cachedRuntimeProfileFrame = -1;

        public FamilyProfile DescribeTarget(ScenarioAuthoringTarget authoringTarget, ScenarioSpriteRuntimeResolver.ResolvedTarget resolvedTarget)
        {
            FamilyProfile profile = BuildProfileFromTransform(
                resolvedTarget != null ? resolvedTarget.Transform : ResolveTransform(authoringTarget),
                resolvedTarget != null ? resolvedTarget.CurrentSprite : null,
                authoringTarget != null ? authoringTarget.Kind : ScenarioAuthoringTargetKind.Unknown);

            if (profile == null)
                profile = CreateEmptyProfile();

            if (authoringTarget != null)
            {
                AddNameTokens(profile, authoringTarget.DisplayName);
                AddNameTokens(profile, authoringTarget.GameObjectName);
                AddNameTokens(profile, authoringTarget.TransformPath);

                string kindKey = BuildKindKey(authoringTarget.Kind);
                if (!string.IsNullOrEmpty(kindKey))
                {
                    profile.KindKey = kindKey;
                    profile.MatchKeys.Add(kindKey);
                }
            }

            return profile;
        }

        public FamilyProfile DescribeRuntimeCandidate(string runtimeSpriteKey, Sprite sprite)
        {
            FamilyProfile profile = null;
            Dictionary<string, FamilyProfile> runtimeProfiles = GetRuntimeProfiles();
            if (!string.IsNullOrEmpty(runtimeSpriteKey))
                runtimeProfiles.TryGetValue(runtimeSpriteKey, out profile);

            if (profile != null)
                return CloneProfile(profile);

            FamilyProfile inferred = CreateEmptyProfile();
            AddSpriteTokens(inferred, sprite);
            return inferred;
        }

        public FamilyProfile DescribeCustomCandidate(string spriteId, string relativePath, Sprite sprite, string sourceName)
        {
            FamilyProfile profile = CreateEmptyProfile();
            AddNameTokens(profile, spriteId);
            AddNameTokens(profile, relativePath);
            AddNameTokens(profile, sourceName);
            AddSpriteTokens(profile, sprite);
            return profile;
        }

        public bool CanApplyFamilyFilter(FamilyProfile targetProfile)
        {
            return targetProfile != null
                && ((targetProfile.MatchKeys != null && targetProfile.MatchKeys.Count > 0)
                    || (targetProfile.Tokens != null && targetProfile.Tokens.Count > 0));
        }

        public bool HasVerifiedFamily(FamilyProfile profile)
        {
            if (profile == null)
                return false;

            if (!string.IsNullOrEmpty(profile.PrimaryKey) && profile.HasStrongIdentity)
                return true;

            return !string.IsNullOrEmpty(profile.KindKey);
        }

        public bool IsExactVerifiedMatch(FamilyProfile targetProfile, FamilyProfile candidateProfile)
        {
            if (targetProfile == null || candidateProfile == null)
                return false;

            if (!string.IsNullOrEmpty(targetProfile.PrimaryKey) && targetProfile.HasStrongIdentity)
                return string.Equals(targetProfile.PrimaryKey, candidateProfile.PrimaryKey, StringComparison.OrdinalIgnoreCase);

            if (!string.IsNullOrEmpty(targetProfile.KindKey))
                return string.Equals(targetProfile.KindKey, candidateProfile.KindKey, StringComparison.OrdinalIgnoreCase);

            return false;
        }

        public string DescribeVerifiedFamily(FamilyProfile profile)
        {
            if (profile == null)
                return "Unverified target";
            if (!string.IsNullOrEmpty(profile.DisplayLabel))
                return profile.DisplayLabel;
            if (!string.IsNullOrEmpty(profile.PrimaryKey))
                return profile.PrimaryKey;
            if (!string.IsNullOrEmpty(profile.KindKey))
                return profile.KindKey;
            return "Unverified target";
        }

        public bool IsMatch(FamilyProfile targetProfile, FamilyProfile candidateProfile)
        {
            if (targetProfile == null || candidateProfile == null)
                return false;

            if (SharesKey(targetProfile.MatchKeys, candidateProfile.MatchKeys))
                return true;

            if (!string.IsNullOrEmpty(targetProfile.KindKey)
                && candidateProfile.MatchKeys != null
                && candidateProfile.MatchKeys.Contains(targetProfile.KindKey))
            {
                return true;
            }

            int overlap = CountTokenOverlap(targetProfile.Tokens, candidateProfile.Tokens);
            if (overlap >= 2)
                return true;

            return overlap == 1 && HasDistinctiveSharedToken(targetProfile.Tokens, candidateProfile.Tokens);
        }

        public string DescribeFilter(FamilyProfile targetProfile)
        {
            if (targetProfile == null)
                return "Same-size sprites";

            if (!string.IsNullOrEmpty(targetProfile.DisplayLabel))
                return "Like-for-like in-game family: " + targetProfile.DisplayLabel;

            if (!string.IsNullOrEmpty(targetProfile.PrimaryKey))
                return "Like-for-like in-game family";

            return "Same-size sprites";
        }

        private static Dictionary<string, FamilyProfile> GetRuntimeProfiles()
        {
            if (_cachedRuntimeProfiles != null
                && (_cachedRuntimeProfileFrame < 0 || Time.frameCount - _cachedRuntimeProfileFrame < 120))
            {
                return _cachedRuntimeProfiles;
            }

            Dictionary<string, FamilyProfile> profiles = new Dictionary<string, FamilyProfile>(StringComparer.OrdinalIgnoreCase);

            Obj_Base[] objects = Resources.FindObjectsOfTypeAll<Obj_Base>();
            for (int i = 0; objects != null && i < objects.Length; i++)
            {
                Obj_Base obj = objects[i];
                if (obj == null)
                    continue;

                SpriteRenderer[] renderers = obj.GetComponentsInChildren<SpriteRenderer>(true);
                for (int renderIndex = 0; renderers != null && renderIndex < renderers.Length; renderIndex++)
                    RegisterRuntimeSpriteProfile(profiles, renderers[renderIndex] != null ? renderers[renderIndex].transform : null, renderers[renderIndex] != null ? renderers[renderIndex].sprite : null, obj.gameObject);

                UI2DSprite[] uiSprites = obj.GetComponentsInChildren<UI2DSprite>(true);
                for (int uiIndex = 0; uiSprites != null && uiIndex < uiSprites.Length; uiIndex++)
                    RegisterRuntimeSpriteProfile(profiles, uiSprites[uiIndex] != null ? uiSprites[uiIndex].transform : null, uiSprites[uiIndex] != null ? uiSprites[uiIndex].sprite2D : null, obj.gameObject);
            }

            SpriteRenderer[] allRenderers = Resources.FindObjectsOfTypeAll<SpriteRenderer>();
            for (int i = 0; allRenderers != null && i < allRenderers.Length; i++)
                RegisterRuntimeSpriteProfile(profiles, allRenderers[i] != null ? allRenderers[i].transform : null, allRenderers[i] != null ? allRenderers[i].sprite : null, null);

            UI2DSprite[] allUiSprites = Resources.FindObjectsOfTypeAll<UI2DSprite>();
            for (int i = 0; allUiSprites != null && i < allUiSprites.Length; i++)
                RegisterRuntimeSpriteProfile(profiles, allUiSprites[i] != null ? allUiSprites[i].transform : null, allUiSprites[i] != null ? allUiSprites[i].sprite2D : null, null);

            _cachedRuntimeProfiles = profiles;
            _cachedRuntimeProfileFrame = Time.frameCount;
            return _cachedRuntimeProfiles;
        }

        private static void RegisterRuntimeSpriteProfile(
            Dictionary<string, FamilyProfile> profiles,
            Transform transform,
            Sprite sprite,
            GameObject forcedRoot)
        {
            if (profiles == null || sprite == null)
                return;

            string runtimeSpriteKey = ScenarioSpriteReferenceLibrary.CreateRuntimeSpriteKey(sprite);
            if (string.IsNullOrEmpty(runtimeSpriteKey))
                return;

            FamilyProfile profile = BuildProfileFromTransform(transform, sprite, ScenarioAuthoringTargetKind.Unknown, forcedRoot);
            if (profile == null)
                return;

            FamilyProfile existing;
            if (!profiles.TryGetValue(runtimeSpriteKey, out existing))
            {
                profiles[runtimeSpriteKey] = CloneProfile(profile);
                return;
            }

            MergeInto(existing, profile);
        }

        private static FamilyProfile BuildProfileFromTransform(
            Transform transform,
            Sprite sprite,
            ScenarioAuthoringTargetKind preferredKind)
        {
            return BuildProfileFromTransform(transform, sprite, preferredKind, null);
        }

        private static FamilyProfile BuildProfileFromTransform(
            Transform transform,
            Sprite sprite,
            ScenarioAuthoringTargetKind preferredKind,
            GameObject forcedRoot)
        {
            FamilyProfile profile = CreateEmptyProfile();
            Obj_Base objBase = forcedRoot != null ? forcedRoot.GetComponent<Obj_Base>() : null;
            if (objBase == null && transform != null)
                objBase = transform.GetComponentInParent<Obj_Base>();

            if (objBase != null)
            {
                string objectType = SafeEnumName(objBase.GetObjectType());
                string typeKey = !string.IsNullOrEmpty(objectType) ? "objtype:" + objectType : null;
                if (!string.IsNullOrEmpty(typeKey))
                {
                    profile.PrimaryKey = typeKey;
                    profile.DisplayLabel = objectType;
                    profile.KindKey = BuildKindKey(ScenarioAuthoringTargetKind.PlaceableObject);
                    profile.HasStrongIdentity = true;
                    profile.MatchKeys.Add(typeKey);
                    profile.MatchKeys.Add("objclass:" + objBase.GetType().Name);
                    profile.MatchKeys.Add(profile.KindKey);
                }

                AddNameTokens(profile, objectType);
                AddNameTokens(profile, objBase.GetType().Name);
                AddNameTokens(profile, objBase.gameObject != null ? objBase.gameObject.name : null);
                AddNameTokens(profile, BuildTransformPath(objBase.transform));
            }
            else
            {
                ScenarioAuthoringTargetKind kind = preferredKind != ScenarioAuthoringTargetKind.Unknown && preferredKind != ScenarioAuthoringTargetKind.None
                    ? preferredKind
                    : ClassifyTransform(transform, sprite);
                string kindKey = BuildKindKey(kind);
                if (!string.IsNullOrEmpty(kindKey))
                {
                    profile.PrimaryKey = kindKey;
                    profile.DisplayLabel = kind.ToString();
                    profile.KindKey = kindKey;
                    profile.HasStrongIdentity = kind != ScenarioAuthoringTargetKind.Unknown && kind != ScenarioAuthoringTargetKind.None;
                    profile.MatchKeys.Add(kindKey);
                }

                AddNameTokens(profile, transform != null ? transform.name : null);
                AddNameTokens(profile, BuildTransformPath(transform));
            }

            AddSpriteTokens(profile, sprite);
            return profile;
        }

        private static ScenarioAuthoringTargetKind ClassifyTransform(Transform transform, Sprite sprite)
        {
            string path = BuildTransformPath(transform);
            string lowerPath = !string.IsNullOrEmpty(path) ? path.ToLowerInvariant() : string.Empty;

            if (ContainsAny(lowerPath, "wire", "cable", "power"))
                return ScenarioAuthoringTargetKind.Wire;
            if (ContainsAny(lowerPath, "wall", "barricade"))
                return ScenarioAuthoringTargetKind.Wall;
            if (ContainsAny(lowerPath, "light", "lamp"))
                return ScenarioAuthoringTargetKind.Light;
            if (ContainsAny(lowerPath, "van", "vehicle", "rv"))
                return ScenarioAuthoringTargetKind.Vehicle;
            if (ContainsAny(lowerPath, "room"))
                return ScenarioAuthoringTargetKind.Room;
            if (ContainsAny(lowerPath, "tile", "grid"))
                return ScenarioAuthoringTargetKind.Tile;
            if (ContainsAny(lowerPath, "background", "scenery", "sky", "terrain", "backdrop"))
                return ScenarioAuthoringTargetKind.Background;

            SpriteRenderer spriteRenderer = transform != null ? transform.GetComponent<SpriteRenderer>() : null;
            if (spriteRenderer != null && spriteRenderer.sortingOrder < 0)
                return ScenarioAuthoringTargetKind.Background;

            return sprite != null ? ScenarioAuthoringTargetKind.PlaceableObject : ScenarioAuthoringTargetKind.Unknown;
        }

        private static string BuildKindKey(ScenarioAuthoringTargetKind kind)
        {
            switch (kind)
            {
                case ScenarioAuthoringTargetKind.PlaceableObject:
                case ScenarioAuthoringTargetKind.Wall:
                case ScenarioAuthoringTargetKind.Wire:
                case ScenarioAuthoringTargetKind.Light:
                case ScenarioAuthoringTargetKind.Vehicle:
                case ScenarioAuthoringTargetKind.Room:
                case ScenarioAuthoringTargetKind.Tile:
                case ScenarioAuthoringTargetKind.Background:
                case ScenarioAuthoringTargetKind.SceneSprite:
                    return "kind:" + kind;
                default:
                    return null;
            }
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

        private static string BuildTransformPath(Transform transform)
        {
            if (transform == null)
                return null;

            List<string> names = new List<string>();
            Transform current = transform;
            while (current != null)
            {
                names.Add(current.name);
                current = current.parent;
            }

            names.Reverse();
            return string.Join("/", names.ToArray());
        }

        private static void AddSpriteTokens(FamilyProfile profile, Sprite sprite)
        {
            if (profile == null || sprite == null)
                return;

            AddNameTokens(profile, sprite.name);
            Texture2D texture = sprite.texture;
            if (texture != null)
                AddNameTokens(profile, texture.name);
        }

        private static void AddNameTokens(FamilyProfile profile, string value)
        {
            if (profile == null || string.IsNullOrEmpty(value))
                return;

            List<string> tokens = SplitTokens(value);
            for (int i = 0; i < tokens.Count; i++)
            {
                string token = tokens[i];
                if (IsStopWord(token))
                    continue;

                profile.Tokens.Add(token);
            }

            string collapsed = CollapseTokens(tokens);
            if (!string.IsNullOrEmpty(collapsed) && !IsStopWord(collapsed))
                profile.Tokens.Add(collapsed);
        }

        private static List<string> SplitTokens(string value)
        {
            List<string> tokens = new List<string>();
            if (string.IsNullOrEmpty(value))
                return tokens;

            char[] buffer = value.ToCharArray();
            List<char> current = new List<char>();
            for (int i = 0; i < buffer.Length; i++)
            {
                char currentChar = buffer[i];
                bool isLetterOrDigit = char.IsLetterOrDigit(currentChar);
                if (!isLetterOrDigit)
                {
                    FlushToken(tokens, current);
                    continue;
                }

                if (current.Count > 0
                    && char.IsUpper(currentChar)
                    && char.IsLower(current[current.Count - 1]))
                {
                    FlushToken(tokens, current);
                }

                current.Add(char.ToLowerInvariant(currentChar));
            }

            FlushToken(tokens, current);
            return tokens;
        }

        private static void FlushToken(List<string> tokens, List<char> current)
        {
            if (tokens == null || current == null || current.Count == 0)
                return;

            string token = new string(current.ToArray());
            if (!string.IsNullOrEmpty(token))
                tokens.Add(token);
            current.Clear();
        }

        private static string CollapseTokens(List<string> tokens)
        {
            if (tokens == null || tokens.Count == 0)
                return null;

            string collapsed = string.Empty;
            for (int i = 0; i < tokens.Count; i++)
                collapsed += tokens[i];
            return collapsed;
        }

        private static bool SharesKey(HashSet<string> left, HashSet<string> right)
        {
            if (left == null || right == null || left.Count == 0 || right.Count == 0)
                return false;

            foreach (string key in left)
            {
                if (!string.IsNullOrEmpty(key) && right.Contains(key))
                    return true;
            }

            return false;
        }

        private static int CountTokenOverlap(HashSet<string> left, HashSet<string> right)
        {
            if (left == null || right == null || left.Count == 0 || right.Count == 0)
                return 0;

            int overlap = 0;
            foreach (string token in left)
            {
                if (!string.IsNullOrEmpty(token) && right.Contains(token))
                    overlap++;
            }

            return overlap;
        }

        private static bool HasDistinctiveSharedToken(HashSet<string> left, HashSet<string> right)
        {
            if (left == null || right == null)
                return false;

            foreach (string token in left)
            {
                if (string.IsNullOrEmpty(token) || token.Length < 5 || IsStopWord(token))
                    continue;

                if (right.Contains(token))
                    return true;
            }

            return false;
        }

        private static bool ContainsAny(string value, params string[] parts)
        {
            if (string.IsNullOrEmpty(value) || parts == null)
                return false;

            for (int i = 0; i < parts.Length; i++)
            {
                if (!string.IsNullOrEmpty(parts[i]) && value.IndexOf(parts[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static string SafeEnumName(Enum value)
        {
            return value != null ? value.ToString() : null;
        }

        private static bool IsStopWord(string token)
        {
            if (string.IsNullOrEmpty(token))
                return true;

            for (int i = 0; i < StopWords.Length; i++)
            {
                if (string.Equals(StopWords[i], token, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static FamilyProfile CreateEmptyProfile()
        {
            return new FamilyProfile
            {
                MatchKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                Tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            };
        }

        private static FamilyProfile CloneProfile(FamilyProfile source)
        {
            if (source == null)
                return null;

            FamilyProfile clone = CreateEmptyProfile();
            clone.PrimaryKey = source.PrimaryKey;
            clone.DisplayLabel = source.DisplayLabel;
            clone.KindKey = source.KindKey;
            clone.HasStrongIdentity = source.HasStrongIdentity;

            foreach (string key in source.MatchKeys)
                clone.MatchKeys.Add(key);
            foreach (string token in source.Tokens)
                clone.Tokens.Add(token);

            return clone;
        }

        private static void MergeInto(FamilyProfile destination, FamilyProfile source)
        {
            if (destination == null || source == null)
                return;

            if (string.IsNullOrEmpty(destination.PrimaryKey))
                destination.PrimaryKey = source.PrimaryKey;
            if (string.IsNullOrEmpty(destination.DisplayLabel))
                destination.DisplayLabel = source.DisplayLabel;
            if (string.IsNullOrEmpty(destination.KindKey))
                destination.KindKey = source.KindKey;
            destination.HasStrongIdentity = destination.HasStrongIdentity || source.HasStrongIdentity;

            foreach (string key in source.MatchKeys)
                destination.MatchKeys.Add(key);
            foreach (string token in source.Tokens)
                destination.Tokens.Add(token);
        }
    }
}
