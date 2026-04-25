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
            public Sprite Sprite;
            public string FamilyKey;
            public string FamilyLabel;
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

        internal sealed class PlacementCatalog
        {
            public List<SpriteCandidate> VanillaCandidates;
            public List<SpriteCandidate> ModdedCandidates;
            public string FilterSummary;
            public string GuidanceMessage;
            public string XmlPathHint;
        }

        private readonly ScenarioSpriteRuntimeResolver _resolver;
        private readonly ScenarioSpriteFamilyMatcher _familyMatcher = new ScenarioSpriteFamilyMatcher();
        private string _cachedTargetPath;
        private string _cachedCurrentSpriteKey;
        private int _cachedCustomSpriteSignature;
        private string _cachedScenarioFilePath;
        private int _cachedFrame = -1;
        private SpriteCatalog _cachedCatalog;
        private int _cachedPlacementCustomSpriteSignature;
        private string _cachedPlacementScenarioFilePath;
        private int _cachedPlacementFrame = -1;
        private PlacementCatalog _cachedPlacementCatalog;

        internal ScenarioSpriteCatalogService(ScenarioSpriteRuntimeResolver resolver)
        {
            _resolver = resolver;
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

            SpriteCatalog catalog = BuildCatalog(session.WorkingDefinition, target, resolvedTarget, scenarioFilePath, _familyMatcher);
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
            _cachedPlacementCustomSpriteSignature = 0;
            _cachedPlacementScenarioFilePath = null;
            _cachedPlacementFrame = -1;
            _cachedPlacementCatalog = null;
        }

        public PlacementCatalog GetPlacementCatalog(ScenarioEditorSession session, string scenarioFilePath)
        {
            if (session == null || session.WorkingDefinition == null)
                return null;

            int customSpriteSignature = ComputeCustomSpriteSignature(session.WorkingDefinition);
            if (_cachedPlacementCatalog != null
                && string.Equals(_cachedPlacementScenarioFilePath, scenarioFilePath, StringComparison.OrdinalIgnoreCase)
                && _cachedPlacementCustomSpriteSignature == customSpriteSignature
                && (_cachedPlacementFrame < 0 || Time.frameCount - _cachedPlacementFrame < 30))
            {
                return ClonePlacementCatalog(_cachedPlacementCatalog);
            }

            PlacementCatalog catalog = BuildPlacementCatalog(session.WorkingDefinition, scenarioFilePath);
            _cachedPlacementScenarioFilePath = scenarioFilePath;
            _cachedPlacementCustomSpriteSignature = customSpriteSignature;
            _cachedPlacementFrame = Time.frameCount;
            _cachedPlacementCatalog = ClonePlacementCatalog(catalog);
            return catalog;
        }

        private static SpriteCatalog BuildCatalog(
            ScenarioDefinition definition,
            ScenarioAuthoringTarget authoringTarget,
            ScenarioSpriteRuntimeResolver.ResolvedTarget target,
            string scenarioFilePath,
            ScenarioSpriteFamilyMatcher familyMatcher)
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
            if (familyMatcher == null || !familyMatcher.HasVerifiedFamily(targetFamily))
            {
                catalog.GuidanceMessage = "No verified in-game sprite family could be resolved for this target. The editor will not guess based on sprite size.";
                return catalog;
            }

            List<ScenarioSpriteReferenceLibrary.LoadedSpriteReference> loadedSprites = ScenarioSpriteReferenceLibrary.GetLoadedSprites();
            for (int i = 0; i < loadedSprites.Count; i++)
            {
                ScenarioSpriteReferenceLibrary.LoadedSpriteReference loaded = loadedSprites[i];
                if (loaded == null || loaded.Sprite == null || !IsCompatible(target.CurrentSprite, loaded.Sprite))
                    continue;

                ScenarioSpriteFamilyMatcher.FamilyProfile candidateFamily = familyMatcher != null
                    ? familyMatcher.DescribeRuntimeCandidate(loaded.RuntimeSpriteKey, loaded.Sprite)
                    : null;
                if (candidateFamily == null || !familyMatcher.IsExactVerifiedMatch(targetFamily, candidateFamily))
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
            if (catalog.VanillaCandidates.Count == 0)
            {
                catalog.GuidanceMessage = "No verified runtime replacements were found for the selected family '"
                    + familyMatcher.DescribeVerifiedFamily(targetFamily)
                    + "'. The editor will not widen the list to same-size sprites.";
            }
            else
            {
                catalog.GuidanceMessage = "Showing verified runtime replacements for the in-game family '"
                    + familyMatcher.DescribeVerifiedFamily(targetFamily)
                    + "'. Custom sprite overrides are hidden in strict mode.";
            }
            catalog.VanillaCandidates.Sort(CompareCandidate);
            catalog.ModdedCandidates.Sort(CompareCandidate);
            return catalog;
        }

        private static PlacementCatalog BuildPlacementCatalog(ScenarioDefinition definition, string scenarioFilePath)
        {
            PlacementCatalog catalog = new PlacementCatalog
            {
                VanillaCandidates = new List<SpriteCandidate>(),
                ModdedCandidates = new List<SpriteCandidate>(),
                FilterSummary = "Existing game art only",
                GuidanceMessage = "Scene sprite placement only exposes loaded runtime sprites so placed visuals stay consistent with built-in game art.",
                XmlPathHint = "AssetReferences > SceneSpritePlacements > Placement"
            };

            List<ScenarioSpriteReferenceLibrary.LoadedSpriteReference> loadedSprites = ScenarioSpriteReferenceLibrary.GetLoadedSprites();
            for (int i = 0; i < loadedSprites.Count; i++)
            {
                ScenarioSpriteReferenceLibrary.LoadedSpriteReference loaded = loadedSprites[i];
                if (loaded == null || loaded.Sprite == null)
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
                    Sprite = loaded.Sprite
                });
            }

            catalog.VanillaCandidates.Sort(CompareCandidate);
            catalog.ModdedCandidates.Sort(CompareCandidate);
            return catalog;
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

        private static PlacementCatalog ClonePlacementCatalog(PlacementCatalog catalog)
        {
            if (catalog == null)
                return null;

            return new PlacementCatalog
            {
                VanillaCandidates = CloneCandidates(catalog.VanillaCandidates),
                ModdedCandidates = CloneCandidates(catalog.ModdedCandidates),
                FilterSummary = catalog.FilterSummary,
                GuidanceMessage = catalog.GuidanceMessage,
                XmlPathHint = catalog.XmlPathHint
            };
        }

        private static List<SpriteCandidate> CloneCandidates(List<SpriteCandidate> source)
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
                    Sprite = item.Sprite,
                    FamilyKey = item.FamilyKey,
                    FamilyLabel = item.FamilyLabel
                });
            }

            return clone;
        }

        private static int ComputeCustomSpriteSignature(ScenarioDefinition definition)
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

        private static string BuildLabel(string spriteName, string sourceName)
        {
            string primary = !string.IsNullOrEmpty(spriteName) ? spriteName : "<sprite>";
            string source = !string.IsNullOrEmpty(sourceName) ? sourceName : "<source>";
            return primary == source ? primary : (primary + " [" + source + "]");
        }

        private static string BuildHint(string sourceName, string spriteName, Sprite sprite)
        {
            Rect rect = sprite != null ? sprite.rect : new Rect();
            return "Map: " + (!string.IsNullOrEmpty(sourceName) ? sourceName : "<source>")
                + " | Sprite: " + (!string.IsNullOrEmpty(spriteName) ? spriteName : "<sprite>")
                + " | Size: " + Mathf.RoundToInt(rect.width) + "x" + Mathf.RoundToInt(rect.height);
        }

        private static int CompareCandidate(SpriteCandidate left, SpriteCandidate right)
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
