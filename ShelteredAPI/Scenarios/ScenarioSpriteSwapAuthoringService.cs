using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ModAPI.Core;
using ModAPI.Scenarios;
using ShelteredAPI.Content;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    // Thin orchestrator for the sprite-swap authoring workflow. Catalog queries live
    // in ScenarioSpriteCatalogService; rule mutation in ScenarioSpriteSwapRuleEditor;
    // undo/redo in ScenarioAuthoringHistoryService; clipboard in
    // ScenarioSpriteSwapClipboard. This class composes them and translates authoring
    // actions into preview or persistence steps.
    internal sealed class ScenarioSpriteSwapAuthoringService
    {
        internal enum CustomEditorTool
        {
            Paint = 0,
            Pick = 1,
            Select = 2
        }

        internal sealed class SpritePickerModel
        {
            public ScenarioSpriteRuntimeResolver.ResolvedTarget Target;
            public List<ScenarioSpriteCatalogService.SpriteCandidate> VanillaCandidates;
            public List<ScenarioSpriteCatalogService.SpriteCandidate> ModdedCandidates;
            public bool HasActiveRule;
            public string ActiveRuleSummary;
            public string ActiveCandidateToken;
            public string ActiveCandidateLabel;
            public bool FamilyFiltered;
            public string CompatibilitySummary;
            public string GuidanceMessage;
            public string XmlPathHint;
        }

        internal sealed class CustomEditorModel
        {
            public bool Visible;
            public Sprite PreviewSprite;
            public int Width;
            public int Height;
            public int Zoom;
            public Color[] BrushPalette;
            public int ActiveBrushIndex;
            public Color ActiveColor;
            public string ActiveColorHex;
            public CustomEditorTool ActiveTool;
            public bool HasSelection;
            public int SelectionX;
            public int SelectionY;
            public int SelectionWidth;
            public int SelectionHeight;
            public bool HasClipboard;
            public int ClipboardWidth;
            public int ClipboardHeight;
            public bool Dirty;
            public bool Checkerboard;
            public string SourceLabel;
            public bool IsCharacterEditor;
            public ScenarioCharacterTexturePart CharacterPart;
            public string CharacterPartLabel;
        }

        private sealed class PreviewSession
        {
            public string TargetPath;
            public ScenarioSpriteTargetComponentKind TargetKind;
            public Sprite BaselineSprite;
        }

        private sealed class CustomEditorSession
        {
            public string TargetPath;
            public string SourceLabel;
            public Texture2D BaselineTexture;
            public Texture2D Texture;
            public Sprite PreviewSprite;
            public Color ActiveColor;
            public int ActiveBrushIndex;
            public CustomEditorTool ActiveTool;
            public bool Dirty;
            public string CustomSpriteId;
            public string BaseSpriteId;
            public string BaseRelativePath;
            public string BaseRuntimeSpriteKey;
            public bool HasSelection;
            public int SelectionX;
            public int SelectionY;
            public int SelectionWidth;
            public int SelectionHeight;
            public bool SelectionDragActive;
            public int SelectionAnchorX;
            public int SelectionAnchorY;
            public int LastInteractionX;
            public int LastInteractionY;
            public bool IsCharacterEditor;
            public ScenarioCharacterTexturePart CharacterPart;
            public int CharacterFamilyIndex;
        }

        private static readonly Color[] _brushPalette = new Color[]
        {
            new Color32(0, 0, 0, 255),
            new Color32(255, 255, 255, 255),
            new Color32(211, 74, 68, 255),
            new Color32(90, 170, 92, 255),
            new Color32(72, 116, 204, 255),
            new Color32(234, 210, 98, 255),
            new Color32(193, 121, 222, 255),
            new Color32(92, 199, 209, 255),
            new Color32(0, 0, 0, 0)
        };
        private readonly ScenarioSpriteCatalogService _catalogService;
        private readonly ScenarioCharacterAppearanceService _characterAppearanceService;
        private readonly ScenarioSpriteRuntimeResolver _runtimeResolver;
        private readonly SpritePatchBuilder _spritePatchBuilder;
        private readonly ScenarioAuthoringHistoryService _historyService;
        private readonly IScenarioSpriteSwapEngine _spriteSwapEngine;
        private readonly IScenarioSceneSpritePlacementEngine _sceneSpritePlacementEngine;
        private readonly IScenarioEditorService _editorService;
        private PreviewSession _previewSession;
        private ScenarioCharacterAppearanceService.PreviewSession _characterPreviewSession;
        private CustomEditorSession _customEditorSession;
        private Color[] _customClipboardPixels;
        private int _customClipboardWidth;
        private int _customClipboardHeight;

        public static ScenarioSpriteSwapAuthoringService Instance
        {
            get { return ScenarioCompositionRoot.Resolve<ScenarioSpriteSwapAuthoringService>(); }
        }

        internal ScenarioSpriteSwapAuthoringService(
            ScenarioSpriteCatalogService catalogService,
            ScenarioCharacterAppearanceService characterAppearanceService,
            ScenarioSpriteRuntimeResolver runtimeResolver,
            SpritePatchBuilder spritePatchBuilder,
            ScenarioAuthoringHistoryService historyService,
            IScenarioSpriteSwapEngine spriteSwapEngine,
            IScenarioSceneSpritePlacementEngine sceneSpritePlacementEngine,
            IScenarioEditorService editorService)
        {
            _catalogService = catalogService;
            _characterAppearanceService = characterAppearanceService;
            _runtimeResolver = runtimeResolver;
            _spritePatchBuilder = spritePatchBuilder;
            _historyService = historyService;
            _spriteSwapEngine = spriteSwapEngine;
            _sceneSpritePlacementEngine = sceneSpritePlacementEngine;
            _editorService = editorService;
        }

        public SpritePickerModel GetPickerModel(ScenarioEditorSession session, ScenarioAuthoringTarget target, string scenarioFilePath)
        {
            ScenarioSpriteCatalogService.SpriteCatalog catalog = _catalogService.GetCatalog(session, target, scenarioFilePath);
            if (catalog == null || catalog.Target == null)
                return null;

            SpritePickerModel model = new SpritePickerModel
            {
                Target = catalog.Target,
                VanillaCandidates = CloneCandidates(catalog.VanillaCandidates),
                ModdedCandidates = CloneCandidates(catalog.ModdedCandidates),
                FamilyFiltered = catalog.FamilyFiltered,
                CompatibilitySummary = catalog.FilterSummary,
                GuidanceMessage = catalog.GuidanceMessage,
                XmlPathHint = catalog.XmlPathHint
            };

            SpriteSwapRule activeRule = ScenarioSpriteSwapRuleEditor.FindActiveRule(
                session != null ? session.WorkingDefinition : null,
                catalog.Target.TargetPath,
                GetCurrentDay());
            model.HasActiveRule = activeRule != null;
            model.ActiveRuleSummary = ScenarioSpriteSwapRuleEditor.DescribeRule(activeRule);
            model.ActiveCandidateToken = FindMatchingCandidateToken(model, activeRule);
            model.ActiveCandidateLabel = FindCandidateLabel(model, model.ActiveCandidateToken);
            AnnotateCandidateHints(model.VanillaCandidates, model.ActiveCandidateToken);
            AnnotateCandidateHints(model.ModdedCandidates, model.ActiveCandidateToken);
            return model;
        }

        public CustomEditorModel GetCustomEditorModel(ScenarioAuthoringState state)
        {
            if (!HasCustomEditor(state))
                return null;

            int zoom = state != null && state.Settings != null
                ? Mathf.Clamp(state.Settings.GetInt("sprite.zoom", 8), 1, 48)
                : 8;
            Color initialColor = _customEditorSession.ActiveColor;
            return new CustomEditorModel
            {
                Visible = true,
                PreviewSprite = _customEditorSession.PreviewSprite,
                Width = _customEditorSession.Texture != null ? _customEditorSession.Texture.width : 0,
                Height = _customEditorSession.Texture != null ? _customEditorSession.Texture.height : 0,
                Zoom = zoom,
                BrushPalette = CloneBrushPalette(),
                ActiveBrushIndex = _customEditorSession.ActiveBrushIndex,
                ActiveColor = initialColor,
                ActiveColorHex = EncodeColor(initialColor),
                ActiveTool = _customEditorSession.ActiveTool,
                HasSelection = _customEditorSession.HasSelection,
                SelectionX = _customEditorSession.SelectionX,
                SelectionY = _customEditorSession.SelectionY,
                SelectionWidth = _customEditorSession.SelectionWidth,
                SelectionHeight = _customEditorSession.SelectionHeight,
                HasClipboard = _customClipboardPixels != null && _customClipboardPixels.Length > 0 && _customClipboardWidth > 0 && _customClipboardHeight > 0,
                ClipboardWidth = _customClipboardWidth,
                ClipboardHeight = _customClipboardHeight,
                Dirty = _customEditorSession.Dirty,
                Checkerboard = state != null && state.Settings != null && state.Settings.GetBool("sprite.checkerboard", true),
                SourceLabel = _customEditorSession.SourceLabel,
                IsCharacterEditor = _customEditorSession.IsCharacterEditor,
                CharacterPart = _customEditorSession.CharacterPart,
                CharacterPartLabel = _customEditorSession.IsCharacterEditor
                    ? ScenarioCharacterAppearanceService.BuildPartLabel(_customEditorSession.CharacterPart)
                    : null
            };
        }

        public void Invalidate()
        {
            _catalogService.Invalidate();
        }

        public void ResetTransientState(bool restorePreview)
        {
            if (restorePreview)
            {
                RestorePreviewSession();
                RestoreCharacterPreviewSession();
            }

            ClearCustomEditorSession();
            ClearPreviewSession();
            ClearCharacterPreviewSession();
            ClearCustomClipboard();
            _catalogService.Invalidate();
        }

        public bool SynchronizePicker(ScenarioAuthoringState state, out string message)
        {
            message = null;
            if (!IsPickerOpen(state))
                return false;

            if (state.ActiveTool != ScenarioAuthoringTool.Assets
                || state.AssetMode != ScenarioAssetAuthoringMode.ReplaceExisting)
            {
                ClosePickerState(state, true);
                message = "Sprite picker closed because the asset workflow changed.";
                return true;
            }

            if (state.SelectedTarget == null || !AreSameTarget(state.SelectedTarget, state.SpriteSwapPicker.Target))
            {
                ClosePickerState(state, true);
                message = "Sprite picker closed because the selected target changed.";
                return true;
            }

            return false;
        }

        public bool TryHandleAction(ScenarioAuthoringState state, string actionId, out bool handled, out string message)
        {
            handled = false;
            message = null;
            if (state == null || string.IsNullOrEmpty(actionId))
                return false;

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapPickerOpen, StringComparison.Ordinal))
            {
                handled = true;
                return OpenPicker(state, out message);
            }

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapCharacterPartHead, StringComparison.Ordinal))
            {
                handled = true;
                return SwitchCharacterPart(state, ScenarioCharacterTexturePart.Head, out message);
            }

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapCharacterPartTorso, StringComparison.Ordinal))
            {
                handled = true;
                return SwitchCharacterPart(state, ScenarioCharacterTexturePart.Torso, out message);
            }

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapCharacterPartLegs, StringComparison.Ordinal))
            {
                handled = true;
                return SwitchCharacterPart(state, ScenarioCharacterTexturePart.Legs, out message);
            }

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapCustomZoomIn, StringComparison.Ordinal))
            {
                handled = true;
                return AdjustEditorZoom(state, +1, out message);
            }

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapCustomZoomOut, StringComparison.Ordinal))
            {
                handled = true;
                return AdjustEditorZoom(state, -1, out message);
            }

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapCustomZoomReset, StringComparison.Ordinal))
            {
                handled = true;
                return ResetEditorZoom(state, out message);
            }

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapPickerSave, StringComparison.Ordinal))
            {
                handled = true;
                return SavePicker(state, out message);
            }

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapPickerCancel, StringComparison.Ordinal))
            {
                handled = true;
                return CancelPicker(state, out message);
            }

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapCustomEditStart, StringComparison.Ordinal))
            {
                handled = true;
                return BeginCustomEdit(state, out message);
            }

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapCustomEditDiscard, StringComparison.Ordinal))
            {
                handled = true;
                return DiscardCustomEdit(state, out message);
            }

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapCustomToolPaint, StringComparison.Ordinal))
            {
                handled = true;
                return SelectCustomTool(state, CustomEditorTool.Paint, out message);
            }

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapCustomToolPick, StringComparison.Ordinal))
            {
                handled = true;
                return SelectCustomTool(state, CustomEditorTool.Pick, out message);
            }

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapCustomToolSelect, StringComparison.Ordinal))
            {
                handled = true;
                return SelectCustomTool(state, CustomEditorTool.Select, out message);
            }

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapCustomSelectionClear, StringComparison.Ordinal))
            {
                handled = true;
                return ClearCustomSelection(state, out message);
            }

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapCustomCopy, StringComparison.Ordinal))
            {
                handled = true;
                return CopyCustomPixels(state, out message);
            }

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapCustomPaste, StringComparison.Ordinal))
            {
                handled = true;
                return PasteCustomPixels(state, out message);
            }

            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionSpriteSwapCustomBrushPrefix, StringComparison.Ordinal))
            {
                handled = true;
                int brushIndex;
                if (!int.TryParse(actionId.Substring(ScenarioAuthoringActionIds.ActionSpriteSwapCustomBrushPrefix.Length), out brushIndex))
                {
                    message = "Custom sprite brush selection could not be decoded.";
                    return false;
                }

                return SelectCustomBrush(state, brushIndex, out message);
            }

            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionSpriteSwapCustomPresetPrefix, StringComparison.Ordinal))
            {
                handled = true;
                int presetIndex;
                if (!int.TryParse(actionId.Substring(ScenarioAuthoringActionIds.ActionSpriteSwapCustomPresetPrefix.Length), out presetIndex))
                {
                    message = "Custom color preset could not be decoded.";
                    return false;
                }

                return SelectCustomPreset(state, presetIndex, out message);
            }

            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionSpriteSwapCustomPaintPrefix, StringComparison.Ordinal))
            {
                handled = true;
                int pixelX;
                int pixelY;
                if (!TryDecodePixel(actionId.Substring(ScenarioAuthoringActionIds.ActionSpriteSwapCustomPaintPrefix.Length), out pixelX, out pixelY))
                {
                    message = "Custom sprite paint coordinates could not be decoded.";
                    return false;
                }

                return PaintCustomPixel(state, pixelX, pixelY, out message);
            }

            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionSpriteSwapCustomPickPrefix, StringComparison.Ordinal))
            {
                handled = true;
                int pixelX;
                int pixelY;
                if (!TryDecodePixel(actionId.Substring(ScenarioAuthoringActionIds.ActionSpriteSwapCustomPickPrefix.Length), out pixelX, out pixelY))
                {
                    message = "Custom sprite pick coordinates could not be decoded.";
                    return false;
                }

                return PickCustomColor(state, pixelX, pixelY, out message);
            }

            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionSpriteSwapCustomSelectStartPrefix, StringComparison.Ordinal))
            {
                handled = true;
                int pixelX;
                int pixelY;
                if (!TryDecodePixel(actionId.Substring(ScenarioAuthoringActionIds.ActionSpriteSwapCustomSelectStartPrefix.Length), out pixelX, out pixelY))
                {
                    message = "Custom sprite selection start could not be decoded.";
                    return false;
                }

                return StartCustomSelection(state, pixelX, pixelY, out message);
            }

            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionSpriteSwapCustomSelectDragPrefix, StringComparison.Ordinal))
            {
                handled = true;
                int pixelX;
                int pixelY;
                if (!TryDecodePixel(actionId.Substring(ScenarioAuthoringActionIds.ActionSpriteSwapCustomSelectDragPrefix.Length), out pixelX, out pixelY))
                {
                    message = "Custom sprite selection drag could not be decoded.";
                    return false;
                }

                return DragCustomSelection(state, pixelX, pixelY, out message);
            }

            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionSpriteSwapCustomSelectEndPrefix, StringComparison.Ordinal))
            {
                handled = true;
                int pixelX;
                int pixelY;
                if (!TryDecodePixel(actionId.Substring(ScenarioAuthoringActionIds.ActionSpriteSwapCustomSelectEndPrefix.Length), out pixelX, out pixelY))
                {
                    message = "Custom sprite selection end could not be decoded.";
                    return false;
                }

                return EndCustomSelection(state, pixelX, pixelY, out message);
            }

            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionSpriteSwapCustomColorPrefix, StringComparison.Ordinal))
            {
                handled = true;
                Color color;
                if (!TryDecodeColor(actionId.Substring(ScenarioAuthoringActionIds.ActionSpriteSwapCustomColorPrefix.Length), out color))
                {
                    message = "Custom sprite color could not be decoded.";
                    return false;
                }

                return SetCustomColor(state, color, -1, out message);
            }

            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionSpriteSwapPreviewPrefix, StringComparison.Ordinal))
            {
                handled = true;
                string previewToken = DecodeActionToken(actionId.Substring(ScenarioAuthoringActionIds.ActionSpriteSwapPreviewPrefix.Length));
                if (string.IsNullOrEmpty(previewToken))
                {
                    message = "Sprite preview selection could not be decoded.";
                    return false;
                }

                return PreviewCandidate(state, previewToken, out message);
            }

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapClear, StringComparison.Ordinal)
                || string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapRevert, StringComparison.Ordinal))
            {
                handled = true;
                if (HasCustomEditor(state))
                    return DiscardCustomEdit(state, out message);
                return ClearActiveSwap(state, out message);
            }

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapCopy, StringComparison.Ordinal))
            {
                handled = true;
                if (HasCustomEditor(state))
                    return CopyCustomPixels(state, out message);
                return CopyActiveSwap(state, out message);
            }

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapPaste, StringComparison.Ordinal))
            {
                handled = true;
                if (HasCustomEditor(state))
                    return PasteCustomPixels(state, out message);
                return PasteSwap(state, out message);
            }

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionHistoryUndo, StringComparison.Ordinal))
            {
                handled = true;
                return Undo(state, out message);
            }

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionHistoryRedo, StringComparison.Ordinal))
            {
                handled = true;
                return Redo(state, out message);
            }

            if (!actionId.StartsWith(ScenarioAuthoringActionIds.ActionSpriteSwapApplyPrefix, StringComparison.Ordinal))
                return false;

            handled = true;
            string token = DecodeActionToken(actionId.Substring(ScenarioAuthoringActionIds.ActionSpriteSwapApplyPrefix.Length));
            if (string.IsNullOrEmpty(token))
            {
                message = "Sprite swap selection could not be decoded.";
                return false;
            }

            return ApplyCandidateImmediately(state, token, out message);
        }

        public static string BuildApplyActionId(string token)
        {
            if (string.IsNullOrEmpty(token))
                return ScenarioAuthoringActionIds.ActionSpriteSwapApplyPrefix;

            byte[] bytes = Encoding.UTF8.GetBytes(token);
            return ScenarioAuthoringActionIds.ActionSpriteSwapApplyPrefix + Convert.ToBase64String(bytes);
        }

        public static string BuildPreviewActionId(string token)
        {
            if (string.IsNullOrEmpty(token))
                return ScenarioAuthoringActionIds.ActionSpriteSwapPreviewPrefix;

            byte[] bytes = Encoding.UTF8.GetBytes(token);
            return ScenarioAuthoringActionIds.ActionSpriteSwapPreviewPrefix + Convert.ToBase64String(bytes);
        }

        public static string BuildCustomBrushActionId(int brushIndex)
        {
            return ScenarioAuthoringActionIds.ActionSpriteSwapCustomBrushPrefix + brushIndex;
        }

        public static string BuildCustomPaintActionId(int pixelX, int pixelY)
        {
            return ScenarioAuthoringActionIds.ActionSpriteSwapCustomPaintPrefix + pixelX + "," + pixelY;
        }

        public static string BuildCustomPickActionId(int pixelX, int pixelY)
        {
            return ScenarioAuthoringActionIds.ActionSpriteSwapCustomPickPrefix + pixelX + "," + pixelY;
        }

        public static string BuildCustomPresetActionId(int brushIndex)
        {
            return ScenarioAuthoringActionIds.ActionSpriteSwapCustomPresetPrefix + brushIndex;
        }

        public static string BuildCustomColorActionId(Color color)
        {
            return ScenarioAuthoringActionIds.ActionSpriteSwapCustomColorPrefix + EncodeColor(color);
        }

        public static string BuildCustomSelectStartActionId(int pixelX, int pixelY)
        {
            return ScenarioAuthoringActionIds.ActionSpriteSwapCustomSelectStartPrefix + pixelX + "," + pixelY;
        }

        public static string BuildCustomSelectDragActionId(int pixelX, int pixelY)
        {
            return ScenarioAuthoringActionIds.ActionSpriteSwapCustomSelectDragPrefix + pixelX + "," + pixelY;
        }

        public static string BuildCustomSelectEndActionId(int pixelX, int pixelY)
        {
            return ScenarioAuthoringActionIds.ActionSpriteSwapCustomSelectEndPrefix + pixelX + "," + pixelY;
        }

        private bool OpenPicker(ScenarioAuthoringState state, out string message)
        {
            message = null;
            ScenarioEditorSession session = _editorService.CurrentSession;
            if (session == null || session.WorkingDefinition == null)
            {
                message = "No active authoring session is available.";
                return false;
            }

            if (state.SelectedTarget == null)
            {
                message = "Select a visual target before opening the sprite picker.";
                return false;
            }

            ScenarioCharacterAppearanceService.ResolvedCharacterTarget characterTarget;
            if (_characterAppearanceService.TryResolve(state.SelectedTarget, out characterTarget, out message))
            {
                ClosePickerState(state, true);
                state.SpriteSwapPicker = new ScenarioSpriteSwapPickerState
                {
                    IsOpen = true,
                    Target = state.SelectedTarget.Copy(),
                    TargetPath = !string.IsNullOrEmpty(state.SelectedTarget.TransformPath) ? state.SelectedTarget.TransformPath : characterTarget.TargetPath
                };
                _characterPreviewSession = _characterAppearanceService.CapturePreview(characterTarget);
                return OpenCharacterEditor(state, characterTarget, ScenarioCharacterTexturePart.Head, out message);
            }

            SpritePickerModel model = GetPickerModel(session, state.SelectedTarget, state.ActiveScenarioFilePath);
            if (model == null || model.Target == null)
            {
                message = "The selected target does not expose compatible sprite replacements.";
                return false;
            }

            ClosePickerState(state, true);
            state.SpriteSwapPicker = new ScenarioSpriteSwapPickerState
            {
                IsOpen = true,
                Target = state.SelectedTarget.Copy(),
                TargetPath = model.Target.TargetPath,
                SavedCandidateToken = model.ActiveCandidateToken,
                SavedCandidateLabel = model.ActiveCandidateLabel,
                PreviewCandidateToken = model.ActiveCandidateToken,
                PreviewCandidateLabel = model.ActiveCandidateLabel
            };

            message = "Sprite picker opened for '" + SafeLabel(state.SelectedTarget.DisplayName) + "'.";
            return true;
        }

        private bool PreviewCandidate(ScenarioAuthoringState state, string token, out string message)
        {
            message = null;
            ScenarioEditorSession session;
            SpritePickerModel model;
            if (!TryGetOpenPickerModel(state, out session, out model, out message))
                return false;

            ScenarioSpriteCatalogService.SpriteCandidate candidate = FindCandidate(model, token);
            if (candidate == null || candidate.Sprite == null)
            {
                message = "The requested sprite preview is no longer available.";
                return false;
            }

            ClearCustomEditorSession();
            if (!EnsurePreviewSession(model.Target, out message))
                return false;

            if (!ScenarioSpriteRuntimeMutationService.TryApply(model.Target, candidate.Sprite))
            {
                message = "The selected sprite could not be previewed on this target.";
                return false;
            }

            state.SpriteSwapPicker.PreviewCandidateToken = candidate.Token;
            state.SpriteSwapPicker.PreviewCandidateLabel = candidate.Label;
            message = "Previewing '" + SafeLabel(candidate.Label) + "' on '" + SafeLabel(state.SpriteSwapPicker.Target.DisplayName) + "'.";
            return true;
        }

        private bool BeginCustomEdit(ScenarioAuthoringState state, out string message)
        {
            message = null;
            ScenarioEditorSession session;
            SpritePickerModel model;
            if (!TryGetOpenPickerModel(state, out session, out model, out message))
                return false;

            Sprite sourceSprite = model.Target.CurrentSprite;
            string sourceLabel = "Current Sprite";
            ScenarioSpriteCatalogService.SpriteCandidate sourceCandidate = FindCandidate(model, state.SpriteSwapPicker.PreviewCandidateToken);
            if (sourceCandidate != null && sourceCandidate.Sprite != null)
            {
                sourceSprite = sourceCandidate.Sprite;
                sourceLabel = CleanCandidateLabel(sourceCandidate.Label);
            }

            if (sourceSprite == null)
            {
                message = "No source sprite is available to start a custom edit.";
                return false;
            }

            if (!EnsurePreviewSession(model.Target, out message))
                return false;

            Texture2D editableTexture = CreateEditableTexture(sourceSprite);
            if (editableTexture == null)
            {
                message = "The selected sprite could not be copied into the custom editor.";
                return false;
            }

            ClearCustomEditorSession();
            Sprite previewSprite = CreatePreviewSprite(editableTexture, sourceSprite);
            string customSpriteId = BuildCustomSpriteId(model.Target.TargetPath);
            Color initialColor = FindInitialBrushColor(editableTexture);
            Texture2D baselineTexture = CreateEditableTexture(sourceSprite);
            _customEditorSession = new CustomEditorSession
            {
                TargetPath = model.Target.TargetPath,
                SourceLabel = sourceLabel,
                BaselineTexture = baselineTexture,
                Texture = editableTexture,
                PreviewSprite = previewSprite,
                ActiveColor = initialColor,
                ActiveBrushIndex = FindMatchingBrushIndex(initialColor),
                ActiveTool = CustomEditorTool.Paint,
                Dirty = false,
                CustomSpriteId = customSpriteId,
                BaseSpriteId = sourceCandidate != null ? sourceCandidate.SpriteId : null,
                BaseRelativePath = sourceCandidate != null ? sourceCandidate.RelativePath : null,
                BaseRuntimeSpriteKey = sourceCandidate != null ? sourceCandidate.RuntimeSpriteKey : null,
                LastInteractionX = 0,
                LastInteractionY = 0
            };

            ScenarioSpriteRuntimeMutationService.TryApply(model.Target, previewSprite);
            state.SpriteSwapPicker.PreviewCandidateToken = null;
            state.SpriteSwapPicker.PreviewCandidateLabel = "Custom Sprite Draft";
            message = "Custom pixel editor opened from '" + SafeLabel(sourceLabel) + "'.";
            return true;
        }

        private bool OpenCharacterEditor(
            ScenarioAuthoringState state,
            ScenarioCharacterAppearanceService.ResolvedCharacterTarget target,
            ScenarioCharacterTexturePart part,
            out string message)
        {
            message = null;
            if (state == null || target == null)
            {
                message = "Character editor could not be opened.";
                return false;
            }

            Texture2D editableTexture;
            string sourceId;
            string sourceLabel;
            if (!_characterAppearanceService.TryCreateEditableTexture(target, part, out editableTexture, out sourceId, out sourceLabel) || editableTexture == null)
            {
                message = "The selected " + ScenarioCharacterAppearanceService.BuildPartLabel(part).ToLowerInvariant()
                    + " texture could not be copied into the editor.";
                return false;
            }

            ClearCustomEditorSession();
            Sprite previewSprite = CreatePreviewSprite(editableTexture, null);
            string customTextureId = BuildCharacterCustomTextureId(target, part);
            Color initialColor = FindInitialBrushColor(editableTexture);
            _customEditorSession = new CustomEditorSession
            {
                TargetPath = state.SpriteSwapPicker != null ? state.SpriteSwapPicker.TargetPath : target.TargetPath,
                SourceLabel = sourceLabel + " (" + (sourceId ?? "default") + ")",
                BaselineTexture = ScenarioCharacterAppearanceService.CopyTexture(editableTexture),
                Texture = editableTexture,
                PreviewSprite = previewSprite,
                ActiveColor = initialColor,
                ActiveBrushIndex = FindMatchingBrushIndex(initialColor),
                ActiveTool = CustomEditorTool.Paint,
                Dirty = false,
                CustomSpriteId = customTextureId,
                BaseSpriteId = sourceId,
                LastInteractionX = 0,
                LastInteractionY = 0,
                IsCharacterEditor = true,
                CharacterPart = part,
                CharacterFamilyIndex = target.FamilyIndex
            };

            _characterAppearanceService.ApplyPreviewTexture(target, part, customTextureId, editableTexture, out message);
            if (!string.IsNullOrEmpty(message))
                message = null;

            message = "Character " + ScenarioCharacterAppearanceService.BuildPartLabel(part).ToLowerInvariant()
                + " editor opened for '" + SafeLabel(target.DisplayName) + "'.";
            return true;
        }

        private bool SwitchCharacterPart(ScenarioAuthoringState state, ScenarioCharacterTexturePart part, out string message)
        {
            message = null;
            if (!HasCharacterEditor(state))
            {
                message = "Character editor is not active.";
                return false;
            }

            ScenarioCharacterAppearanceService.ResolvedCharacterTarget target;
            if (!TryResolveCharacterEditorTarget(state, out target, out message))
                return false;

            if (_customEditorSession.CharacterPart == part)
                return false;

            if (_customEditorSession.Dirty)
            {
                message = "Save or cancel the current " + ScenarioCharacterAppearanceService.BuildPartLabel(_customEditorSession.CharacterPart).ToLowerInvariant()
                    + " edit before switching parts.";
                return false;
            }

            return OpenCharacterEditor(state, target, part, out message);
        }

        private bool SaveCharacterEditor(ScenarioAuthoringState state, out string message)
        {
            message = null;
            if (!HasCharacterEditor(state) || _customEditorSession == null || _customEditorSession.Texture == null)
            {
                message = "Character editor is not active.";
                return false;
            }

            ScenarioEditorSession session = _editorService.CurrentSession;
            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            if (definition == null)
            {
                message = "No active authoring session is available.";
                return false;
            }

            ScenarioCharacterAppearanceService.ResolvedCharacterTarget target;
            if (!TryResolveCharacterEditorTarget(state, out target, out message))
                return false;

            if (!_customEditorSession.Dirty)
            {
                ClosePickerState(state, false);
                message = "Closed the character editor for '" + SafeLabel(target.DisplayName) + "' without changes.";
                return true;
            }

            string packRoot = !string.IsNullOrEmpty(state.ActiveScenarioFilePath)
                ? Path.GetDirectoryName(state.ActiveScenarioFilePath)
                : null;
            if (string.IsNullOrEmpty(packRoot))
            {
                message = "Scenario pack path is unavailable, so the character texture could not be saved.";
                return false;
            }

            try
            {
                string customTextureId = _customEditorSession.CustomSpriteId;
                _historyService.RecordVisualChange(
                    definition,
                    "Apply character " + ScenarioCharacterAppearanceService.BuildPartLabel(_customEditorSession.CharacterPart).ToLowerInvariant()
                    + " texture to " + SafeLabel(target.DisplayName));

                string patchId = UpsertPatchSpriteAsset(definition, packRoot, customTextureId, _customEditorSession.SourceLabel);
                if (string.IsNullOrEmpty(patchId))
                {
                    message = "Character texture patch could not be generated.";
                    return false;
                }
                FamilyMemberConfig memberConfig = EnsureFamilyMemberConfig(definition, target);
                ScenarioCharacterAppearanceService.UpsertAppearance(
                    memberConfig,
                    _customEditorSession.CharacterPart,
                    customTextureId,
                    null);

                string applyMessage;
                _characterAppearanceService.ApplyConfiguredAppearance(
                    definition,
                    state.ActiveScenarioFilePath,
                    memberConfig,
                    target.FamilyMember,
                    out applyMessage);

                MarkFamilyDirty(session);
                ClosePickerState(state, false);
                message = "Saved character " + ScenarioCharacterAppearanceService.BuildPartLabel(_customEditorSession.CharacterPart).ToLowerInvariant()
                    + " patch '" + patchId + "' onto '" + SafeLabel(target.DisplayName) + "'.";
                MMLog.WriteInfo("[ScenarioSpriteSwapAuthoring] " + message);
                return true;
            }
            catch (Exception ex)
            {
                message = "Character texture save failed: " + ex.Message;
                return false;
            }
        }

        private bool AdjustEditorZoom(ScenarioAuthoringState state, int delta, out string message)
        {
            message = null;
            if (!HasCustomEditor(state) || state == null || state.Settings == null)
                return false;

            int current = state.Settings.GetInt("sprite.zoom", 8);
            int next = Mathf.Clamp(current + delta, 1, 48);
            if (next == current)
                return false;

            state.Settings.Set("sprite.zoom", next.ToString());
            message = "Canvas zoom set to " + next + "x.";
            return true;
        }

        private bool ResetEditorZoom(ScenarioAuthoringState state, out string message)
        {
            message = null;
            if (!HasCustomEditor(state) || state == null || state.Settings == null)
                return false;

            int current = state.Settings.GetInt("sprite.zoom", 8);
            if (current == 8)
                return false;

            state.Settings.Set("sprite.zoom", "8");
            message = "Canvas zoom reset to 8x.";
            return true;
        }

        private bool DiscardCustomEdit(ScenarioAuthoringState state, out string message)
        {
            message = null;
            if (!HasCustomEditor(state))
            {
                message = "Custom sprite editor is not active.";
                return false;
            }

            if (HasCharacterEditor(state))
            {
                string targetDisplay = state != null && state.SpriteSwapPicker != null && state.SpriteSwapPicker.Target != null
                    ? state.SpriteSwapPicker.Target.DisplayName
                    : "character";
                ClosePickerState(state, true);
                message = "Discarded character texture edits for '" + SafeLabel(targetDisplay) + "'.";
                return true;
            }

            ScenarioEditorSession session;
            SpritePickerModel model;
            if (!TryGetOpenPickerModel(state, out session, out model, out message))
                return false;

            ClearCustomEditorSession();
            RestorePreviewSession();
            state.SpriteSwapPicker.PreviewCandidateToken = state.SpriteSwapPicker.SavedCandidateToken;
            state.SpriteSwapPicker.PreviewCandidateLabel = state.SpriteSwapPicker.SavedCandidateLabel;
            message = "Discarded the custom sprite draft for '" + SafeLabel(state.SpriteSwapPicker.Target.DisplayName) + "'.";
            return true;
        }

        private bool SelectCustomBrush(ScenarioAuthoringState state, int brushIndex, out string message)
        {
            message = null;
            if (!HasCustomEditor(state))
            {
                message = "Custom sprite editor is not active.";
                return false;
            }

            if (brushIndex < 0 || brushIndex >= _brushPalette.Length)
            {
                message = "Custom sprite brush was out of range.";
                return false;
            }

            _customEditorSession.ActiveBrushIndex = brushIndex;
            _customEditorSession.ActiveColor = _brushPalette[brushIndex];
            message = "Custom sprite brush updated to #" + EncodeColor(_customEditorSession.ActiveColor) + ".";
            return true;
        }

        private bool SelectCustomPreset(ScenarioAuthoringState state, int presetIndex, out string message)
        {
            message = null;
            if (!HasCustomEditor(state))
            {
                message = "Custom sprite editor is not active.";
                return false;
            }

            if (presetIndex < 0 || presetIndex >= _brushPalette.Length)
            {
                message = "Custom sprite preset was out of range.";
                return false;
            }

            _customEditorSession.ActiveBrushIndex = presetIndex;
            _customEditorSession.ActiveColor = _brushPalette[presetIndex];
            message = "Preset color selected.";
            return true;
        }

        private bool SelectCustomTool(ScenarioAuthoringState state, CustomEditorTool tool, out string message)
        {
            message = null;
            if (!HasCustomEditor(state))
            {
                message = "Custom sprite editor is not active.";
                return false;
            }

            if (_customEditorSession.ActiveTool == tool)
                return false;

            _customEditorSession.ActiveTool = tool;
            message = tool == CustomEditorTool.Paint
                ? "Paint tool selected."
                : (tool == CustomEditorTool.Pick ? "Color picker selected." : "Selection tool selected.");
            return true;
        }

        private bool SetCustomColor(ScenarioAuthoringState state, Color color, int preferredBrushIndex, out string message)
        {
            message = null;
            if (!HasCustomEditor(state))
            {
                message = "Custom sprite editor is not active.";
                return false;
            }

            Color normalized = NormalizeColor(color);
            if (ColorsEqual(_customEditorSession.ActiveColor, normalized))
                return false;

            _customEditorSession.ActiveColor = normalized;
            _customEditorSession.ActiveBrushIndex = preferredBrushIndex >= 0
                ? preferredBrushIndex
                : FindMatchingBrushIndex(normalized);
            message = "Active color set to #" + EncodeColor(normalized) + ".";
            return true;
        }

        private bool PaintCustomPixel(ScenarioAuthoringState state, int pixelX, int pixelY, out string message)
        {
            message = null;
            if (!HasCustomEditor(state) || _customEditorSession.Texture == null)
            {
                message = "Custom sprite editor is not active.";
                return false;
            }

            if (pixelX < 0 || pixelY < 0 || pixelX >= _customEditorSession.Texture.width || pixelY >= _customEditorSession.Texture.height)
            {
                message = "Custom sprite paint position was outside the editable area.";
                return false;
            }

            _customEditorSession.LastInteractionX = pixelX;
            _customEditorSession.LastInteractionY = pixelY;
            if (_customEditorSession.ActiveTool == CustomEditorTool.Pick)
                return PickCustomColor(state, pixelX, pixelY, out message);
            if (_customEditorSession.ActiveTool == CustomEditorTool.Select)
                return false;

            if (_customEditorSession.HasSelection && !SelectionContains(_customEditorSession, pixelX, pixelY))
                return false;

            Color color = _customEditorSession.ActiveColor;
            _customEditorSession.Texture.SetPixel(pixelX, pixelY, color);
            _customEditorSession.Texture.Apply();
            _customEditorSession.Dirty = true;
            message = "Painted custom sprite pixel.";
            return true;
        }

        private bool PickCustomColor(ScenarioAuthoringState state, int pixelX, int pixelY, out string message)
        {
            message = null;
            if (!HasCustomEditor(state) || _customEditorSession.Texture == null)
            {
                message = "Custom sprite editor is not active.";
                return false;
            }

            if (pixelX < 0 || pixelY < 0 || pixelX >= _customEditorSession.Texture.width || pixelY >= _customEditorSession.Texture.height)
            {
                message = "Color pick position was outside the editable area.";
                return false;
            }

            _customEditorSession.LastInteractionX = pixelX;
            _customEditorSession.LastInteractionY = pixelY;
            Color sampled = _customEditorSession.Texture.GetPixel(pixelX, pixelY);
            _customEditorSession.ActiveColor = sampled;
            _customEditorSession.ActiveBrushIndex = FindMatchingBrushIndex(sampled);
            message = "Picked color #" + EncodeColor(sampled) + ".";
            return true;
        }

        private bool StartCustomSelection(ScenarioAuthoringState state, int pixelX, int pixelY, out string message)
        {
            message = null;
            if (!HasCustomEditor(state) || _customEditorSession.Texture == null)
            {
                message = "Custom sprite editor is not active.";
                return false;
            }

            if (!ClampToTexture(_customEditorSession.Texture, ref pixelX, ref pixelY))
            {
                message = "Selection start was outside the editable area.";
                return false;
            }

            _customEditorSession.ActiveTool = CustomEditorTool.Select;
            _customEditorSession.SelectionDragActive = true;
            _customEditorSession.SelectionAnchorX = pixelX;
            _customEditorSession.SelectionAnchorY = pixelY;
            _customEditorSession.LastInteractionX = pixelX;
            _customEditorSession.LastInteractionY = pixelY;
            UpdateSelectionBounds(_customEditorSession, pixelX, pixelY, pixelX, pixelY);
            message = "Selection started.";
            return true;
        }

        private bool DragCustomSelection(ScenarioAuthoringState state, int pixelX, int pixelY, out string message)
        {
            message = null;
            if (!HasCustomEditor(state) || _customEditorSession.Texture == null)
            {
                message = "Custom sprite editor is not active.";
                return false;
            }

            if (!_customEditorSession.SelectionDragActive)
                return false;

            if (!ClampToTexture(_customEditorSession.Texture, ref pixelX, ref pixelY))
                return false;

            _customEditorSession.LastInteractionX = pixelX;
            _customEditorSession.LastInteractionY = pixelY;
            UpdateSelectionBounds(_customEditorSession, _customEditorSession.SelectionAnchorX, _customEditorSession.SelectionAnchorY, pixelX, pixelY);
            return true;
        }

        private bool EndCustomSelection(ScenarioAuthoringState state, int pixelX, int pixelY, out string message)
        {
            message = null;
            if (!HasCustomEditor(state) || _customEditorSession.Texture == null)
            {
                message = "Custom sprite editor is not active.";
                return false;
            }

            if (!_customEditorSession.SelectionDragActive)
                return false;

            if (!ClampToTexture(_customEditorSession.Texture, ref pixelX, ref pixelY))
                return false;

            _customEditorSession.LastInteractionX = pixelX;
            _customEditorSession.LastInteractionY = pixelY;
            UpdateSelectionBounds(_customEditorSession, _customEditorSession.SelectionAnchorX, _customEditorSession.SelectionAnchorY, pixelX, pixelY);
            _customEditorSession.SelectionDragActive = false;
            message = "Selection updated to " + _customEditorSession.SelectionWidth + "x" + _customEditorSession.SelectionHeight + ".";
            return true;
        }

        private bool ClearCustomSelection(ScenarioAuthoringState state, out string message)
        {
            message = null;
            if (!HasCustomEditor(state))
            {
                message = "Custom sprite editor is not active.";
                return false;
            }

            if (!_customEditorSession.HasSelection)
                return false;

            _customEditorSession.HasSelection = false;
            _customEditorSession.SelectionDragActive = false;
            _customEditorSession.SelectionWidth = 0;
            _customEditorSession.SelectionHeight = 0;
            message = "Selection cleared.";
            return true;
        }

        private bool CopyCustomPixels(ScenarioAuthoringState state, out string message)
        {
            message = null;
            if (!HasCustomEditor(state) || _customEditorSession.Texture == null)
            {
                message = "Custom sprite editor is not active.";
                return false;
            }

            int copyX;
            int copyY;
            int copyWidth;
            int copyHeight;
            ResolveCopyRegion(_customEditorSession, out copyX, out copyY, out copyWidth, out copyHeight);
            if (copyWidth <= 0 || copyHeight <= 0)
            {
                message = "Nothing is available to copy.";
                return false;
            }

            _customClipboardPixels = _customEditorSession.Texture.GetPixels(copyX, copyY, copyWidth, copyHeight);
            _customClipboardWidth = copyWidth;
            _customClipboardHeight = copyHeight;
            message = "Copied " + copyWidth + "x" + copyHeight + " pixels to the pixel clipboard.";
            return true;
        }

        private bool PasteCustomPixels(ScenarioAuthoringState state, out string message)
        {
            message = null;
            if (!HasCustomEditor(state) || _customEditorSession.Texture == null)
            {
                message = "Custom sprite editor is not active.";
                return false;
            }

            if (_customClipboardPixels == null || _customClipboardPixels.Length == 0 || _customClipboardWidth <= 0 || _customClipboardHeight <= 0)
            {
                message = "Pixel clipboard is empty.";
                return false;
            }

            int targetX = _customEditorSession.HasSelection
                ? _customEditorSession.SelectionX
                : Mathf.Max(0, _customEditorSession.LastInteractionX);
            int targetY = _customEditorSession.HasSelection
                ? _customEditorSession.SelectionY
                : Mathf.Max(0, _customEditorSession.LastInteractionY);
            int applied = 0;
            for (int y = 0; y < _customClipboardHeight; y++)
            {
                for (int x = 0; x < _customClipboardWidth; x++)
                {
                    int destX = targetX + x;
                    int destY = targetY + y;
                    if (destX < 0 || destY < 0 || destX >= _customEditorSession.Texture.width || destY >= _customEditorSession.Texture.height)
                        continue;

                    _customEditorSession.Texture.SetPixel(destX, destY, _customClipboardPixels[x + (y * _customClipboardWidth)]);
                    applied++;
                }
            }

            if (applied <= 0)
            {
                message = "Clipboard pixels were outside the editable area.";
                return false;
            }

            _customEditorSession.Texture.Apply();
            _customEditorSession.Dirty = true;
            _customEditorSession.LastInteractionX = targetX;
            _customEditorSession.LastInteractionY = targetY;
            _customEditorSession.HasSelection = true;
            _customEditorSession.SelectionDragActive = false;
            _customEditorSession.SelectionX = Mathf.Clamp(targetX, 0, Mathf.Max(0, _customEditorSession.Texture.width - 1));
            _customEditorSession.SelectionY = Mathf.Clamp(targetY, 0, Mathf.Max(0, _customEditorSession.Texture.height - 1));
            _customEditorSession.SelectionWidth = Mathf.Min(_customClipboardWidth, _customEditorSession.Texture.width - _customEditorSession.SelectionX);
            _customEditorSession.SelectionHeight = Mathf.Min(_customClipboardHeight, _customEditorSession.Texture.height - _customEditorSession.SelectionY);
            message = "Pasted " + _customEditorSession.SelectionWidth + "x" + _customEditorSession.SelectionHeight + " pixels.";
            return true;
        }

        private bool SavePicker(ScenarioAuthoringState state, out string message)
        {
            message = null;
            if (HasCharacterEditor(state))
                return SaveCharacterEditor(state, out message);

            ScenarioEditorSession session;
            SpritePickerModel model;
            if (!TryGetOpenPickerModel(state, out session, out model, out message))
                return false;

            if (HasCustomEditor(state))
                return SaveCustomSprite(state, session, model, out message);

            string previewToken = state.SpriteSwapPicker.PreviewCandidateToken;
            string savedToken = state.SpriteSwapPicker.SavedCandidateToken;
            string targetDisplay = state.SpriteSwapPicker.Target != null
                ? state.SpriteSwapPicker.Target.DisplayName
                : model.Target.TargetPath;

            if (string.IsNullOrEmpty(previewToken) || string.Equals(previewToken, savedToken, StringComparison.Ordinal))
            {
                ClosePickerState(state, false);
                message = "Closed the sprite picker for '" + SafeLabel(targetDisplay) + "' without changes.";
                return true;
            }

            ScenarioSpriteCatalogService.SpriteCandidate candidate = FindCandidate(model, previewToken);
            if (candidate == null)
            {
                ClosePickerState(state, true);
                message = "The selected sprite preview is no longer available.";
                return false;
            }

            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            if (definition == null)
            {
                ClosePickerState(state, true);
                message = "No active authoring session is available.";
                return false;
            }

            _historyService.RecordVisualChange(definition, "Apply sprite to " + SafeLabel(targetDisplay));
            ScenarioSpriteSwapRuleEditor.ApplyCandidate(definition, model.Target, candidate, GetCurrentDay());

            MarkAssetsDirty(session);
            _spriteSwapEngine.Activate(definition, state.ActiveScenarioFilePath, null);
            ClosePickerState(state, false);
            Invalidate();

            string kindLabel = candidate.SourceKind == ScenarioSpriteCatalogService.SpriteCandidateSourceKind.VanillaRuntime
                ? "vanilla sprite"
                : "modded sprite";
            message = "Saved " + kindLabel + " '" + SafeLabel(candidate.Label) + "' onto '" + SafeLabel(targetDisplay) + "'.";
            MMLog.WriteInfo("[ScenarioSpriteSwapAuthoring] " + message);
            return true;
        }

        private bool SaveCustomSprite(
            ScenarioAuthoringState state,
            ScenarioEditorSession session,
            SpritePickerModel model,
            out string message)
        {
            message = null;
            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            if (definition == null || _customEditorSession == null || _customEditorSession.Texture == null)
            {
                message = "Custom sprite editor is not active.";
                return false;
            }

            string packRoot = !string.IsNullOrEmpty(state.ActiveScenarioFilePath)
                ? Path.GetDirectoryName(state.ActiveScenarioFilePath)
                : null;
            if (string.IsNullOrEmpty(packRoot))
            {
                message = "Scenario pack path is unavailable, so the custom sprite could not be saved.";
                return false;
            }

            try
            {
                string targetDisplay = state.SpriteSwapPicker.Target != null
                    ? state.SpriteSwapPicker.Target.DisplayName
                    : model.Target.TargetPath;
                string customSpriteId = _customEditorSession.CustomSpriteId;
                _historyService.RecordVisualChange(
                    definition,
                    "Apply custom sprite to " + SafeLabel(state.SpriteSwapPicker.Target.DisplayName));

                string patchId = UpsertPatchSpriteAsset(definition, packRoot, customSpriteId, _customEditorSession.SourceLabel);
                if (string.IsNullOrEmpty(patchId))
                {
                    message = "Custom sprite patch could not be generated.";
                    return false;
                }
                ApplyCustomSpriteRule(definition, model.Target, customSpriteId, null);

                MarkAssetsDirty(session);
                _spriteSwapEngine.Activate(definition, state.ActiveScenarioFilePath, null);
                ClosePickerState(state, false);
                Invalidate();

                message = "Saved custom sprite patch '" + patchId + "' onto '" + SafeLabel(targetDisplay) + "'.";
                MMLog.WriteInfo("[ScenarioSpriteSwapAuthoring] " + message);
                return true;
            }
            catch (Exception ex)
            {
                message = "Custom sprite save failed: " + ex.Message;
                return false;
            }
        }

        private bool CancelPicker(ScenarioAuthoringState state, out string message)
        {
            message = null;
            if (!IsPickerOpen(state))
            {
                message = "Sprite picker is not open.";
                return false;
            }

            string targetDisplay = state.SpriteSwapPicker.Target != null
                ? state.SpriteSwapPicker.Target.DisplayName
                : state.SpriteSwapPicker.TargetPath;
            ClosePickerState(state, true);
            message = "Cancelled sprite changes for '" + SafeLabel(targetDisplay) + "'.";
            return true;
        }

        private bool ApplyCandidateImmediately(ScenarioAuthoringState state, string token, out string message)
        {
            message = null;
            ClosePickerState(state, true);

            ScenarioEditorSession session = _editorService.CurrentSession;
            SpritePickerModel model = GetPickerModel(session, state.SelectedTarget, state.ActiveScenarioFilePath);
            ScenarioSpriteCatalogService.SpriteCandidate candidate = FindCandidate(model, token);
            if (candidate == null || model == null || model.Target == null)
            {
                message = model != null && !string.IsNullOrEmpty(model.GuidanceMessage)
                    ? model.GuidanceMessage
                    : "No compatible sprite candidate is available for the selected target.";
                return false;
            }

            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            if (definition == null)
            {
                message = "No active authoring session is available.";
                return false;
            }

            string targetDisplay = state.SelectedTarget != null ? state.SelectedTarget.DisplayName : model.Target.TargetPath;
            _historyService.RecordVisualChange(definition, "Apply sprite to " + SafeLabel(targetDisplay));
            ScenarioSpriteSwapRuleEditor.ApplyCandidate(definition, model.Target, candidate, GetCurrentDay());

            string kindLabel = candidate.SourceKind == ScenarioSpriteCatalogService.SpriteCandidateSourceKind.VanillaRuntime
                ? "vanilla sprite"
                : "modded sprite";
            message = "Applied " + kindLabel + " '" + SafeLabel(candidate.Label) + "' to '" + SafeLabel(targetDisplay) + "'.";

            MarkAssetsDirty(session);
            _spriteSwapEngine.Activate(definition, state.ActiveScenarioFilePath, null);
            Invalidate();
            MMLog.WriteInfo("[ScenarioSpriteSwapAuthoring] " + message);
            return true;
        }

        private bool ClearActiveSwap(ScenarioAuthoringState state, out string message)
        {
            message = null;
            ClosePickerState(state, true);

            ScenarioEditorSession session = _editorService.CurrentSession;
            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            if (definition == null || state.SelectedTarget == null)
            {
                message = "No active sprite swap is available for the selected target.";
                return false;
            }

            SpritePickerModel model = GetPickerModel(session, state.SelectedTarget, state.ActiveScenarioFilePath);
            string targetPath = model != null && model.Target != null ? model.Target.TargetPath : state.SelectedTarget.TransformPath;
            string targetDisplay = state.SelectedTarget.DisplayName;

            _historyService.RecordVisualChange(definition, "Revert sprite on " + SafeLabel(targetDisplay));
            if (!ScenarioSpriteSwapRuleEditor.ClearActiveRule(definition, targetPath, GetCurrentDay()))
            {
                message = "The selected target does not have an active sprite swap.";
                string ignored;
                _historyService.Undo(definition, out ignored);
                return false;
            }

            MarkAssetsDirty(session);
            _spriteSwapEngine.Activate(definition, state.ActiveScenarioFilePath, null);
            Invalidate();
            message = "Reverted the active sprite swap on '" + SafeLabel(targetDisplay) + "'.";
            MMLog.WriteInfo("[ScenarioSpriteSwapAuthoring] " + message);
            return true;
        }

        private bool CopyActiveSwap(ScenarioAuthoringState state, out string message)
        {
            message = null;
            ScenarioEditorSession session = _editorService.CurrentSession;
            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            if (definition == null || state.SelectedTarget == null)
            {
                message = "Select a target with an active swap before copying.";
                return false;
            }

            SpritePickerModel model = GetPickerModel(session, state.SelectedTarget, state.ActiveScenarioFilePath);
            string targetPath = model != null && model.Target != null ? model.Target.TargetPath : state.SelectedTarget.TransformPath;
            SpriteSwapRule activeRule = ScenarioSpriteSwapRuleEditor.FindActiveRule(definition, targetPath, GetCurrentDay());
            if (activeRule == null)
            {
                message = "Selected target has no active sprite swap to copy.";
                return false;
            }

            ScenarioSpriteSwapClipboard.Copy(activeRule, state.SelectedTarget.DisplayName);
            ScenarioHoverVisualService.Instance.SetSecondary(state.SelectedTarget);
            message = "Copied sprite swap from '" + SafeLabel(state.SelectedTarget.DisplayName) + "'. Select another target and paste.";
            MMLog.WriteInfo("[ScenarioSpriteSwapAuthoring] " + message);
            return true;
        }

        private bool PasteSwap(ScenarioAuthoringState state, out string message)
        {
            message = null;
            ClosePickerState(state, true);

            if (!ScenarioSpriteSwapClipboard.HasRule)
            {
                message = "Clipboard is empty. Copy a sprite swap first.";
                return false;
            }

            ScenarioEditorSession session = _editorService.CurrentSession;
            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            if (definition == null || state.SelectedTarget == null)
            {
                message = "Select a target before pasting the clipboard sprite swap.";
                return false;
            }

            SpritePickerModel model = GetPickerModel(session, state.SelectedTarget, state.ActiveScenarioFilePath);
            if (model == null || model.Target == null)
            {
                message = "Selected target does not accept sprite swaps.";
                return false;
            }

            SpriteSwapRule clipRule = ScenarioSpriteSwapClipboard.TakeClone();
            if (clipRule == null)
            {
                message = "Clipboard entry was empty.";
                return false;
            }

            _historyService.RecordVisualChange(definition, "Paste sprite to " + SafeLabel(state.SelectedTarget.DisplayName));

            int currentDay = GetCurrentDay();
            ScenarioSpriteSwapRuleEditor.EnsureAssetReferences(definition);
            SpriteSwapRule rule = ScenarioSpriteSwapRuleEditor.FindEditableRule(definition, model.Target.TargetPath, currentDay);
            if (rule == null)
            {
                rule = new SpriteSwapRule
                {
                    Id = ScenarioSpriteSwapRuleEditor.BuildRuleId(model.Target.TargetPath),
                    Day = 1
                };
                definition.AssetReferences.SpriteSwaps.Add(rule);
            }

            rule.TargetPath = model.Target.TargetPath;
            rule.TargetComponent = model.Target.Kind;
            rule.SpriteId = clipRule.SpriteId;
            rule.RelativePath = clipRule.RelativePath;
            rule.RuntimeSpriteKey = clipRule.RuntimeSpriteKey;

            MarkAssetsDirty(session);
            _spriteSwapEngine.Activate(definition, state.ActiveScenarioFilePath, null);
            Invalidate();
            message = "Pasted sprite swap '" + ScenarioSpriteSwapRuleEditor.DescribeRuleShort(rule)
                + "' onto '" + SafeLabel(state.SelectedTarget.DisplayName) + "'.";
            MMLog.WriteInfo("[ScenarioSpriteSwapAuthoring] " + message);
            return true;
        }

        private bool Undo(ScenarioAuthoringState state, out string message)
        {
            message = null;
            ClosePickerState(state, true);

            ScenarioEditorSession session = _editorService.CurrentSession;
            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            if (definition == null)
            {
                message = "No active authoring session.";
                return false;
            }

            string description;
            if (!_historyService.Undo(definition, out description))
            {
                message = "Nothing to undo.";
                return false;
            }

            MarkAssetsDirty(session);
            ReapplyVisualState(definition, state.ActiveScenarioFilePath);
            Invalidate();
            message = "Undid: " + (description ?? "last change") + ".";
            MMLog.WriteInfo("[ScenarioSpriteSwapAuthoring] " + message);
            return true;
        }

        private bool Redo(ScenarioAuthoringState state, out string message)
        {
            message = null;
            ClosePickerState(state, true);

            ScenarioEditorSession session = _editorService.CurrentSession;
            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            if (definition == null)
            {
                message = "No active authoring session.";
                return false;
            }

            string description;
            if (!_historyService.Redo(definition, out description))
            {
                message = "Nothing to redo.";
                return false;
            }

            MarkAssetsDirty(session);
            ReapplyVisualState(definition, state.ActiveScenarioFilePath);
            Invalidate();
            message = "Redid: " + (description ?? "change") + ".";
            MMLog.WriteInfo("[ScenarioSpriteSwapAuthoring] " + message);
            return true;
        }

        private bool TryGetOpenPickerModel(
            ScenarioAuthoringState state,
            out ScenarioEditorSession session,
            out SpritePickerModel model,
            out string message)
        {
            session = _editorService.CurrentSession;
            model = null;
            message = null;

            if (!IsPickerOpen(state))
            {
                message = "Sprite picker is not open.";
                return false;
            }

            if (session == null || session.WorkingDefinition == null)
            {
                message = "No active authoring session is available.";
                return false;
            }

            model = GetPickerModel(session, state.SpriteSwapPicker.Target, state.ActiveScenarioFilePath);
            if (model == null || model.Target == null)
            {
                message = "The selected target does not expose compatible sprite replacements.";
                return false;
            }

            return true;
        }

        private bool EnsurePreviewSession(ScenarioSpriteRuntimeResolver.ResolvedTarget target, out string message)
        {
            message = null;
            if (target == null || !ScenarioSpriteRuntimeResolver.IsAlive(target))
            {
                message = "The selected sprite target is no longer available in the scene.";
                return false;
            }

            if (_previewSession != null
                && string.Equals(_previewSession.TargetPath, target.TargetPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            RestorePreviewSession();
            _previewSession = new PreviewSession
            {
                TargetPath = target.TargetPath,
                TargetKind = target.Kind,
                BaselineSprite = target.CurrentSprite
            };
            return true;
        }

        private void RestorePreviewSession()
        {
            if (_previewSession == null)
                return;

            ScenarioSpriteRuntimeResolver.ResolvedTarget runtimeTarget;
            if (_runtimeResolver.TryResolve(_previewSession.TargetPath, _previewSession.TargetKind, out runtimeTarget))
                ScenarioSpriteRuntimeMutationService.TryApply(runtimeTarget, _previewSession.BaselineSprite);
        }

        private void RestoreCharacterPreviewSession()
        {
            if (_characterPreviewSession == null)
                return;

            _characterAppearanceService.RestorePreview(_characterPreviewSession);
        }

        private void ClearPreviewSession()
        {
            _previewSession = null;
        }

        private void ClearCharacterPreviewSession()
        {
            _characterPreviewSession = null;
        }

        private void ClearCustomEditorSession()
        {
            if (_customEditorSession != null)
            {
                if (_customEditorSession.PreviewSprite != null)
                    UnityEngine.Object.Destroy(_customEditorSession.PreviewSprite);
                if (_customEditorSession.BaselineTexture != null)
                    UnityEngine.Object.Destroy(_customEditorSession.BaselineTexture);
                if (_customEditorSession.Texture != null)
                    UnityEngine.Object.Destroy(_customEditorSession.Texture);
            }

            _customEditorSession = null;
        }

        private void ClosePickerState(ScenarioAuthoringState state, bool restorePreview)
        {
            if (restorePreview)
            {
                RestorePreviewSession();
                RestoreCharacterPreviewSession();
            }

            ClearCustomEditorSession();
            ClearPreviewSession();
            ClearCharacterPreviewSession();
            if (state != null)
                state.SpriteSwapPicker = null;
        }

        private static bool IsPickerOpen(ScenarioAuthoringState state)
        {
            return state != null
                && state.SpriteSwapPicker != null
                && state.SpriteSwapPicker.IsOpen
                && state.SpriteSwapPicker.Target != null;
        }

        private bool HasCustomEditor(ScenarioAuthoringState state)
        {
            return IsPickerOpen(state)
                && _customEditorSession != null
                && string.Equals(_customEditorSession.TargetPath, state.SpriteSwapPicker.TargetPath, StringComparison.OrdinalIgnoreCase);
        }

        private bool HasCharacterEditor(ScenarioAuthoringState state)
        {
            return HasCustomEditor(state)
                && _customEditorSession != null
                && _customEditorSession.IsCharacterEditor;
        }

        private bool TryResolveCharacterEditorTarget(
            ScenarioAuthoringState state,
            out ScenarioCharacterAppearanceService.ResolvedCharacterTarget target,
            out string message)
        {
            target = null;
            message = null;
            if (state == null || state.SpriteSwapPicker == null || state.SpriteSwapPicker.Target == null)
            {
                message = "Character editor target is unavailable.";
                return false;
            }

            return _characterAppearanceService.TryResolve(state.SpriteSwapPicker.Target, out target, out message);
        }

        private static FamilyMemberConfig EnsureFamilyMemberConfig(
            ScenarioDefinition definition,
            ScenarioCharacterAppearanceService.ResolvedCharacterTarget target)
        {
            if (definition.FamilySetup == null)
                definition.FamilySetup = new FamilySetupDefinition();

            while (definition.FamilySetup.Members.Count <= target.FamilyIndex)
                definition.FamilySetup.Members.Add(new FamilyMemberConfig());

            FamilyMemberConfig memberConfig = definition.FamilySetup.Members[target.FamilyIndex];
            if (memberConfig == null)
            {
                memberConfig = new FamilyMemberConfig();
                definition.FamilySetup.Members[target.FamilyIndex] = memberConfig;
            }

            if (string.IsNullOrEmpty(memberConfig.Name) && target.FamilyMember != null)
                memberConfig.Name = target.FamilyMember.firstName;
            if (memberConfig.Gender == ScenarioGender.Any && target.FamilyMember != null)
                memberConfig.Gender = target.FamilyMember.isMale ? ScenarioGender.Male : ScenarioGender.Female;
            if (memberConfig.Appearance == null)
                memberConfig.Appearance = new FamilyMemberAppearanceConfig();

            return memberConfig;
        }

        private static void MarkFamilyDirty(ScenarioEditorSession session)
        {
            if (session == null)
                return;

            if (!session.DirtyFlags.Contains(ScenarioDirtySection.Family))
                session.DirtyFlags.Add(ScenarioDirtySection.Family);
            if (!session.DirtyFlags.Contains(ScenarioDirtySection.Assets))
                session.DirtyFlags.Add(ScenarioDirtySection.Assets);

            session.CurrentEditCategory = ScenarioEditCategory.Family;
            session.HasAppliedToCurrentWorld = true;
        }

        private void ClearCustomClipboard()
        {
            _customClipboardPixels = null;
            _customClipboardWidth = 0;
            _customClipboardHeight = 0;
        }

        private static void ResolveCopyRegion(CustomEditorSession session, out int copyX, out int copyY, out int copyWidth, out int copyHeight)
        {
            copyX = 0;
            copyY = 0;
            copyWidth = 0;
            copyHeight = 0;
            if (session == null || session.Texture == null)
                return;

            if (session.HasSelection && session.SelectionWidth > 0 && session.SelectionHeight > 0)
            {
                copyX = session.SelectionX;
                copyY = session.SelectionY;
                copyWidth = session.SelectionWidth;
                copyHeight = session.SelectionHeight;
                return;
            }

            copyWidth = session.Texture.width;
            copyHeight = session.Texture.height;
        }

        private static bool SelectionContains(CustomEditorSession session, int pixelX, int pixelY)
        {
            return session != null
                && session.HasSelection
                && pixelX >= session.SelectionX
                && pixelY >= session.SelectionY
                && pixelX < session.SelectionX + session.SelectionWidth
                && pixelY < session.SelectionY + session.SelectionHeight;
        }

        private static void UpdateSelectionBounds(CustomEditorSession session, int startX, int startY, int endX, int endY)
        {
            if (session == null || session.Texture == null)
                return;

            int minX = Mathf.Clamp(Math.Min(startX, endX), 0, Mathf.Max(0, session.Texture.width - 1));
            int maxX = Mathf.Clamp(Math.Max(startX, endX), 0, Mathf.Max(0, session.Texture.width - 1));
            int minY = Mathf.Clamp(Math.Min(startY, endY), 0, Mathf.Max(0, session.Texture.height - 1));
            int maxY = Mathf.Clamp(Math.Max(startY, endY), 0, Mathf.Max(0, session.Texture.height - 1));
            session.HasSelection = true;
            session.SelectionX = minX;
            session.SelectionY = minY;
            session.SelectionWidth = (maxX - minX) + 1;
            session.SelectionHeight = (maxY - minY) + 1;
        }

        private static bool ClampToTexture(Texture2D texture, ref int pixelX, ref int pixelY)
        {
            if (texture == null || texture.width <= 0 || texture.height <= 0)
                return false;

            pixelX = Mathf.Clamp(pixelX, 0, texture.width - 1);
            pixelY = Mathf.Clamp(pixelY, 0, texture.height - 1);
            return true;
        }

        private static List<ScenarioSpriteCatalogService.SpriteCandidate> CloneCandidates(List<ScenarioSpriteCatalogService.SpriteCandidate> source)
        {
            List<ScenarioSpriteCatalogService.SpriteCandidate> clone = new List<ScenarioSpriteCatalogService.SpriteCandidate>();
            for (int i = 0; source != null && i < source.Count; i++)
            {
                ScenarioSpriteCatalogService.SpriteCandidate item = source[i];
                if (item == null)
                    continue;

                clone.Add(new ScenarioSpriteCatalogService.SpriteCandidate
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
                    Sprite = item.Sprite
                });
            }

            return clone;
        }

        private static void AnnotateCandidateHints(List<ScenarioSpriteCatalogService.SpriteCandidate> candidates, string activeToken)
        {
            for (int i = 0; candidates != null && i < candidates.Count; i++)
            {
                ScenarioSpriteCatalogService.SpriteCandidate candidate = candidates[i];
                if (candidate == null || !string.Equals(candidate.Token, activeToken, StringComparison.Ordinal))
                    continue;

                candidate.Hint = string.IsNullOrEmpty(candidate.Hint)
                    ? "Saved in the scenario for this target."
                    : (candidate.Hint + " | Saved in the scenario for this target.");
            }
        }

        private static string FindMatchingCandidateToken(SpritePickerModel model, SpriteSwapRule activeRule)
        {
            ScenarioSpriteCatalogService.SpriteCandidate candidate = FindMatchingCandidate(model != null ? model.VanillaCandidates : null, activeRule);
            if (candidate != null)
                return candidate.Token;

            candidate = FindMatchingCandidate(model != null ? model.ModdedCandidates : null, activeRule);
            return candidate != null ? candidate.Token : null;
        }

        private static ScenarioSpriteCatalogService.SpriteCandidate FindMatchingCandidate(
            List<ScenarioSpriteCatalogService.SpriteCandidate> candidates,
            SpriteSwapRule activeRule)
        {
            for (int i = 0; candidates != null && i < candidates.Count; i++)
            {
                ScenarioSpriteCatalogService.SpriteCandidate candidate = candidates[i];
                if (candidate != null && ScenarioSpriteSwapRuleEditor.RuleMatchesCandidate(activeRule, candidate))
                    return candidate;
            }

            return null;
        }

        private static string FindCandidateLabel(SpritePickerModel model, string token)
        {
            ScenarioSpriteCatalogService.SpriteCandidate candidate = FindCandidate(model, token);
            return candidate != null ? candidate.Label : null;
        }

        private static ScenarioSpriteCatalogService.SpriteCandidate FindCandidate(SpritePickerModel model, string token)
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

        private static bool AreSameTarget(ScenarioAuthoringTarget left, ScenarioAuthoringTarget right)
        {
            if (left == null || right == null)
                return false;

            if (!string.IsNullOrEmpty(left.Id) && !string.IsNullOrEmpty(right.Id))
                return string.Equals(left.Id, right.Id, StringComparison.OrdinalIgnoreCase);

            if (!string.IsNullOrEmpty(left.TransformPath) && !string.IsNullOrEmpty(right.TransformPath))
                return string.Equals(left.TransformPath, right.TransformPath, StringComparison.OrdinalIgnoreCase);

            return string.Equals(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
        }

        private static Color[] CloneBrushPalette()
        {
            Color[] palette = new Color[_brushPalette.Length];
            for (int i = 0; i < _brushPalette.Length; i++)
                palette[i] = _brushPalette[i];
            return palette;
        }

        private static int FindMatchingBrushIndex(Color color)
        {
            for (int i = 0; i < _brushPalette.Length; i++)
            {
                if (ColorsEqual(_brushPalette[i], color))
                    return i;
            }

            return -1;
        }

        private static Color FindInitialBrushColor(Texture2D texture)
        {
            if (texture != null)
            {
                Color[] pixels = texture.GetPixels();
                for (int i = 0; pixels != null && i < pixels.Length; i++)
                {
                    if (pixels[i].a > 0.001f)
                        return pixels[i];
                }
            }

            return _brushPalette[0];
        }

        private static string EncodeColor(Color color)
        {
            Color32 value = (Color32)NormalizeColor(color);
            return value.r.ToString("X2")
                + value.g.ToString("X2")
                + value.b.ToString("X2")
                + value.a.ToString("X2");
        }

        private static bool TryDecodeColor(string encoded, out Color color)
        {
            color = Color.white;
            if (string.IsNullOrEmpty(encoded))
                return false;

            return ColorUtility.TryParseHtmlString("#" + encoded, out color);
        }

        private static Color NormalizeColor(Color color)
        {
            return new Color(
                Mathf.Clamp01(color.r),
                Mathf.Clamp01(color.g),
                Mathf.Clamp01(color.b),
                Mathf.Clamp01(color.a));
        }

        private static bool ColorsEqual(Color left, Color right)
        {
            return Math.Abs(left.r - right.r) <= (1f / 255f)
                && Math.Abs(left.g - right.g) <= (1f / 255f)
                && Math.Abs(left.b - right.b) <= (1f / 255f)
                && Math.Abs(left.a - right.a) <= (1f / 255f);
        }

        private static bool TryDecodePixel(string encoded, out int pixelX, out int pixelY)
        {
            pixelX = 0;
            pixelY = 0;
            if (string.IsNullOrEmpty(encoded))
                return false;

            string[] parts = encoded.Split(',');
            return parts.Length == 2
                && int.TryParse(parts[0], out pixelX)
                && int.TryParse(parts[1], out pixelY);
        }

        private static Texture2D CreateEditableTexture(Sprite source)
        {
            if (source == null || source.texture == null)
                return null;

            Rect sourceRect = source.textureRect;
            int width = Mathf.Max(1, Mathf.RoundToInt(sourceRect.width));
            int height = Mathf.Max(1, Mathf.RoundToInt(sourceRect.height));
            Texture2D texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            try
            {
                Color[] pixels = source.texture.GetPixels(
                    Mathf.RoundToInt(sourceRect.x),
                    Mathf.RoundToInt(sourceRect.y),
                    width,
                    height);
                texture.SetPixels(pixels);
                texture.Apply();
                return texture;
            }
            catch
            {
                RenderTexture renderTexture = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = renderTexture;
                GL.Clear(true, true, Color.clear);
                GL.PushMatrix();
                GL.LoadPixelMatrix(0f, width, height, 0f);
                Rect uv = new Rect(
                    sourceRect.x / source.texture.width,
                    sourceRect.y / source.texture.height,
                    sourceRect.width / source.texture.width,
                    sourceRect.height / source.texture.height);
                Graphics.DrawTexture(new Rect(0f, 0f, width, height), source.texture, uv, 0, 0, 0, 0);
                GL.PopMatrix();
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                texture.Apply();
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(renderTexture);
                return texture;
            }
        }

        private static Sprite CreatePreviewSprite(Texture2D texture, Sprite source)
        {
            if (texture == null)
                return null;

            Rect rect = new Rect(0f, 0f, texture.width, texture.height);
            Vector2 pivot = new Vector2(0.5f, 0.5f);
            float pixelsPerUnit = 100f;
            if (source != null)
            {
                Rect sourceRect = source.rect;
                if (sourceRect.width > 0f && sourceRect.height > 0f)
                {
                    pivot = new Vector2(source.pivot.x / sourceRect.width, source.pivot.y / sourceRect.height);
                }

                if (source.pixelsPerUnit > 0f)
                    pixelsPerUnit = source.pixelsPerUnit;
            }

            Sprite sprite = Sprite.Create(texture, rect, pivot, pixelsPerUnit);
            if (sprite != null && sprite.texture != null)
                sprite.texture.filterMode = FilterMode.Point;
            return sprite;
        }

        private static string BuildCustomSpriteId(string targetPath)
        {
            string safe = string.IsNullOrEmpty(targetPath) ? "sprite" : targetPath.Replace('/', '_').Replace('\\', '_');
            return "custom_" + safe.ToLowerInvariant() + "_" + DateTime.UtcNow.Ticks;
        }

        private static string BuildCharacterCustomTextureId(
            ScenarioCharacterAppearanceService.ResolvedCharacterTarget target,
            ScenarioCharacterTexturePart part)
        {
            string safe = target != null && !string.IsNullOrEmpty(target.TargetPath)
                ? target.TargetPath.Replace('/', '_').Replace('\\', '_')
                : "character";
            return "character_" + safe.ToLowerInvariant() + "_" + part.ToString().ToLowerInvariant() + "_" + DateTime.UtcNow.Ticks;
        }

        private static string BuildPatchBaseRelativePath(string spriteId)
        {
            return Path.Combine(Path.Combine(Path.Combine("Sprites", "Authoring"), "bases"), spriteId + ".png").Replace('\\', '/');
        }

        private string UpsertPatchSpriteAsset(ScenarioDefinition definition, string packRoot, string spriteId, string displayName)
        {
            if (definition == null || definition.AssetReferences == null || _customEditorSession == null || string.IsNullOrEmpty(spriteId))
                return null;

            string patchId = spriteId + ".patch";
            string baseSpriteId = _customEditorSession.BaseSpriteId;
            string baseRelativePath = _customEditorSession.BaseRelativePath;
            string baseRuntimeSpriteKey = _customEditorSession.BaseRuntimeSpriteKey;

            if (string.IsNullOrEmpty(baseSpriteId)
                && string.IsNullOrEmpty(baseRelativePath)
                && string.IsNullOrEmpty(baseRuntimeSpriteKey)
                && !string.IsNullOrEmpty(packRoot)
                && _customEditorSession.BaselineTexture != null)
            {
                baseRelativePath = BuildPatchBaseRelativePath(spriteId);
                string fullPath = Path.Combine(packRoot, baseRelativePath);
                string directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllBytes(fullPath, _customEditorSession.BaselineTexture.EncodeToPNG());
            }

            SpritePatchDefinition patch = _spritePatchBuilder.Build(
                patchId,
                string.IsNullOrEmpty(displayName) ? spriteId : displayName,
                baseSpriteId,
                baseRelativePath,
                baseRuntimeSpriteKey,
                _customEditorSession.BaselineTexture,
                _customEditorSession.Texture);
            if (patch == null)
                return null;

            UpsertPatchDefinition(definition, patch);
            UpsertCustomSpriteReference(definition, spriteId, patchId);
            return patchId;
        }

        private static void UpsertCustomSpriteReference(ScenarioDefinition definition, string spriteId, string patchId)
        {
            if (definition == null || definition.AssetReferences == null || string.IsNullOrEmpty(spriteId))
                return;

            for (int i = 0; i < definition.AssetReferences.CustomSprites.Count; i++)
            {
                SpriteRef sprite = definition.AssetReferences.CustomSprites[i];
                if (sprite != null && string.Equals(sprite.Id, spriteId, StringComparison.OrdinalIgnoreCase))
                {
                    sprite.RelativePath = null;
                    sprite.PatchId = patchId;
                    return;
                }
            }

            definition.AssetReferences.CustomSprites.Add(new SpriteRef
            {
                Id = spriteId,
                PatchId = patchId
            });
        }

        private static void UpsertPatchDefinition(ScenarioDefinition definition, SpritePatchDefinition patch)
        {
            if (definition == null || definition.AssetReferences == null || patch == null)
                return;

            for (int i = 0; i < definition.AssetReferences.SpritePatches.Count; i++)
            {
                SpritePatchDefinition existing = definition.AssetReferences.SpritePatches[i];
                if (existing != null && string.Equals(existing.Id, patch.Id, StringComparison.OrdinalIgnoreCase))
                {
                    definition.AssetReferences.SpritePatches[i] = patch;
                    return;
                }
            }

            definition.AssetReferences.SpritePatches.Add(patch);
        }

        private static void ApplyCustomSpriteRule(
            ScenarioDefinition definition,
            ScenarioSpriteRuntimeResolver.ResolvedTarget target,
            string spriteId,
            string relativePath)
        {
            if (definition == null || target == null)
                return;

            int currentDay = GetCurrentDay();
            ScenarioSpriteSwapRuleEditor.EnsureAssetReferences(definition);
            SpriteSwapRule rule = ScenarioSpriteSwapRuleEditor.FindEditableRule(definition, target.TargetPath, currentDay);
            if (rule == null)
            {
                rule = new SpriteSwapRule
                {
                    Id = ScenarioSpriteSwapRuleEditor.BuildRuleId(target.TargetPath),
                    Day = 1
                };
                definition.AssetReferences.SpriteSwaps.Add(rule);
            }

            rule.TargetPath = target.TargetPath;
            rule.TargetComponent = target.Kind;
            rule.RuntimeSpriteKey = null;
            rule.SpriteId = spriteId;
            rule.RelativePath = relativePath;
        }

        private void ReapplyVisualState(ScenarioDefinition definition, string scenarioFilePath)
        {
            _spriteSwapEngine.Activate(definition, scenarioFilePath, null);
            _sceneSpritePlacementEngine.Activate(definition, scenarioFilePath, null);

            List<FamilyMember> family = FamilyManager.Instance != null ? FamilyManager.Instance.GetAllFamilyMembers() : null;
            for (int i = 0; definition != null && definition.FamilySetup != null && family != null && i < definition.FamilySetup.Members.Count && i < family.Count; i++)
            {
                FamilyMemberConfig config = definition.FamilySetup.Members[i];
                FamilyMember member = family[i];
                string ignored;
                if (config != null && member != null)
                    _characterAppearanceService.ApplyConfiguredAppearance(definition, scenarioFilePath, config, member, out ignored);
            }
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

        private static string SafeLabel(string value)
        {
            return string.IsNullOrEmpty(value) ? "<target>" : value;
        }

        private static string CleanCandidateLabel(string label)
        {
            return string.IsNullOrEmpty(label) ? "<sprite>" : label;
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

        private static int GetCurrentDay()
        {
            try
            {
                return GameTime.Day > 0 ? GameTime.Day : 1;
            }
            catch
            {
                return 1;
            }
        }
    }
}
