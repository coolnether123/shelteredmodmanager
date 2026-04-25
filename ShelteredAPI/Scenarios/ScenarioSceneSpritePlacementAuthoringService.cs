using System;
using System.Collections.Generic;
using System.Text;
using ModAPI.Core;
using ModAPI.Scenarios;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioSceneSpritePlacementAuthoringService
    {
        internal sealed class PlacementPickerModel
        {
            public List<ScenarioSpriteCatalogService.SpriteCandidate> VanillaCandidates;
            public List<ScenarioSpriteCatalogService.SpriteCandidate> ModdedCandidates;
            public SceneSpritePlacement ActivePlacement;
            public bool CanPlace;
            public string PlacementSummary;
            public string CompatibilitySummary;
            public string GuidanceMessage;
            public string XmlPathHint;
        }

        private readonly ScenarioSpriteCatalogService _catalogService;
        private readonly ScenarioAuthoringHistoryService _historyService;
        private readonly IScenarioSceneSpritePlacementEngine _sceneSpritePlacementEngine;
        private readonly IScenarioEditorService _editorService;

        public static ScenarioSceneSpritePlacementAuthoringService Instance
        {
            get { return ScenarioCompositionRoot.Resolve<ScenarioSceneSpritePlacementAuthoringService>(); }
        }

        internal ScenarioSceneSpritePlacementAuthoringService(
            ScenarioSpriteCatalogService catalogService,
            ScenarioAuthoringHistoryService historyService,
            IScenarioSceneSpritePlacementEngine sceneSpritePlacementEngine,
            IScenarioEditorService editorService)
        {
            _catalogService = catalogService;
            _historyService = historyService;
            _sceneSpritePlacementEngine = sceneSpritePlacementEngine;
            _editorService = editorService;
        }

        public PlacementPickerModel GetPickerModel(ScenarioEditorSession session, ScenarioAuthoringTarget target, string scenarioFilePath)
        {
            PlacementPickerModel model = new PlacementPickerModel
            {
                VanillaCandidates = new List<ScenarioSpriteCatalogService.SpriteCandidate>(),
                ModdedCandidates = new List<ScenarioSpriteCatalogService.SpriteCandidate>(),
                CanPlace = target != null
            };

            ScenarioSpriteCatalogService.PlacementCatalog catalog = _catalogService.GetPlacementCatalog(session, scenarioFilePath);
            if (catalog != null)
            {
                model.VanillaCandidates.AddRange(catalog.VanillaCandidates);
                model.ModdedCandidates.AddRange(catalog.ModdedCandidates);
                model.CompatibilitySummary = catalog.FilterSummary;
                model.GuidanceMessage = catalog.GuidanceMessage;
                model.XmlPathHint = catalog.XmlPathHint;
            }

            SceneSpritePlacement activePlacement = FindPlacement(session != null ? session.WorkingDefinition : null, target);
            model.ActivePlacement = activePlacement;
            model.PlacementSummary = activePlacement != null
                ? "Placement '" + (activePlacement.Id ?? "<placement>") + "' is selected."
                : (model.CanPlace ? "Selecting a sprite will add a snapped scene sprite placement." : "Select a tile, object, or background target to place a sprite.");
            return model;
        }

        public void Invalidate()
        {
            _catalogService.Invalidate();
        }

        public bool TryHandleAction(ScenarioAuthoringState state, string actionId, out bool handled, out string message)
        {
            handled = false;
            message = null;
            if (state == null || string.IsNullOrEmpty(actionId))
                return false;

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionSceneSpritePlacementRemove, StringComparison.Ordinal))
            {
                handled = true;
                return RemovePlacement(state, out message);
            }

            if (!actionId.StartsWith(ScenarioAuthoringActionIds.ActionSceneSpritePlacementApplyPrefix, StringComparison.Ordinal))
                return false;

            handled = true;
            string token = DecodeActionToken(actionId.Substring(ScenarioAuthoringActionIds.ActionSceneSpritePlacementApplyPrefix.Length));
            if (string.IsNullOrEmpty(token))
            {
                message = "Scene sprite selection could not be decoded.";
                return false;
            }

            return ApplyPlacement(state, token, out message);
        }

        public static string BuildApplyActionId(string token)
        {
            if (string.IsNullOrEmpty(token))
                return ScenarioAuthoringActionIds.ActionSceneSpritePlacementApplyPrefix;

            byte[] bytes = Encoding.UTF8.GetBytes(token);
            return ScenarioAuthoringActionIds.ActionSceneSpritePlacementApplyPrefix + Convert.ToBase64String(bytes);
        }

        private bool ApplyPlacement(ScenarioAuthoringState state, string token, out string message)
        {
            message = null;
            ScenarioEditorSession session = _editorService.CurrentSession;
            if (session == null || session.WorkingDefinition == null || state.SelectedTarget == null)
            {
                message = "Select a world target before placing a sprite.";
                return false;
            }

            PlacementPickerModel model = GetPickerModel(session, state.SelectedTarget, state.ActiveScenarioFilePath);
            ScenarioSpriteCatalogService.SpriteCandidate candidate = FindCandidate(model, token);
            if (candidate == null)
            {
                message = model != null && !string.IsNullOrEmpty(model.GuidanceMessage)
                    ? model.GuidanceMessage
                    : "The requested sprite placement candidate was not available.";
                return false;
            }

            EnsureAssetReferences(session.WorkingDefinition);
            _historyService.RecordVisualChange(session.WorkingDefinition, "Apply scene sprite placement");
            SceneSpritePlacement placement = model.ActivePlacement ?? CreatePlacement(state.SelectedTarget);
            if (model.ActivePlacement == null)
                session.WorkingDefinition.AssetReferences.SceneSpritePlacements.Add(placement);

            ApplyCandidateReference(placement, candidate);
            ApplyPlacementTransform(placement, state.SelectedTarget);
            ApplyTargetSorting(placement, state.SelectedTarget);

            MarkAssetsDirty(session);
            _sceneSpritePlacementEngine.Activate(session.WorkingDefinition, state.ActiveScenarioFilePath, null);
            Invalidate();

            message = model.ActivePlacement != null
                ? "Updated placed scene sprite '" + SafeLabel(placement.Id) + "'."
                : "Placed snapped scene sprite '" + SafeLabel(candidate.Label) + "'.";
            MMLog.WriteInfo("[ScenarioSceneSpritePlacementAuthoring] " + message);
            return true;
        }

        private bool RemovePlacement(ScenarioAuthoringState state, out string message)
        {
            message = null;
            ScenarioEditorSession session = _editorService.CurrentSession;
            if (session == null || session.WorkingDefinition == null || state.SelectedTarget == null)
            {
                message = "No scene sprite placement is selected.";
                return false;
            }

            SceneSpritePlacement placement = FindPlacement(session.WorkingDefinition, state.SelectedTarget);
            if (placement == null)
            {
                message = "The selected target is not an authored scene sprite placement.";
                return false;
            }

            _historyService.RecordVisualChange(session.WorkingDefinition, "Remove scene sprite placement");
            session.WorkingDefinition.AssetReferences.SceneSpritePlacements.Remove(placement);
            MarkAssetsDirty(session);
            _sceneSpritePlacementEngine.Activate(session.WorkingDefinition, state.ActiveScenarioFilePath, null);
            Invalidate();
            message = "Removed scene sprite placement '" + SafeLabel(placement.Id) + "'.";
            MMLog.WriteInfo("[ScenarioSceneSpritePlacementAuthoring] " + message);
            return true;
        }

        private static ScenarioSpriteCatalogService.SpriteCandidate FindCandidate(PlacementPickerModel model, string token)
        {
            ScenarioSpriteCatalogService.SpriteCandidate candidate = FindCandidate(model != null ? model.VanillaCandidates : null, token);
            if (candidate != null)
                return candidate;

            return FindCandidate(model != null ? model.ModdedCandidates : null, token);
        }

        private static ScenarioSpriteCatalogService.SpriteCandidate FindCandidate(List<ScenarioSpriteCatalogService.SpriteCandidate> candidates, string token)
        {
            for (int i = 0; candidates != null && i < candidates.Count; i++)
            {
                ScenarioSpriteCatalogService.SpriteCandidate candidate = candidates[i];
                if (candidate != null && string.Equals(candidate.Token, token, StringComparison.Ordinal))
                    return candidate;
            }

            return null;
        }

        private static SceneSpritePlacement FindPlacement(ScenarioDefinition definition, ScenarioAuthoringTarget target)
        {
            if (definition == null
                || definition.AssetReferences == null
                || definition.AssetReferences.SceneSpritePlacements == null
                || target == null
                || string.IsNullOrEmpty(target.ScenarioReferenceId))
            {
                return null;
            }

            for (int i = 0; i < definition.AssetReferences.SceneSpritePlacements.Count; i++)
            {
                SceneSpritePlacement placement = definition.AssetReferences.SceneSpritePlacements[i];
                if (placement != null && string.Equals(placement.Id, target.ScenarioReferenceId, StringComparison.OrdinalIgnoreCase))
                    return placement;
            }

            return null;
        }

        private static SceneSpritePlacement CreatePlacement(ScenarioAuthoringTarget target)
        {
            SceneSpritePlacement placement = new SceneSpritePlacement();
            placement.Id = "scene_sprite_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            ApplyPlacementTransform(placement, target);
            return placement;
        }

        private static void ApplyCandidateReference(SceneSpritePlacement placement, ScenarioSpriteCatalogService.SpriteCandidate candidate)
        {
            placement.SpriteId = null;
            placement.RelativePath = null;
            placement.RuntimeSpriteKey = null;

            if (candidate.SourceKind == ScenarioSpriteCatalogService.SpriteCandidateSourceKind.VanillaRuntime)
                placement.RuntimeSpriteKey = candidate.RuntimeSpriteKey;
            else
            {
                placement.SpriteId = candidate.SpriteId;
                placement.RelativePath = candidate.RelativePath;
            }
        }

        private static void ApplyPlacementTransform(SceneSpritePlacement placement, ScenarioAuthoringTarget target)
        {
            Vector3 snappedPosition;
            int gridX;
            int gridY;
            if (target != null && target.GridX.HasValue && target.GridY.HasValue)
            {
                placement.SnapToGrid = true;
                placement.GridX = target.GridX;
                placement.GridY = target.GridY;
                snappedPosition = ScenarioGridSnapService.GetCellCenterWorldPosition(target.GridX.Value, target.GridY.Value);
            }
            else if (target != null && ScenarioGridSnapService.TrySnapWorldPosition(target.WorldPosition, out gridX, out gridY, out snappedPosition))
            {
                placement.SnapToGrid = true;
                placement.GridX = gridX;
                placement.GridY = gridY;
            }
            else
            {
                placement.SnapToGrid = false;
                placement.GridX = null;
                placement.GridY = null;
                snappedPosition = target != null ? target.WorldPosition : Vector3.zero;
            }

            placement.Position = new ScenarioVector3
            {
                X = snappedPosition.x,
                Y = snappedPosition.y,
                Z = snappedPosition.z
            };
        }

        private static void ApplyTargetSorting(SceneSpritePlacement placement, ScenarioAuthoringTarget target)
        {
            if (placement == null)
                return;

            SpriteRenderer spriteRenderer = ResolveSpriteRenderer(target);
            if (spriteRenderer != null)
            {
                placement.SortingLayerName = spriteRenderer.sortingLayerName;
                placement.SortingOrder = spriteRenderer.sortingOrder + 1;
                return;
            }

            placement.SortingLayerName = string.IsNullOrEmpty(placement.SortingLayerName) ? "Default" : placement.SortingLayerName;
        }

        private static SpriteRenderer ResolveSpriteRenderer(ScenarioAuthoringTarget target)
        {
            if (target == null || target.RuntimeObject == null)
                return null;

            GameObject gameObject = target.RuntimeObject as GameObject;
            if (gameObject != null)
                return gameObject.GetComponent<SpriteRenderer>() ?? gameObject.GetComponentInChildren<SpriteRenderer>(true);

            Component component = target.RuntimeObject as Component;
            return component != null ? (component.GetComponent<SpriteRenderer>() ?? component.GetComponentInChildren<SpriteRenderer>(true)) : null;
        }

        private static void EnsureAssetReferences(ScenarioDefinition definition)
        {
            if (definition == null)
                return;

            if (definition.AssetReferences == null)
                definition.AssetReferences = new AssetReferencesDefinition();
        }

        private static void MarkAssetsDirty(ScenarioEditorSession session)
        {
            if (session == null)
                return;

            if (!session.DirtyFlags.Contains(ScenarioDirtySection.Assets))
                session.DirtyFlags.Add(ScenarioDirtySection.Assets);

            session.CurrentEditCategory = ScenarioEditCategory.Assets;
            session.HasAppliedToCurrentWorld = true;
        }

        private static string DecodeActionToken(string encoded)
        {
            if (string.IsNullOrEmpty(encoded))
                return null;

            try
            {
                byte[] bytes = Convert.FromBase64String(encoded);
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return null;
            }
        }

        private static string SafeLabel(string value)
        {
            return string.IsNullOrEmpty(value) ? "<scene sprite>" : value;
        }
    }
}
