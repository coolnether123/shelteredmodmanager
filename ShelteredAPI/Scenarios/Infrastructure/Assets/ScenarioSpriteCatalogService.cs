using System;
using System.Collections.Generic;
using ModAPI.Scenarios;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioSpriteCatalogService
    {
        internal enum SpriteCandidateSourceKind
        {
            VanillaRuntime = 0,
            ScenarioCustom = 1
        }

        internal sealed class SpriteCandidate
        {
            public string Token;
            public string Label;
            public string Hint;
            public string SpriteName;
            public string SourceName;
            public SpriteCandidateSourceKind SourceKind;
            public string RuntimeSpriteKey;
            public string SpriteId;
            public string RelativePath;
            public bool UserOwned;
            public Sprite Sprite;
            public string FamilyKey;
            public string FamilyLabel;
            public ScenarioPlaceableAssetKind PlacementKind;
            public bool CanPlaceAsSceneSprite;
            public string PlacementGuidance;
        }

        internal sealed class SpriteCatalog
        {
            public ScenarioSpriteRuntimeResolver.ResolvedTarget Target;
            public List<SpriteCandidate> VanillaCandidates;
            public List<SpriteCandidate> ModdedCandidates;
            public bool FamilyFiltered;
            public string FilterSummary;
            public string GuidanceMessage;
            public string XmlPathHint;
        }

        private readonly ScenarioSpriteRuntimeResolver _resolver;
        private readonly IScenarioSpriteAssetResolver _assetResolver;
        private readonly ScenarioSpriteFamilyMatcher _familyMatcher = new ScenarioSpriteFamilyMatcher();
        private string _cachedTargetPath;
        private string _cachedCurrentSpriteKey;
        private int _cachedCustomSpriteSignature;
        private string _cachedScenarioFilePath;
        private int _cachedFrame = -1;
        private SpriteCatalog _cachedCatalog;
        internal ScenarioSpriteCatalogService(ScenarioSpriteRuntimeResolver resolver, IScenarioSpriteAssetResolver assetResolver)
        {
            _resolver = resolver;
            _assetResolver = assetResolver;
        }

        public SpriteCatalog GetCatalog(ScenarioEditorSession session, ScenarioAuthoringTarget target, string scenarioFilePath)
        {
            if (session == null || session.WorkingDefinition == null || target == null)
                return null;

            ScenarioSpriteRuntimeResolver.ResolvedTarget resolvedTarget;
            if (!_resolver.TryResolve(target, out resolvedTarget) || resolvedTarget == null || resolvedTarget.CurrentSprite == null)
                return null;

            string targetPath = resolvedTarget.TargetPath ?? target.TransformPath;
            string currentSpriteKey = ScenarioSpriteReferenceLibrary.CreateRuntimeSpriteKey(resolvedTarget.CurrentSprite);
            int customSpriteSignature = ComputeCustomSpriteSignature(session.WorkingDefinition);
            if (_cachedCatalog != null
                && string.Equals(_cachedTargetPath, targetPath, StringComparison.OrdinalIgnoreCase)
                && string.Equals(_cachedCurrentSpriteKey, currentSpriteKey, StringComparison.OrdinalIgnoreCase)
                && string.Equals(_cachedScenarioFilePath, scenarioFilePath, StringComparison.OrdinalIgnoreCase)
                && _cachedCustomSpriteSignature == customSpriteSignature
                && (_cachedFrame < 0 || Time.frameCount - _cachedFrame < 30))
            {
                return CloneCatalog(_cachedCatalog);
            }

            SpriteCatalog catalog = BuildCatalog(session.WorkingDefinition, target, resolvedTarget, scenarioFilePath, _familyMatcher, _assetResolver);
            _cachedTargetPath = targetPath;
            _cachedCurrentSpriteKey = currentSpriteKey;
            _cachedScenarioFilePath = scenarioFilePath;
            _cachedCustomSpriteSignature = customSpriteSignature;
            _cachedFrame = Time.frameCount;
            _cachedCatalog = CloneCatalog(catalog);
            return catalog;
        }

        public void Invalidate()
        {
            _cachedTargetPath = null;
            _cachedCurrentSpriteKey = null;
            _cachedScenarioFilePath = null;
            _cachedCustomSpriteSignature = 0;
            _cachedFrame = -1;
            _cachedCatalog = null;
        }

        private static SpriteCatalog BuildCatalog(
            ScenarioDefinition definition,
            ScenarioAuthoringTarget authoringTarget,
            ScenarioSpriteRuntimeResolver.ResolvedTarget target,
            string scenarioFilePath,
            ScenarioSpriteFamilyMatcher familyMatcher,
            IScenarioSpriteAssetResolver assetResolver)
        {
            SpriteCatalog catalog = new SpriteCatalog
            {
                Target = target,
                VanillaCandidates = new List<SpriteCandidate>(),
                ModdedCandidates = new List<SpriteCandidate>(),
                FamilyFiltered = true,
                FilterSummary = "Verified in-game replacements only",
                GuidanceMessage = "The editor will only list verified runtime sprites already used by this in-game target family.",
                XmlPathHint = "AssetReferences > SpriteSwaps > Swap"
            };

            ScenarioSpriteFamilyMatcher.FamilyProfile targetFamily = familyMatcher != null
                ? familyMatcher.DescribeTarget(authoringTarget, target)
                : null;
            bool allowEnvironmentPalette = IsEnvironmentArtTarget(authoringTarget);
            if (familyMatcher == null || !familyMatcher.HasVerifiedFamily(targetFamily))
            {
                if (!allowEnvironmentPalette)
                {
                    AddCustomSpriteCandidates(
                        definition,
                        scenarioFilePath,
                        assetResolver,
                        catalog.ModdedCandidates,
                        target.CurrentSprite,
                        true);
                    catalog.GuidanceMessage = catalog.ModdedCandidates.Count > 0
                        ? "No verified in-game sprite family could be resolved for this target. Showing compatible scenario custom sprite patches only."
                        : "No verified in-game sprite family could be resolved for this target. The editor will not guess based on sprite size.";
                    return catalog;
                }

                catalog.FamilyFiltered = false;
                catalog.FilterSummary = "Scenario environment art";
                catalog.GuidanceMessage = "Showing same-size loaded environment sprites so scenario wall and background art can be reused across built-in scenarios.";
            }

            List<ScenarioSpriteReferenceLibrary.LoadedSpriteReference> loadedSprites = ScenarioSpriteReferenceLibrary.GetLoadedSprites();
            for (int i = 0; i < loadedSprites.Count; i++)
            {
                ScenarioSpriteReferenceLibrary.LoadedSpriteReference loaded = loadedSprites[i];
                if (loaded == null
                    || loaded.Sprite == null
                    || IsGeneratedPatchRuntimeKey(loaded.RuntimeSpriteKey)
                    || !IsCompatible(target.CurrentSprite, loaded.Sprite))
                {
                    continue;
                }

                ScenarioSpriteFamilyMatcher.FamilyProfile candidateFamily = familyMatcher != null
                    ? familyMatcher.DescribeRuntimeCandidate(loaded.RuntimeSpriteKey, loaded.Sprite)
                    : null;
                bool exactMatch = candidateFamily != null
                    && familyMatcher != null
                    && familyMatcher.IsExactVerifiedMatch(targetFamily, candidateFamily);
                if (!exactMatch && !allowEnvironmentPalette)
                    continue;
                if (!exactMatch && allowEnvironmentPalette && !IsEnvironmentCandidate(loaded, candidateFamily))
                    continue;

                catalog.VanillaCandidates.Add(new SpriteCandidate
                {
                    Token = "runtime:" + (loaded.RuntimeSpriteKey ?? string.Empty),
                    Label = BuildLabel(loaded.SpriteName, loaded.TextureName),
                    Hint = BuildHint(loaded.TextureName, loaded.SpriteName, loaded.Sprite),
                    SpriteName = loaded.SpriteName,
                    SourceName = loaded.TextureName,
                    SourceKind = SpriteCandidateSourceKind.VanillaRuntime,
                    RuntimeSpriteKey = loaded.RuntimeSpriteKey,
                    Sprite = loaded.Sprite,
                    FamilyKey = candidateFamily != null ? candidateFamily.PrimaryKey : null,
                    FamilyLabel = candidateFamily != null ? candidateFamily.DisplayLabel : null
                });
            }
            catalog.ModdedCandidates.Clear();
            AddCustomSpriteCandidates(
                definition,
                scenarioFilePath,
                assetResolver,
                catalog.ModdedCandidates,
                target.CurrentSprite,
                true);
            if (catalog.VanillaCandidates.Count == 0)
            {
                catalog.GuidanceMessage = allowEnvironmentPalette && !catalog.FamilyFiltered
                    ? "No same-size loaded environment sprites were found for this scenario art target."
                    : "No verified runtime replacements were found for the selected family '"
                        + (familyMatcher != null ? familyMatcher.DescribeVerifiedFamily(targetFamily) : "<unknown>")
                        + "'. The editor will not widen the list to same-size sprites.";
            }
            else
            {
                catalog.GuidanceMessage = allowEnvironmentPalette && !catalog.FamilyFiltered
                    ? "Showing same-size loaded environment sprites, including built-in scenario room/background sheets when Unity has them loaded."
                    : "Showing verified runtime replacements for the in-game family '"
                        + familyMatcher.DescribeVerifiedFamily(targetFamily)
                        + "'. Scenario custom sprite patches are listed separately.";
            }
            catalog.VanillaCandidates.Sort(CompareCandidate);
            catalog.ModdedCandidates.Sort(CompareCandidate);
            return catalog;
        }

        internal static void AddCustomSpriteCandidates(
            ScenarioDefinition definition,
            string scenarioFilePath,
            IScenarioSpriteAssetResolver assetResolver,
            List<SpriteCandidate> candidates,
            Sprite compatibilityTarget,
            bool requireCompatible)
        {
            if (definition == null
                || definition.AssetReferences == null
                || definition.AssetReferences.CustomSprites == null
                || assetResolver == null
                || candidates == null)
            {
                return;
            }

            string packRoot = !string.IsNullOrEmpty(scenarioFilePath) ? System.IO.Path.GetDirectoryName(scenarioFilePath) : null;
            for (int i = 0; i < definition.AssetReferences.CustomSprites.Count; i++)
            {
                SpriteRef spriteRef = definition.AssetReferences.CustomSprites[i];
                if (spriteRef == null || string.IsNullOrEmpty(spriteRef.Id))
                    continue;

                Sprite sprite = assetResolver.ResolveSprite(
                    definition,
                    packRoot,
                    spriteRef.Id,
                    spriteRef.RelativePath,
                    null,
                    "scenario custom sprite '" + spriteRef.Id + "'");
                if (sprite == null)
                    continue;

                if (requireCompatible && !IsCompatible(compatibilityTarget, sprite))
                    continue;

                SpritePatchDefinition patch = FindPatch(definition.AssetReferences, spriteRef.PatchId);
                candidates.Add(new SpriteCandidate
                {
                    Token = "custom:" + spriteRef.Id,
                    Label = !string.IsNullOrEmpty(patch != null ? patch.DisplayName : null)
                        ? patch.DisplayName
                        : spriteRef.Id,
                    Hint = BuildCustomSpriteHint(spriteRef, patch, sprite),
                    SpriteName = spriteRef.Id,
                    SourceName = "Scenario Custom",
                    SourceKind = SpriteCandidateSourceKind.ScenarioCustom,
                    SpriteId = spriteRef.Id,
                    RelativePath = spriteRef.RelativePath,
                    UserOwned = spriteRef.UserOwned,
                    Sprite = sprite
                });
            }
        }

        private static SpriteCatalog CloneCatalog(SpriteCatalog catalog)
        {
            if (catalog == null)
                return null;

            return new SpriteCatalog
            {
                Target = catalog.Target,
                VanillaCandidates = CloneCandidates(catalog.VanillaCandidates),
                ModdedCandidates = CloneCandidates(catalog.ModdedCandidates),
                FamilyFiltered = catalog.FamilyFiltered,
                FilterSummary = catalog.FilterSummary,
                GuidanceMessage = catalog.GuidanceMessage,
                XmlPathHint = catalog.XmlPathHint
            };
        }

        internal static List<SpriteCandidate> CloneCandidates(List<SpriteCandidate> source)
        {
            List<SpriteCandidate> clone = new List<SpriteCandidate>();
            for (int i = 0; source != null && i < source.Count; i++)
            {
                SpriteCandidate item = source[i];
                if (item == null)
                    continue;

                clone.Add(new SpriteCandidate
                {
                    Token = item.Token,
                    Label = item.Label,
                    Hint = item.Hint,
                    SpriteName = item.SpriteName,
                    SourceName = item.SourceName,
                    SourceKind = item.SourceKind,
                    RuntimeSpriteKey = item.RuntimeSpriteKey,
                    SpriteId = item.SpriteId,
                    RelativePath = item.RelativePath,
                    UserOwned = item.UserOwned,
                    Sprite = item.Sprite,
                    FamilyKey = item.FamilyKey,
                    FamilyLabel = item.FamilyLabel,
                    PlacementKind = item.PlacementKind,
                    CanPlaceAsSceneSprite = item.CanPlaceAsSceneSprite,
                    PlacementGuidance = item.PlacementGuidance
                });
            }

            return clone;
        }

        internal static int ComputeCustomSpriteSignature(ScenarioDefinition definition)
        {
            if (definition == null || definition.AssetReferences == null || definition.AssetReferences.CustomSprites == null)
                return 0;

            int hash = definition.AssetReferences.CustomSprites.Count;
            for (int i = 0; i < definition.AssetReferences.CustomSprites.Count; i++)
            {
                SpriteRef sprite = definition.AssetReferences.CustomSprites[i];
                if (sprite == null)
                    continue;

                hash = (hash * 397) ^ SafeHash(sprite.Id);
                hash = (hash * 397) ^ SafeHash(sprite.RelativePath);
                hash = (hash * 397) ^ SafeHash(sprite.PatchId);
                hash = (hash * 397) ^ (sprite.UserOwned ? 1 : 0);
            }

            if (definition.AssetReferences.SpritePatches != null)
            {
                hash = (hash * 397) ^ definition.AssetReferences.SpritePatches.Count;
                for (int i = 0; i < definition.AssetReferences.SpritePatches.Count; i++)
                {
                    SpritePatchDefinition patch = definition.AssetReferences.SpritePatches[i];
                    if (patch == null)
                        continue;

                    hash = (hash * 397) ^ SafeHash(patch.Id);
                    hash = (hash * 397) ^ patch.Width;
                    hash = (hash * 397) ^ patch.Height;
                    hash = (hash * 397) ^ (patch.Operations != null ? patch.Operations.Count : 0);
                    for (int operationIndex = 0; patch.Operations != null && operationIndex < patch.Operations.Count; operationIndex++)
                    {
                        SpritePatchOperation operation = patch.Operations[operationIndex];
                        if (operation == null)
                            continue;

                        hash = (hash * 397) ^ SafeHash(operation.Id);
                        hash = (hash * 397) ^ operation.Order;
                        hash = (hash * 397) ^ (int)operation.Kind;
                        hash = (hash * 397) ^ (operation.Runs != null ? operation.Runs.Count : 0);
                    }
                }
            }

            return hash;
        }

        private static int SafeHash(string value)
        {
            return !string.IsNullOrEmpty(value) ? StringComparer.OrdinalIgnoreCase.GetHashCode(value) : 0;
        }

        private static bool IsCompatible(Sprite currentSprite, Sprite candidate)
        {
            if (currentSprite == null || candidate == null)
                return false;

            Rect currentRect = currentSprite.rect;
            Rect candidateRect = candidate.rect;
            return Mathf.RoundToInt(currentRect.width) == Mathf.RoundToInt(candidateRect.width)
                && Mathf.RoundToInt(currentRect.height) == Mathf.RoundToInt(candidateRect.height);
        }

        private static bool IsEnvironmentArtTarget(ScenarioAuthoringTarget target)
        {
            if (target == null)
                return false;

            switch (target.Kind)
            {
                case ScenarioAuthoringTargetKind.Background:
                case ScenarioAuthoringTargetKind.Wall:
                case ScenarioAuthoringTargetKind.Room:
                case ScenarioAuthoringTargetKind.Tile:
                    return true;
                default:
                    return false;
            }
        }

        internal static bool IsGeneratedPatchRuntimeKey(string runtimeSpriteKey)
        {
            return !string.IsNullOrEmpty(runtimeSpriteKey)
                && runtimeSpriteKey.StartsWith("patch:", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsEnvironmentCandidate(
            ScenarioSpriteReferenceLibrary.LoadedSpriteReference loaded,
            ScenarioSpriteFamilyMatcher.FamilyProfile candidateFamily)
        {
            if (candidateFamily != null && !string.IsNullOrEmpty(candidateFamily.KindKey))
            {
                if (candidateFamily.KindKey.IndexOf("Background", StringComparison.OrdinalIgnoreCase) >= 0
                    || candidateFamily.KindKey.IndexOf("Wall", StringComparison.OrdinalIgnoreCase) >= 0
                    || candidateFamily.KindKey.IndexOf("Room", StringComparison.OrdinalIgnoreCase) >= 0
                    || candidateFamily.KindKey.IndexOf("Tile", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            string combined = ((loaded != null ? loaded.SpriteName : null) ?? string.Empty)
                + " "
                + ((loaded != null ? loaded.TextureName : null) ?? string.Empty);
            return ContainsAny(combined, "wall", "background", "backdrop", "room", "bunker", "shelter", "surrounded", "stasis", "scenario");
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

        internal static string BuildLabel(string spriteName, string sourceName)
        {
            string primary = !string.IsNullOrEmpty(spriteName) ? spriteName : "<sprite>";
            string source = !string.IsNullOrEmpty(sourceName) ? sourceName : "<source>";
            return primary == source ? primary : (primary + " [" + source + "]");
        }

        internal static string BuildHint(string sourceName, string spriteName, Sprite sprite)
        {
            Rect rect = sprite != null ? sprite.rect : new Rect();
            return "Map: " + (!string.IsNullOrEmpty(sourceName) ? sourceName : "<source>")
                + " | Sprite: " + (!string.IsNullOrEmpty(spriteName) ? spriteName : "<sprite>")
                + " | Size: " + Mathf.RoundToInt(rect.width) + "x" + Mathf.RoundToInt(rect.height);
        }

        private static string BuildCustomSpriteHint(SpriteRef spriteRef, SpritePatchDefinition patch, Sprite sprite)
        {
            Rect rect = sprite != null ? sprite.rect : new Rect();
            string source = patch != null && !string.IsNullOrEmpty(patch.BaseRuntimeSpriteKey)
                ? "runtime"
                : (patch != null && !string.IsNullOrEmpty(patch.BaseSpriteId)
                    ? patch.BaseSpriteId
                    : (spriteRef != null && !string.IsNullOrEmpty(spriteRef.RelativePath) ? spriteRef.RelativePath : "scenario patch"));
            return "Scenario custom sprite"
                + (spriteRef != null && spriteRef.UserOwned ? " | User-owned asset" : string.Empty)
                + " | Source: " + source
                + " | Size: " + Mathf.RoundToInt(rect.width) + "x" + Mathf.RoundToInt(rect.height);
        }

        private static SpritePatchDefinition FindPatch(AssetReferencesDefinition assets, string patchId)
        {
            if (assets == null || assets.SpritePatches == null || string.IsNullOrEmpty(patchId))
                return null;

            for (int i = 0; i < assets.SpritePatches.Count; i++)
            {
                SpritePatchDefinition patch = assets.SpritePatches[i];
                if (patch != null && string.Equals(patch.Id, patchId, StringComparison.OrdinalIgnoreCase))
                    return patch;
            }

            return null;
        }

        internal static int CompareCandidate(SpriteCandidate left, SpriteCandidate right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return 1;
            if (right == null) return -1;

            int source = string.Compare(left.SourceName, right.SourceName, StringComparison.OrdinalIgnoreCase);
            if (source != 0) return source;

            int label = string.Compare(left.Label, right.Label, StringComparison.OrdinalIgnoreCase);
            if (label != 0) return label;

            return string.Compare(left.Token, right.Token, StringComparison.OrdinalIgnoreCase);
        }
    }
}
