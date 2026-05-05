using System;
using System.Collections.Generic;
using UnityEngine;
using ShelteredAPI.Content;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Assets;
using ShelteredAPI.Scenarios.Infrastructure.Assets;
namespace ShelteredAPI.Scenarios.Application.Assets{
    internal sealed class ScenarioSceneSpritePlacementCatalogService
    {
        internal sealed class PlacementCatalog
        {
            public List<ScenarioSpriteCatalogService.SpriteCandidate> VanillaCandidates;
            public List<ScenarioSpriteCatalogService.SpriteCandidate> ModdedCandidates;
            public string FilterSummary;
            public string GuidanceMessage;
            public string XmlPathHint;
            public int BlockedPeople;
            public int BlockedInteractiveObjects;
            public int BlockedPathfindingActors;
            public int BlockedGameplayAssets;
        }

        private readonly IScenarioSpriteAssetResolver _assetResolver;
        private readonly ScenarioSpritePlacementPolicy _placementPolicy;
        private int _cachedCustomSpriteSignature;
        private string _cachedScenarioFilePath;
        private int _cachedFrame = -1;
        private PlacementCatalog _cachedCatalog;

        public ScenarioSceneSpritePlacementCatalogService(
            IScenarioSpriteAssetResolver assetResolver,
            ScenarioSpritePlacementPolicy placementPolicy)
        {
            _assetResolver = assetResolver;
            _placementPolicy = placementPolicy;
        }

        public PlacementCatalog GetCatalog(ScenarioEditorSession session, string scenarioFilePath)
        {
            if (session == null || session.WorkingDefinition == null)
                return null;

            int customSpriteSignature = ScenarioSpriteCatalogService.ComputeCustomSpriteSignature(session.WorkingDefinition);
            if (_cachedCatalog != null
                && string.Equals(_cachedScenarioFilePath, scenarioFilePath, StringComparison.OrdinalIgnoreCase)
                && _cachedCustomSpriteSignature == customSpriteSignature
                && (_cachedFrame < 0 || Time.frameCount - _cachedFrame < 30))
            {
                return Clone(_cachedCatalog);
            }

            PlacementCatalog catalog = BuildCatalog(session.WorkingDefinition, scenarioFilePath);
            _cachedScenarioFilePath = scenarioFilePath;
            _cachedCustomSpriteSignature = customSpriteSignature;
            _cachedFrame = Time.frameCount;
            _cachedCatalog = Clone(catalog);
            return catalog;
        }

        public void Invalidate()
        {
            _cachedCustomSpriteSignature = 0;
            _cachedScenarioFilePath = null;
            _cachedFrame = -1;
            _cachedCatalog = null;
        }

        private PlacementCatalog BuildCatalog(ScenarioDefinition definition, string scenarioFilePath)
        {
            PlacementCatalog catalog = new PlacementCatalog
            {
                VanillaCandidates = new List<ScenarioSpriteCatalogService.SpriteCandidate>(),
                ModdedCandidates = new List<ScenarioSpriteCatalogService.SpriteCandidate>(),
                FilterSummary = "Visual-only scene dressing",
                GuidanceMessage = "Scene sprite placement only lists assets that can safely exist as visuals. People, interactive objects, pathfinding actors, and gameplay assets must use their dedicated authoring tools.",
                XmlPathHint = "AssetReferences > SceneSpritePlacements > Placement"
            };

            AddRuntimeCandidates(catalog);
            ScenarioSpriteCatalogService.AddCustomSpriteCandidates(
                definition,
                scenarioFilePath,
                _assetResolver,
                catalog.ModdedCandidates,
                null,
                false);
            AnnotateCustomCandidates(catalog.ModdedCandidates);

            catalog.VanillaCandidates.Sort(ScenarioSpriteCatalogService.CompareCandidate);
            catalog.ModdedCandidates.Sort(ScenarioSpriteCatalogService.CompareCandidate);
            catalog.FilterSummary = BuildFilterSummary(catalog);
            return catalog;
        }

        private void AddRuntimeCandidates(PlacementCatalog catalog)
        {
            List<ScenarioSpriteReferenceLibrary.LoadedSpriteReference> loadedSprites = ScenarioSpriteReferenceLibrary.GetLoadedSprites();
            for (int i = 0; loadedSprites != null && i < loadedSprites.Count; i++)
            {
                ScenarioSpriteReferenceLibrary.LoadedSpriteReference loaded = loadedSprites[i];
                if (loaded == null || loaded.Sprite == null || ScenarioSpriteCatalogService.IsGeneratedPatchRuntimeKey(loaded.RuntimeSpriteKey))
                    continue;

                ScenarioPlaceableAssetClassification classification = _placementPolicy != null
                    ? _placementPolicy.ClassifyRuntimeSprite(loaded)
                    : null;
                if (classification != null && !classification.CanPlaceAsSceneSprite)
                {
                    CountBlocked(catalog, classification.Kind);
                    continue;
                }

                catalog.VanillaCandidates.Add(new ScenarioSpriteCatalogService.SpriteCandidate
                {
                    Token = "runtime:" + (loaded.RuntimeSpriteKey ?? string.Empty),
                    Label = ScenarioSpriteCatalogService.BuildLabel(loaded.SpriteName, loaded.TextureName),
                    Hint = AppendPlacementGuidance(
                        ScenarioSpriteCatalogService.BuildHint(loaded.TextureName, loaded.SpriteName, loaded.Sprite),
                        classification),
                    SpriteName = loaded.SpriteName,
                    SourceName = loaded.TextureName,
                    SourceKind = ScenarioSpriteCatalogService.SpriteCandidateSourceKind.VanillaRuntime,
                    RuntimeSpriteKey = loaded.RuntimeSpriteKey,
                    Sprite = loaded.Sprite,
                    PlacementKind = classification != null ? classification.Kind : ScenarioPlaceableAssetKind.VisualOnly,
                    PlacementGuidance = classification != null ? classification.Guidance : null,
                    CanPlaceAsSceneSprite = true
                });
            }
        }

        private void AnnotateCustomCandidates(List<ScenarioSpriteCatalogService.SpriteCandidate> candidates)
        {
            for (int i = 0; candidates != null && i < candidates.Count; i++)
            {
                ScenarioSpriteCatalogService.SpriteCandidate candidate = candidates[i];
                if (candidate == null)
                    continue;

                ScenarioPlaceableAssetClassification classification = _placementPolicy != null
                    ? _placementPolicy.ClassifyCustomSprite(candidate.Sprite)
                    : null;
                candidate.PlacementKind = classification != null ? classification.Kind : ScenarioPlaceableAssetKind.VisualOnly;
                candidate.PlacementGuidance = classification != null ? classification.Guidance : null;
                candidate.CanPlaceAsSceneSprite = classification == null || classification.CanPlaceAsSceneSprite;
                candidate.Hint = AppendPlacementGuidance(candidate.Hint, classification);
            }
        }

        private static void CountBlocked(PlacementCatalog catalog, ScenarioPlaceableAssetKind kind)
        {
            if (catalog == null)
                return;

            switch (kind)
            {
                case ScenarioPlaceableAssetKind.Person:
                    catalog.BlockedPeople++;
                    break;
                case ScenarioPlaceableAssetKind.InteractiveObject:
                    catalog.BlockedInteractiveObjects++;
                    break;
                case ScenarioPlaceableAssetKind.PathfindingActor:
                    catalog.BlockedPathfindingActors++;
                    break;
                case ScenarioPlaceableAssetKind.GameplayAsset:
                    catalog.BlockedGameplayAssets++;
                    break;
            }
        }

        private static string BuildFilterSummary(PlacementCatalog catalog)
        {
            if (catalog == null)
                return "Visual-only scene dressing";

            int blocked = catalog.BlockedPeople
                + catalog.BlockedInteractiveObjects
                + catalog.BlockedPathfindingActors
                + catalog.BlockedGameplayAssets;
            if (blocked == 0)
                return "Visual-only scene dressing";

            return "Visual-only scene dressing; filtered "
                + blocked.ToString()
                + " gameplay asset(s)";
        }

        private static string AppendPlacementGuidance(string hint, ScenarioPlaceableAssetClassification classification)
        {
            if (classification == null || string.IsNullOrEmpty(classification.Guidance))
                return hint;

            if (string.IsNullOrEmpty(hint))
                return classification.Guidance;

            return hint + " | " + classification.Guidance;
        }

        private static PlacementCatalog Clone(PlacementCatalog catalog)
        {
            if (catalog == null)
                return null;

            return new PlacementCatalog
            {
                VanillaCandidates = ScenarioSpriteCatalogService.CloneCandidates(catalog.VanillaCandidates),
                ModdedCandidates = ScenarioSpriteCatalogService.CloneCandidates(catalog.ModdedCandidates),
                FilterSummary = catalog.FilterSummary,
                GuidanceMessage = catalog.GuidanceMessage,
                XmlPathHint = catalog.XmlPathHint,
                BlockedPeople = catalog.BlockedPeople,
                BlockedInteractiveObjects = catalog.BlockedInteractiveObjects,
                BlockedPathfindingActors = catalog.BlockedPathfindingActors,
                BlockedGameplayAssets = catalog.BlockedGameplayAssets
            };
        }
    }
}
