using System;
using System.Collections.Generic;
using System.IO;
using ModAPI.Core;
using ModAPI.Scenarios;
using ShelteredAPI.Content;
using UnityEngine;

using ShelteredAPI.Hooks;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Infrastructure.Assets;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
namespace ShelteredAPI.Scenarios.Application.Assets{
    // Thin orchestrator for the sprite-swap authoring workflow. Catalog queries live
    // in ScenarioSpriteCatalogService; rule mutation in ScenarioSpriteSwapRuleEditor;
    // undo/redo in ScenarioAuthoringHistoryService; clipboard in
    // ScenarioSpriteSwapClipboard. This class composes them and translates authoring
    // actions into preview or persistence steps.
    internal sealed partial class ScenarioSpriteSwapAuthoringService
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
            public bool IsAnimationEditor;
            public string AnimationClipName;
            public int AnimationFrameIndex;
            public int AnimationFrameCount;
            public float AnimationFrameDurationSeconds;
            public float AnimationClipLengthSeconds;
            public bool AnimationPlaying;
            public bool AnimationPlayingInWorld;
            public float AnimationSpeed;
            public bool OnionSkin;
            public bool CompareOriginal;
            public List<AnimationFrameModel> AnimationFrames;
        }

        internal sealed class AnimationFrameModel
        {
            public int Index;
            public Sprite OriginalSprite;
            public Sprite EditedSprite;
            public bool Dirty;
            public float DurationSeconds;
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
            public ScenarioSpriteAnimationMetadataService.AnimationMetadata AnimationMetadata;
            public List<AnimationFrameEdit> AnimationFrames;
            public int AnimationFrameIndex;
            public bool AnimationPlaying;
            public bool AnimationPlayingInWorld;
            public float AnimationPlaybackAccumulator;
            public float AnimationPlaybackSpeed;
            public bool OnionSkin;
            public bool CompareOriginal;
        }

        private sealed class AnimationFrameEdit
        {
            public int Index;
            public string SourceRuntimeSpriteKey;
            public Texture2D BaselineTexture;
            public Texture2D Texture;
            public Sprite OriginalSprite;
            public Sprite PreviewSprite;
            public float DurationSeconds;
            public bool Dirty;
        }

        private sealed class PixelEditSnapshot
        {
            public string Description;
            public Color[] Pixels;
            public bool Dirty;
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
        private readonly ScenarioSpriteAnimationMetadataService _animationMetadataService;
        private readonly ScenarioSpritePatchAuthoringService _spritePatchAuthoringService;
        private readonly ScenarioPngImportService _pngImportService;
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
        private readonly Stack<PixelEditSnapshot> _customPixelUndo = new Stack<PixelEditSnapshot>();
        private readonly Stack<PixelEditSnapshot> _customPixelRedo = new Stack<PixelEditSnapshot>();
        private bool _customPixelStrokeSnapshotRecorded;

        public static ScenarioSpriteSwapAuthoringService Instance
        {
            get { return ScenarioCompositionRoot.Resolve<ScenarioSpriteSwapAuthoringService>(); }
        }

        internal ScenarioSpriteSwapAuthoringService(
            ScenarioSpriteCatalogService catalogService,
            ScenarioCharacterAppearanceService characterAppearanceService,
            ScenarioSpriteRuntimeResolver runtimeResolver,
            ScenarioSpriteAnimationMetadataService animationMetadataService,
            ScenarioSpritePatchAuthoringService spritePatchAuthoringService,
            ScenarioPngImportService pngImportService,
            ScenarioAuthoringHistoryService historyService,
            IScenarioSpriteSwapEngine spriteSwapEngine,
            IScenarioSceneSpritePlacementEngine sceneSpritePlacementEngine,
            IScenarioEditorService editorService)
        {
            _catalogService = catalogService;
            _characterAppearanceService = characterAppearanceService;
            _runtimeResolver = runtimeResolver;
            _animationMetadataService = animationMetadataService;
            _spritePatchAuthoringService = spritePatchAuthoringService;
            _pngImportService = pngImportService;
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
            AnimationFrameEdit currentFrame = GetCurrentAnimationFrame();
            Texture2D activeTexture = currentFrame != null ? currentFrame.Texture : _customEditorSession.Texture;
            Sprite activeSprite = currentFrame != null ? currentFrame.PreviewSprite : _customEditorSession.PreviewSprite;
            return new CustomEditorModel
            {
                Visible = true,
                PreviewSprite = activeSprite,
                Width = activeTexture != null ? activeTexture.width : 0,
                Height = activeTexture != null ? activeTexture.height : 0,
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
                    : null,
                IsAnimationEditor = IsAnimationEditor(),
                AnimationClipName = _customEditorSession.AnimationMetadata != null ? _customEditorSession.AnimationMetadata.ClipName : null,
                AnimationFrameIndex = _customEditorSession.AnimationFrameIndex,
                AnimationFrameCount = _customEditorSession.AnimationFrames != null ? _customEditorSession.AnimationFrames.Count : 0,
                AnimationFrameDurationSeconds = currentFrame != null ? currentFrame.DurationSeconds : 0f,
                AnimationClipLengthSeconds = _customEditorSession.AnimationMetadata != null ? _customEditorSession.AnimationMetadata.ClipLengthSeconds : 0f,
                AnimationPlaying = _customEditorSession.AnimationPlaying,
                AnimationPlayingInWorld = _customEditorSession.AnimationPlayingInWorld,
                AnimationSpeed = ResolveAnimationPlaybackSpeed(),
                OnionSkin = _customEditorSession.OnionSkin,
                CompareOriginal = _customEditorSession.CompareOriginal,
                AnimationFrames = BuildAnimationFrameModels()
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

            if (state.SelectedTarget == null)
            {
                if (IsCustomEditorBoundToPickerTarget(state))
                    return false;

                ClosePickerState(state, true);
                message = "Asset editor closed because the selected target changed.";
                return true;
            }

            if (!AreSameTarget(state.SelectedTarget, state.SpriteSwapPicker.Target))
            {
                ClosePickerState(state, true);
                message = "Asset editor closed because the selected target changed.";
                return true;
            }

            return false;
        }

        public static string BuildApplyActionId(string token)
        {
            return ScenarioAuthoringActionCodec.BuildTokenActionId(ScenarioAuthoringActionIds.ActionSpriteSwapApplyPrefix, token);
        }

        public static string BuildPreviewActionId(string token)
        {
            return ScenarioAuthoringActionCodec.BuildTokenActionId(ScenarioAuthoringActionIds.ActionSpriteSwapPreviewPrefix, token);
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

        public static string BuildAnimationFrameActionId(int frameIndex)
        {
            return ScenarioAuthoringActionIds.ActionSpriteSwapAnimationFramePrefix + frameIndex;
        }

        public static string BuildAnimationCopyActionId(int frameIndex)
        {
            return ScenarioAuthoringActionIds.ActionSpriteSwapAnimationCopyPrefix + frameIndex;
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
                message = "Select a visual target before opening the asset editor.";
                return false;
            }

            ScenarioCharacterAppearanceService.ResolvedCharacterTarget characterTarget;
            string characterResolveMessage;
            if (_characterAppearanceService.TryResolve(state.SelectedTarget, out characterTarget, out message))
            {
                ClosePickerState(state, true);
                state.AssetMode = ScenarioAssetAuthoringMode.ReplaceExisting;
                state.SpriteSwapPicker = new ScenarioSpriteSwapPickerState
                {
                    IsOpen = true,
                    Target = state.SelectedTarget.Copy(),
                    TargetPath = !string.IsNullOrEmpty(state.SelectedTarget.TransformPath) ? state.SelectedTarget.TransformPath : characterTarget.TargetPath
                };
                _characterPreviewSession = _characterAppearanceService.CapturePreview(characterTarget);
                return OpenCharacterEditor(state, characterTarget, ScenarioCharacterTexturePart.Head, out message);
            }
            characterResolveMessage = message;

            SpritePickerModel model = GetPickerModel(session, state.SelectedTarget, state.ActiveScenarioFilePath);
            if (model == null || model.Target == null)
            {
                message = !string.IsNullOrEmpty(characterResolveMessage)
                    ? characterResolveMessage + " The selected target also does not expose compatible sprite replacements."
                    : "The selected target does not expose compatible sprite replacements.";
                return false;
            }

            ClosePickerState(state, true);
            state.AssetMode = ScenarioAssetAuthoringMode.ReplaceExisting;
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

            message = "Asset editor opened for '" + SafeLabel(state.SelectedTarget.DisplayName) + "'.";
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
            if (!IsPickerOpen(state))
            {
                string openMessage;
                if (!OpenPicker(state, out openMessage))
                {
                    message = openMessage;
                    return false;
                }
            }

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
            string baseSpriteId = sourceCandidate != null ? sourceCandidate.SpriteId : null;
            string baseRelativePath = sourceCandidate != null ? sourceCandidate.RelativePath : null;
            string baseRuntimeSpriteKey = sourceCandidate != null ? sourceCandidate.RuntimeSpriteKey : null;
            if (sourceCandidate == null)
            {
                SpriteSwapRule activeRule = ScenarioSpriteSwapRuleEditor.FindActiveRule(
                    session != null ? session.WorkingDefinition : null,
                    model.Target.TargetPath,
                    GetCurrentDay());
                if (activeRule != null)
                {
                    baseSpriteId = activeRule.SpriteId;
                    baseRelativePath = activeRule.RelativePath;
                    baseRuntimeSpriteKey = activeRule.RuntimeSpriteKey;
                }
            }

            if (string.IsNullOrEmpty(baseSpriteId)
                && string.IsNullOrEmpty(baseRelativePath)
                && string.IsNullOrEmpty(baseRuntimeSpriteKey))
            {
                baseRuntimeSpriteKey = ScenarioSpriteReferenceLibrary.CreateRuntimeSpriteKey(sourceSprite);
            }

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
                BaseSpriteId = baseSpriteId,
                BaseRelativePath = baseRelativePath,
                BaseRuntimeSpriteKey = baseRuntimeSpriteKey,
                AnimationPlaybackSpeed = 1f,
                LastInteractionX = 0,
                LastInteractionY = 0
            };

            AttachAnimationFramesIfAvailable(model.Target, session.WorkingDefinition, model);
            ApplyCustomEditorPreview(state);
            Rect editorWindowRect = PositionPixelEditorWindowBesideTarget(state);
            BeginCustomEditorCameraSession(state, editorWindowRect);
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
                BaseRuntimeSpriteKey = ScenarioSpriteReferenceLibrary.CreateRuntimeSpriteKey(editableTexture, sourceId),
                AnimationPlaybackSpeed = 1f,
                LastInteractionX = 0,
                LastInteractionY = 0,
                IsCharacterEditor = true,
                CharacterPart = part,
                CharacterFamilyIndex = target.FamilyIndex
            };
            Rect editorWindowRect = PositionPixelEditorWindowBesideTarget(state);
            BeginCustomEditorCameraSession(state, editorWindowRect);

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

                string patchMessage;
                string patchId = UpsertPatchSpriteAsset(definition, customTextureId, _customEditorSession.SourceLabel, state.ActiveScenarioFilePath, out patchMessage);
                if (string.IsNullOrEmpty(patchId))
                {
                    message = !string.IsNullOrEmpty(patchMessage) ? patchMessage : "Character texture patch could not be generated.";
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

        private bool ImportPngReplacement(ScenarioAuthoringState state, out string message)
        {
            if (HasCharacterEditor(state))
                return ImportCharacterPngReplacement(state, out message);

            return ImportSpritePngReplacement(state, out message);
        }

        private bool ImportSpritePngReplacement(ScenarioAuthoringState state, out string message)
        {
            message = null;
            ScenarioEditorSession session;
            SpritePickerModel model;
            if (!TryGetOpenPickerModel(state, out session, out model, out message))
                return false;

            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            if (definition == null || model.Target == null || model.Target.CurrentSprite == null)
            {
                message = "No active sprite target is available for PNG import.";
                return false;
            }

            string targetDisplay = state.SpriteSwapPicker != null && state.SpriteSwapPicker.Target != null
                ? state.SpriteSwapPicker.Target.DisplayName
                : model.Target.TargetPath;
            ScenarioPngImportService.ImportedSpriteAsset imported;
            string importMessage;
            if (!_pngImportService.TryImportLatestSpriteReplacement(
                definition,
                state.ActiveScenarioFilePath,
                targetDisplay,
                model.Target.CurrentSprite,
                out imported,
                out importMessage))
            {
                message = importMessage;
                return false;
            }

            _historyService.RecordVisualChange(definition, "Import PNG sprite replacement for " + SafeLabel(targetDisplay));
            ApplyCustomSpriteRule(definition, model.Target, imported.SpriteId, imported.RelativePath);

            MarkAssetsDirty(session);
            _spriteSwapEngine.Activate(definition, state.ActiveScenarioFilePath, null);
            ClosePickerState(state, false);
            Invalidate();

            message = (string.IsNullOrEmpty(importMessage) ? "Imported PNG replacement." : importMessage)
                + " Saved as a user-owned full replacement for '" + SafeLabel(targetDisplay) + "'.";
            MMLog.WriteInfo("[ScenarioSpriteSwapAuthoring] " + message);
            return true;
        }

        private bool ImportCharacterPngReplacement(ScenarioAuthoringState state, out string message)
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

            ScenarioCharacterTexturePart part = _customEditorSession.CharacterPart;
            string partLabel = ScenarioCharacterAppearanceService.BuildPartLabel(part);
            ScenarioCharacterAppearanceService.ResolvedCharacterTarget target;
            if (!TryResolveCharacterEditorTarget(state, out target, out message))
                return false;

            string targetDisplay = target != null ? target.DisplayName : "Character";
            ScenarioPngImportService.ImportedSpriteAsset imported;
            string importMessage;
            if (!_pngImportService.TryImportLatestCharacterTexture(
                definition,
                state.ActiveScenarioFilePath,
                targetDisplay,
                part,
                _customEditorSession.Texture,
                out imported,
                out importMessage))
            {
                message = importMessage;
                return false;
            }

            _historyService.RecordVisualChange(
                definition,
                "Import PNG character " + partLabel.ToLowerInvariant()
                + " texture for " + SafeLabel(targetDisplay));

            FamilyMemberConfig memberConfig = EnsureFamilyMemberConfig(definition, target);
            ScenarioCharacterAppearanceService.UpsertAppearance(
                memberConfig,
                part,
                imported.SpriteId,
                imported.RelativePath);

            string applyMessage;
            _characterAppearanceService.ApplyConfiguredAppearance(
                definition,
                state.ActiveScenarioFilePath,
                memberConfig,
                target.FamilyMember,
                out applyMessage);

            MarkFamilyDirty(session);
            ClosePickerState(state, false);
            Invalidate();

            message = (string.IsNullOrEmpty(importMessage) ? "Imported PNG replacement." : importMessage)
                + " Saved as a user-owned full " + partLabel.ToLowerInvariant()
                + " replacement for '" + SafeLabel(targetDisplay) + "'.";
            MMLog.WriteInfo("[ScenarioSpriteSwapAuthoring] " + message);
            return true;
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

            EnsureCustomPixelStrokeSnapshot("paint stroke");
            Color color = _customEditorSession.ActiveColor;
            if (!ScenarioPixelEditorAdapter.PaintPixel(
                _customEditorSession.Texture,
                pixelX,
                pixelY,
                color))
            {
                return false;
            }
            MarkCustomEditorDirty();
            ApplyCustomEditorPreview(state);
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
            Color sampled;
            if (!ScenarioPixelEditorAdapter.TryPickColor(
                _customEditorSession.Texture,
                pixelX,
                pixelY,
                out sampled))
            {
                message = "The selected pixel could not be sampled.";
                return false;
            }
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
            RecordCustomPixelSnapshot("pixel paste");
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
            MarkCustomEditorDirty();
            ApplyCustomEditorPreview(state);
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
            {
                if (IsAnimationEditor())
                    return SaveAnimationSprite(state, session, model, out message);
                return SaveCustomSprite(state, session, model, out message);
            }

            string previewToken = state.SpriteSwapPicker.PreviewCandidateToken;
            string savedToken = state.SpriteSwapPicker.SavedCandidateToken;
            string targetDisplay = state.SpriteSwapPicker.Target != null
                ? state.SpriteSwapPicker.Target.DisplayName
                : model.Target.TargetPath;

            if (string.IsNullOrEmpty(previewToken) || string.Equals(previewToken, savedToken, StringComparison.Ordinal))
            {
                ClosePickerState(state, false);
                message = "Closed the asset editor for '" + SafeLabel(targetDisplay) + "' without changes.";
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

                string patchMessage;
                string patchId = UpsertPatchSpriteAsset(definition, customSpriteId, _customEditorSession.SourceLabel, state.ActiveScenarioFilePath, out patchMessage);
                if (string.IsNullOrEmpty(patchId))
                {
                    message = !string.IsNullOrEmpty(patchMessage) ? patchMessage : "Custom sprite patch could not be generated.";
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
                message = "Asset editor is already closed.";
                return true;
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

            ScenarioEditorSession session = _editorService.CurrentSession;
            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            if (definition == null)
            {
                message = "No active sprite swap is available for the selected target.";
                return false;
            }

            ScenarioAuthoringTarget authoringTarget = state.SelectedTarget;
            if (authoringTarget == null && state.SpriteSwapPicker != null)
                authoringTarget = state.SpriteSwapPicker.Target;

            SpritePickerModel model = authoringTarget != null
                ? GetPickerModel(session, authoringTarget, state.ActiveScenarioFilePath)
                : null;
            string targetPath = ResolveSpriteSwapTargetPath(state, model, authoringTarget);
            string targetDisplay = ResolveSpriteSwapTargetDisplay(state, model, authoringTarget);
            if (string.IsNullOrEmpty(targetPath))
            {
                ClosePickerState(state, true);
                message = "No active sprite swap is available for the selected target.";
                return false;
            }

            ClosePickerState(state, true);

            _historyService.RecordVisualChange(definition, "Revert sprite on " + SafeLabel(targetDisplay));
            int removed = ScenarioSpriteSwapRuleEditor.ClearActiveRulesForTarget(definition, targetPath, GetCurrentDay());
            if (removed <= 0)
            {
                message = "The selected target does not have an active sprite swap.";
                string ignored;
                _historyService.Undo(definition, out ignored);
                return false;
            }

            MarkAssetsDirty(session);
            _spriteSwapEngine.Activate(definition, state.ActiveScenarioFilePath, null);
            Invalidate();
            message = removed == 1
                ? "Reverted the active sprite swap on '" + SafeLabel(targetDisplay) + "'."
                : "Reverted " + removed.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " active sprite swaps on '" + SafeLabel(targetDisplay) + "'.";
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
            if (HasCustomEditor(state))
                return UndoCustomPixels(state, out message);

            ClosePickerState(state, true);

            ScenarioEditorSession session = _editorService.CurrentSession;
            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            if (definition == null)
            {
                message = "No active authoring session.";
                return false;
            }

            string description;
            ScenarioDirtySection dirtySection;
            ScenarioEditCategory editCategory;
            ScenarioEditCategory[] allowedCategories = ResolveUndoScope(state);
            if (!_historyService.Undo(definition, allowedCategories, out description, out dirtySection, out editCategory))
            {
                message = BuildScopedHistoryUnavailableMessage(true, allowedCategories);
                return false;
            }

            MarkDirty(session, dirtySection, editCategory);
            ReapplyVisualState(definition, state.ActiveScenarioFilePath);
            Invalidate();
            message = "Undid: " + (description ?? "last change") + ".";
            MMLog.WriteInfo("[ScenarioSpriteSwapAuthoring] " + message);
            return true;
        }

        private bool Redo(ScenarioAuthoringState state, out string message)
        {
            message = null;
            if (HasCustomEditor(state))
                return RedoCustomPixels(state, out message);

            ClosePickerState(state, true);

            ScenarioEditorSession session = _editorService.CurrentSession;
            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            if (definition == null)
            {
                message = "No active authoring session.";
                return false;
            }

            string description;
            ScenarioDirtySection dirtySection;
            ScenarioEditCategory editCategory;
            ScenarioEditCategory[] allowedCategories = ResolveUndoScope(state);
            if (!_historyService.Redo(definition, allowedCategories, out description, out dirtySection, out editCategory))
            {
                message = BuildScopedHistoryUnavailableMessage(false, allowedCategories);
                return false;
            }

            MarkDirty(session, dirtySection, editCategory);
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
                message = "Asset editor is not open.";
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
            bool hadSession = _customEditorSession != null;
            if (_customEditorSession != null)
                StopWorldAnimationPreview(null);
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
            _customPixelUndo.Clear();
            _customPixelRedo.Clear();
            _customPixelStrokeSnapshotRecorded = false;
            if (hadSession)
                EndCustomEditorCameraSession();
        }

        private bool BeginCustomPixelStroke(ScenarioAuthoringState state, out string message)
        {
            message = null;
            if (!HasCustomEditor(state) || _customEditorSession.Texture == null)
            {
                message = "Custom sprite editor is not active.";
                return false;
            }

            _customPixelStrokeSnapshotRecorded = false;
            RecordCustomPixelSnapshot("paint stroke");
            _customPixelStrokeSnapshotRecorded = true;
            return true;
        }

        private ScenarioEditCategory[] ResolveUndoScope(ScenarioAuthoringState state)
        {
            if (state == null)
                return null;

            switch (state.ActiveTool)
            {
                case ScenarioAuthoringTool.Objects:
                case ScenarioAuthoringTool.Shelter:
                case ScenarioAuthoringTool.Wiring:
                    return new[] { ScenarioEditCategory.Bunker };

                case ScenarioAuthoringTool.Assets:
                    return new[] { ScenarioEditCategory.Assets };

                case ScenarioAuthoringTool.Family:
                    return new[] { ScenarioEditCategory.Family };

                case ScenarioAuthoringTool.Inventory:
                    return new[] { ScenarioEditCategory.Inventory };

                case ScenarioAuthoringTool.WinLoss:
                    return new[] { ScenarioEditCategory.WinLoss };
            }

            if (state.ActiveStage == ShelteredAPI.Scenarios.Domain.Stages.ScenarioStageKind.BunkerInside)
                return new[] { ScenarioEditCategory.Bunker, ScenarioEditCategory.Assets };

            return null;
        }

        private string BuildScopedHistoryUnavailableMessage(bool undo, ScenarioEditCategory[] allowedCategories)
        {
            if (allowedCategories == null || allowedCategories.Length == 0)
                return undo ? "Nothing to undo." : "Nothing to redo.";

            string description;
            ScenarioEditCategory topCategory;
            bool hasTop = undo
                ? _historyService.TryPeekUndo(out description, out topCategory)
                : _historyService.TryPeekRedo(out description, out topCategory);
            string scope = FormatHistoryScope(allowedCategories);
            if (!hasTop)
                return undo ? "Nothing to undo for " + scope + "." : "Nothing to redo for " + scope + ".";

            return (undo ? "Nothing to undo for " : "Nothing to redo for ")
                + scope
                + ". Next history entry is "
                + FormatEditCategory(topCategory)
                + ": "
                + (description ?? "unnamed change")
                + ".";
        }

        private static string FormatHistoryScope(ScenarioEditCategory[] allowedCategories)
        {
            if (allowedCategories == null || allowedCategories.Length == 0)
                return "this context";

            if (allowedCategories.Length == 1)
                return FormatEditCategory(allowedCategories[0]) + " edits";

            List<string> labels = new List<string>();
            for (int i = 0; i < allowedCategories.Length; i++)
                labels.Add(FormatEditCategory(allowedCategories[i]));
            return string.Join("/", labels.ToArray()) + " edits";
        }

        private static string FormatEditCategory(ScenarioEditCategory category)
        {
            switch (category)
            {
                case ScenarioEditCategory.Family:
                    return "survivor";
                case ScenarioEditCategory.Inventory:
                    return "stockpile";
                case ScenarioEditCategory.Bunker:
                    return "world";
                case ScenarioEditCategory.Triggers:
                    return "event";
                case ScenarioEditCategory.Assets:
                    return "asset";
                case ScenarioEditCategory.WinLoss:
                    return "win/loss";
                default:
                    return category.ToString().ToLowerInvariant();
            }
        }

        private bool SaveAnimationSprite(
            ScenarioAuthoringState state,
            ScenarioEditorSession session,
            SpritePickerModel model,
            out string message)
        {
            message = null;
            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            if (definition == null || !IsAnimationEditor())
            {
                message = "Animation editor is not active.";
                return false;
            }

            int dirtyCount = CountDirtyAnimationFrames();
            if (dirtyCount == 0)
            {
                ClosePickerState(state, false);
                message = "Closed the animation editor without changes.";
                return true;
            }

            string targetDisplay = state.SpriteSwapPicker != null && state.SpriteSwapPicker.Target != null
                ? state.SpriteSwapPicker.Target.DisplayName
                : model.Target.TargetPath;
            _historyService.RecordVisualChange(definition, "Apply animation frames to " + SafeLabel(targetDisplay));

            for (int i = 0; i < _customEditorSession.AnimationFrames.Count; i++)
            {
                AnimationFrameEdit frame = _customEditorSession.AnimationFrames[i];
                if (frame == null || !frame.Dirty)
                    continue;

                string frameSpriteId = _customEditorSession.CustomSpriteId + "_frame_" + frame.Index.ToString(System.Globalization.CultureInfo.InvariantCulture);
                string patchMessage;
                string patchId = UpsertPatchSpriteAsset(
                    definition,
                    frameSpriteId,
                    (_customEditorSession.SourceLabel ?? "Animation") + " frame " + (frame.Index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    _customEditorSession.BaseSpriteId,
                    _customEditorSession.BaseRelativePath,
                    frame.SourceRuntimeSpriteKey,
                    frame.BaselineTexture,
                    frame.Texture,
                    state.ActiveScenarioFilePath,
                    out patchMessage);
                if (string.IsNullOrEmpty(patchId))
                {
                    message = !string.IsNullOrEmpty(patchMessage) ? patchMessage : "Animation frame patch could not be generated.";
                    return false;
                }

                ApplyAnimationFrameRule(definition, model.Target, frame, frameSpriteId);
            }

            MarkAssetsDirty(session);
            _spriteSwapEngine.Activate(definition, state.ActiveScenarioFilePath, null);
            ClosePickerState(state, false);
            Invalidate();

            message = "Saved " + dirtyCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " edited animation frame(s) onto '" + SafeLabel(targetDisplay) + "'.";
            MMLog.WriteInfo("[ScenarioSpriteSwapAuthoring] " + message);
            return true;
        }

        private void EnsureCustomPixelStrokeSnapshot(string description)
        {
            if (_customPixelStrokeSnapshotRecorded)
                return;

            RecordCustomPixelSnapshot(description);
            _customPixelStrokeSnapshotRecorded = true;
        }

        private void RecordCustomPixelSnapshot(string description)
        {
            if (_customEditorSession == null || _customEditorSession.Texture == null)
                return;

            _customPixelUndo.Push(new PixelEditSnapshot
            {
                Description = description,
                Pixels = _customEditorSession.Texture.GetPixels(),
                Dirty = _customEditorSession.Dirty
            });
            TrimPixelUndoStack();
            _customPixelRedo.Clear();
        }

        private bool UndoCustomPixels(ScenarioAuthoringState state, out string message)
        {
            message = null;
            if (!HasCustomEditor(state) || _customEditorSession.Texture == null)
            {
                message = "Custom sprite editor is not active.";
                return false;
            }

            if (_customPixelUndo.Count == 0)
            {
                message = "Nothing to undo in the pixel editor.";
                return false;
            }

            _customPixelRedo.Push(CaptureCurrentPixelSnapshot("redo pixel edit"));
            PixelEditSnapshot snapshot = _customPixelUndo.Pop();
            RestoreCustomPixelSnapshot(state, snapshot);
            message = "Undid " + (snapshot.Description ?? "pixel edit") + ".";
            return true;
        }

        private bool RedoCustomPixels(ScenarioAuthoringState state, out string message)
        {
            message = null;
            if (!HasCustomEditor(state) || _customEditorSession.Texture == null)
            {
                message = "Custom sprite editor is not active.";
                return false;
            }

            if (_customPixelRedo.Count == 0)
            {
                message = "Nothing to redo in the pixel editor.";
                return false;
            }

            _customPixelUndo.Push(CaptureCurrentPixelSnapshot("undo pixel edit"));
            TrimPixelUndoStack();
            PixelEditSnapshot snapshot = _customPixelRedo.Pop();
            RestoreCustomPixelSnapshot(state, snapshot);
            message = "Redid " + (snapshot.Description ?? "pixel edit") + ".";
            return true;
        }

        private PixelEditSnapshot CaptureCurrentPixelSnapshot(string description)
        {
            return new PixelEditSnapshot
            {
                Description = description,
                Pixels = _customEditorSession != null && _customEditorSession.Texture != null
                    ? _customEditorSession.Texture.GetPixels()
                    : new Color[0],
                Dirty = _customEditorSession != null && _customEditorSession.Dirty
            };
        }

        private void RestoreCustomPixelSnapshot(ScenarioAuthoringState state, PixelEditSnapshot snapshot)
        {
            if (_customEditorSession == null || _customEditorSession.Texture == null || snapshot == null || snapshot.Pixels == null)
                return;

            if (snapshot.Pixels.Length != _customEditorSession.Texture.width * _customEditorSession.Texture.height)
                return;

            _customEditorSession.Texture.SetPixels(snapshot.Pixels);
            _customEditorSession.Texture.Apply();
            MarkCustomEditorDirty();
            ApplyCustomEditorPreview(state);
            _customPixelStrokeSnapshotRecorded = false;
        }

        private void TrimPixelUndoStack()
        {
            const int maxPixelUndoDepth = 50;
            if (_customPixelUndo.Count <= maxPixelUndoDepth)
                return;

            PixelEditSnapshot[] keep = _customPixelUndo.ToArray();
            _customPixelUndo.Clear();
            for (int i = keep.Length - 2; i >= 0; i--)
                _customPixelUndo.Push(keep[i]);
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

        private bool IsAnimationEditor()
        {
            return _customEditorSession != null
                && _customEditorSession.AnimationFrames != null
                && _customEditorSession.AnimationFrames.Count > 1;
        }

        private AnimationFrameEdit GetCurrentAnimationFrame()
        {
            if (!IsAnimationEditor())
                return null;

            int index = Mathf.Clamp(_customEditorSession.AnimationFrameIndex, 0, _customEditorSession.AnimationFrames.Count - 1);
            return _customEditorSession.AnimationFrames[index];
        }

        private List<AnimationFrameModel> BuildAnimationFrameModels()
        {
            List<AnimationFrameModel> result = new List<AnimationFrameModel>();
            if (!IsAnimationEditor())
                return result;

            for (int i = 0; i < _customEditorSession.AnimationFrames.Count; i++)
            {
                AnimationFrameEdit frame = _customEditorSession.AnimationFrames[i];
                if (frame == null)
                    continue;

                result.Add(new AnimationFrameModel
                {
                    Index = frame.Index,
                    OriginalSprite = frame.OriginalSprite,
                    EditedSprite = frame.PreviewSprite,
                    Dirty = frame.Dirty,
                    DurationSeconds = frame.DurationSeconds
                });
            }

            return result;
        }

        private void AttachAnimationFramesIfAvailable(
            ScenarioSpriteRuntimeResolver.ResolvedTarget target,
            ScenarioDefinition definition,
            SpritePickerModel model)
        {
            if (_customEditorSession == null || target == null || _animationMetadataService == null)
                return;

            ScenarioSpriteAnimationMetadataService.AnimationMetadata metadata = _animationMetadataService.Resolve(target);
            if (metadata == null || metadata.Frames == null || metadata.Frames.Count <= 1)
                return;

            List<AnimationFrameEdit> frames = new List<AnimationFrameEdit>();
            for (int i = 0; i < metadata.Frames.Count; i++)
            {
                ScenarioSpriteAnimationMetadataService.AnimationFrame source = metadata.Frames[i];
                if (source == null || source.Sprite == null)
                    continue;

                Texture2D editable = CreateEditableTexture(source.Sprite);
                Texture2D baseline = CreateEditableTexture(source.Sprite);
                if (editable == null || baseline == null)
                    continue;

                frames.Add(new AnimationFrameEdit
                {
                    Index = frames.Count,
                    SourceRuntimeSpriteKey = source.RuntimeSpriteKey,
                    BaselineTexture = baseline,
                    Texture = editable,
                    OriginalSprite = source.Sprite,
                    PreviewSprite = CreatePreviewSprite(editable, source.Sprite),
                    DurationSeconds = source.DurationSeconds,
                    Dirty = false
                });
            }

            if (frames.Count <= 1)
                return;

            ApplyPersistedAnimationFrameOverrides(definition, target, model, frames);
            _customEditorSession.AnimationMetadata = metadata;
            _customEditorSession.AnimationFrames = frames;
            _customEditorSession.AnimationFrameIndex = FindAnimationFrameIndex(frames, ScenarioSpriteReferenceLibrary.CreateRuntimeSpriteKey(target.CurrentSprite));
            SyncAnimationFrameToSession();
            _customEditorSession.SourceLabel = (metadata.ClipName ?? "Animation") + " (" + frames.Count + " frames)";
        }

        private static void ApplyPersistedAnimationFrameOverrides(
            ScenarioDefinition definition,
            ScenarioSpriteRuntimeResolver.ResolvedTarget target,
            SpritePickerModel model,
            List<AnimationFrameEdit> frames)
        {
            if (definition == null || target == null || model == null || frames == null)
                return;

            int currentDay = GetCurrentDay();
            for (int i = 0; i < frames.Count; i++)
            {
                AnimationFrameEdit frame = frames[i];
                if (frame == null)
                    continue;

                SpriteSwapRule rule = ScenarioSpriteSwapRuleEditor.FindAnimationFrameRule(
                    definition,
                    target.TargetPath,
                    frame.Index,
                    frame.SourceRuntimeSpriteKey,
                    currentDay);
                if (rule == null)
                    continue;

                ScenarioSpriteCatalogService.SpriteCandidate candidate = FindMatchingCandidate(model.ModdedCandidates, rule);
                if (candidate == null || candidate.Sprite == null)
                    candidate = FindMatchingCandidate(model.VanillaCandidates, rule);
                if (candidate == null || candidate.Sprite == null)
                    continue;

                Texture2D edited = CreateEditableTexture(candidate.Sprite);
                if (edited == null)
                    continue;

                if (frame.Texture != null)
                    UnityEngine.Object.Destroy(frame.Texture);
                if (frame.PreviewSprite != null)
                    UnityEngine.Object.Destroy(frame.PreviewSprite);
                frame.Texture = edited;
                frame.PreviewSprite = CreatePreviewSprite(edited, frame.OriginalSprite);
                frame.Dirty = false;
            }
        }

        private static int FindAnimationFrameIndex(List<AnimationFrameEdit> frames, string currentRuntimeSpriteKey)
        {
            if (!string.IsNullOrEmpty(currentRuntimeSpriteKey))
            {
                for (int i = 0; frames != null && i < frames.Count; i++)
                {
                    if (frames[i] != null && string.Equals(frames[i].SourceRuntimeSpriteKey, currentRuntimeSpriteKey, StringComparison.OrdinalIgnoreCase))
                        return i;
                }
            }

            return 0;
        }

        private void SyncAnimationFrameToSession()
        {
            AnimationFrameEdit frame = GetCurrentAnimationFrame();
            if (frame == null)
                return;

            _customEditorSession.Texture = frame.Texture;
            _customEditorSession.BaselineTexture = frame.BaselineTexture;
            _customEditorSession.PreviewSprite = frame.PreviewSprite;
            _customEditorSession.Dirty = HasDirtyAnimationFrames();
        }

        private bool SelectAnimationFrame(ScenarioAuthoringState state, int frameIndex, out string message)
        {
            message = null;
            if (!HasCustomEditor(state) || !IsAnimationEditor())
            {
                message = "Animation editor is not active.";
                return false;
            }

            if (frameIndex < 0 || frameIndex >= _customEditorSession.AnimationFrames.Count)
            {
                message = "Animation frame is out of range.";
                return false;
            }

            _customEditorSession.AnimationPlaying = false;
            _customEditorSession.AnimationFrameIndex = frameIndex;
            SyncAnimationFrameToSession();
            if (_customEditorSession.AnimationPlayingInWorld)
                UpdateWorldAnimationPreview(state);
            else
                ApplyCustomEditorPreview(state);
            message = "Selected frame " + (frameIndex + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "/" + _customEditorSession.AnimationFrames.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".";
            return true;
        }

        private bool StepAnimationFrame(ScenarioAuthoringState state, int delta, out string message)
        {
            message = null;
            if (!HasCustomEditor(state) || !IsAnimationEditor())
            {
                message = "Animation editor is not active.";
                return false;
            }

            int count = _customEditorSession.AnimationFrames.Count;
            int next = (_customEditorSession.AnimationFrameIndex + delta) % count;
            if (next < 0)
                next += count;
            return SelectAnimationFrame(state, next, out message);
        }

        private bool ToggleAnimationPlayback(ScenarioAuthoringState state, out string message)
        {
            message = null;
            if (!HasCustomEditor(state) || !IsAnimationEditor())
            {
                message = "Animation editor is not active.";
                return false;
            }

            _customEditorSession.AnimationPlaying = !_customEditorSession.AnimationPlaying;
            _customEditorSession.AnimationPlaybackAccumulator = 0f;
            message = _customEditorSession.AnimationPlaying ? "Animation preview playing." : "Animation preview paused.";
            return true;
        }

        private bool ToggleAnimationWorldPlayback(ScenarioAuthoringState state, out string message)
        {
            message = null;
            if (!HasCustomEditor(state) || !IsAnimationEditor())
            {
                message = "Animation editor is not active.";
                return false;
            }

            _customEditorSession.AnimationPlayingInWorld = !_customEditorSession.AnimationPlayingInWorld;
            if (_customEditorSession.AnimationPlayingInWorld)
            {
                UpdateWorldAnimationPreview(state);
                message = "World animation preview playing.";
            }
            else
            {
                StopWorldAnimationPreview(state);
                message = "World animation preview stopped.";
            }

            return true;
        }

        private bool SetAnimationPlaybackSpeed(ScenarioAuthoringState state, float speed, out string message)
        {
            message = null;
            if (!HasCustomEditor(state) || !IsAnimationEditor())
            {
                message = "Animation editor is not active.";
                return false;
            }

            float normalized = Mathf.Clamp(speed, 0.25f, 2f);
            if (Mathf.Abs(ResolveAnimationPlaybackSpeed() - normalized) <= 0.001f)
                return false;

            _customEditorSession.AnimationPlaybackSpeed = normalized;
            if (_customEditorSession.AnimationPlayingInWorld)
                UpdateWorldAnimationPreview(state);
            message = "Animation preview speed set to " + normalized.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "x.";
            return true;
        }

        private bool ToggleOnionSkin(ScenarioAuthoringState state, out string message)
        {
            message = null;
            if (!HasCustomEditor(state) || !IsAnimationEditor())
            {
                message = "Animation editor is not active.";
                return false;
            }

            _customEditorSession.OnionSkin = !_customEditorSession.OnionSkin;
            message = _customEditorSession.OnionSkin ? "Onion skin enabled." : "Onion skin disabled.";
            return true;
        }

        private bool ToggleOriginalComparison(ScenarioAuthoringState state, out string message)
        {
            message = null;
            if (!HasCustomEditor(state) || !IsAnimationEditor())
            {
                message = "Animation editor is not active.";
                return false;
            }

            _customEditorSession.CompareOriginal = !_customEditorSession.CompareOriginal;
            message = _customEditorSession.CompareOriginal ? "Original comparison enabled." : "Original comparison disabled.";
            return true;
        }

        private bool CopyAnimationFrameFrom(ScenarioAuthoringState state, int sourceFrameIndex, out string message)
        {
            message = null;
            if (!HasCustomEditor(state) || !IsAnimationEditor())
            {
                message = "Animation editor is not active.";
                return false;
            }

            AnimationFrameEdit target = GetCurrentAnimationFrame();
            if (target == null || sourceFrameIndex < 0 || sourceFrameIndex >= _customEditorSession.AnimationFrames.Count)
            {
                message = "Animation frame copy is out of range.";
                return false;
            }

            AnimationFrameEdit source = _customEditorSession.AnimationFrames[sourceFrameIndex];
            if (source == null || source.Texture == null || target.Texture == null || source.Texture.width != target.Texture.width || source.Texture.height != target.Texture.height)
            {
                message = "The selected source frame is not compatible with the current frame.";
                return false;
            }

            RecordCustomPixelSnapshot("copy frame");
            target.Texture.SetPixels(source.Texture.GetPixels());
            target.Texture.Apply();
            target.PreviewSprite = CreatePreviewSprite(target.Texture, target.OriginalSprite);
            target.Dirty = true;
            SyncAnimationFrameToSession();
            if (_customEditorSession.AnimationPlayingInWorld)
                UpdateWorldAnimationPreview(state);
            else
                ApplyCustomEditorPreview(state);
            message = "Copied frame " + (sourceFrameIndex + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " into frame " + (target.Index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + ".";
            return true;
        }

        private bool RevertAnimationFrame(ScenarioAuthoringState state, out string message)
        {
            message = null;
            if (!HasCustomEditor(state) || !IsAnimationEditor())
            {
                message = "Animation editor is not active.";
                return false;
            }

            AnimationFrameEdit frame = GetCurrentAnimationFrame();
            if (frame == null || frame.BaselineTexture == null || frame.Texture == null)
            {
                message = "Animation frame is unavailable.";
                return false;
            }

            frame.Texture.SetPixels(frame.BaselineTexture.GetPixels());
            frame.Texture.Apply();
            frame.PreviewSprite = CreatePreviewSprite(frame.Texture, frame.OriginalSprite);
            frame.Dirty = false;
            SyncAnimationFrameToSession();
            int removed = ClearPersistedAnimationFrameRule(state, frame);
            if (_customEditorSession.AnimationPlayingInWorld)
                UpdateWorldAnimationPreview(state);
            else
                ApplyCustomEditorPreview(state);
            message = "Reverted frame " + (frame.Index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)
                + (removed > 0 ? " and cleared its saved override." : ".");
            return true;
        }

        private bool RevertAnimation(ScenarioAuthoringState state, out string message)
        {
            message = null;
            if (!HasCustomEditor(state) || !IsAnimationEditor())
            {
                message = "Animation editor is not active.";
                return false;
            }

            for (int i = 0; i < _customEditorSession.AnimationFrames.Count; i++)
            {
                AnimationFrameEdit frame = _customEditorSession.AnimationFrames[i];
                if (frame == null || frame.BaselineTexture == null || frame.Texture == null)
                    continue;

                frame.Texture.SetPixels(frame.BaselineTexture.GetPixels());
                frame.Texture.Apply();
                frame.PreviewSprite = CreatePreviewSprite(frame.Texture, frame.OriginalSprite);
                frame.Dirty = false;
            }

            SyncAnimationFrameToSession();
            int removed = ClearPersistedAnimationFrameRules(state);
            if (_customEditorSession.AnimationPlayingInWorld)
                UpdateWorldAnimationPreview(state);
            else
                ApplyCustomEditorPreview(state);
            message = removed > 0
                ? "Reverted all animation frames and cleared "
                    + removed.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " saved frame override(s)."
                : "Reverted all animation frames.";
            return true;
        }

        private int ClearPersistedAnimationFrameRule(ScenarioAuthoringState state, AnimationFrameEdit frame)
        {
            if (state == null || frame == null || _customEditorSession == null)
                return 0;

            ScenarioEditorSession session = _editorService.CurrentSession;
            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            if (definition == null)
                return 0;

            string targetPath = ResolveCustomEditorTargetPath(state);
            if (string.IsNullOrEmpty(targetPath))
                return 0;

            int currentDay = GetCurrentDay();
            if (!ScenarioSpriteSwapRuleEditor.HasAnimationFrameRule(definition, targetPath, frame.Index, frame.SourceRuntimeSpriteKey, currentDay))
                return 0;

            _historyService.RecordVisualChange(
                definition,
                "Revert animation frame " + (frame.Index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " on " + SafeLabel(ResolveCustomEditorTargetDisplay(state, targetPath)));
            int removed = ScenarioSpriteSwapRuleEditor.ClearAnimationFrameRule(
                definition,
                targetPath,
                frame.Index,
                frame.SourceRuntimeSpriteKey,
                currentDay);
            if (removed <= 0)
            {
                string ignored;
                _historyService.Undo(definition, out ignored);
                return 0;
            }

            MarkAssetsDirty(session);
            _spriteSwapEngine.Activate(definition, state.ActiveScenarioFilePath, null);
            Invalidate();
            return removed;
        }

        private int ClearPersistedAnimationFrameRules(ScenarioAuthoringState state)
        {
            if (state == null || _customEditorSession == null)
                return 0;

            ScenarioEditorSession session = _editorService.CurrentSession;
            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            if (definition == null)
                return 0;

            string targetPath = ResolveCustomEditorTargetPath(state);
            if (string.IsNullOrEmpty(targetPath))
                return 0;

            _historyService.RecordVisualChange(
                definition,
                "Revert animation on " + SafeLabel(ResolveCustomEditorTargetDisplay(state, targetPath)));
            int removed = ScenarioSpriteSwapRuleEditor.ClearAnimationFrameRules(definition, targetPath, GetCurrentDay());
            if (removed <= 0)
            {
                string ignored;
                _historyService.Undo(definition, out ignored);
                return 0;
            }

            MarkAssetsDirty(session);
            _spriteSwapEngine.Activate(definition, state.ActiveScenarioFilePath, null);
            Invalidate();
            return removed;
        }

        public void TickAnimationPreview(ScenarioAuthoringState state)
        {
            if (!HasCustomEditor(state) || !IsAnimationEditor() || !_customEditorSession.AnimationPlaying)
                return;

            AnimationFrameEdit frame = GetCurrentAnimationFrame();
            if (frame == null)
                return;

            _customEditorSession.AnimationPlaybackAccumulator += Time.unscaledDeltaTime * ResolveAnimationPlaybackSpeed();
            if (_customEditorSession.AnimationPlaybackAccumulator < Mathf.Max(0.01f, frame.DurationSeconds))
                return;

            _customEditorSession.AnimationPlaybackAccumulator = 0f;
            int count = _customEditorSession.AnimationFrames.Count;
            _customEditorSession.AnimationFrameIndex = (_customEditorSession.AnimationFrameIndex + 1) % count;
            SyncAnimationFrameToSession();
            if (!_customEditorSession.AnimationPlayingInWorld)
                ApplyCustomEditorPreview(state);
        }

        private float ResolveAnimationPlaybackSpeed()
        {
            if (_customEditorSession == null || _customEditorSession.AnimationPlaybackSpeed <= 0f)
                return 1f;

            return Mathf.Clamp(_customEditorSession.AnimationPlaybackSpeed, 0.25f, 2f);
        }

        private bool HasDirtyAnimationFrames()
        {
            return CountDirtyAnimationFrames() > 0;
        }

        private void MarkCustomEditorDirty()
        {
            if (_customEditorSession == null)
                return;

            AnimationFrameEdit frame = GetCurrentAnimationFrame();
            if (frame != null)
            {
                frame.Dirty = true;
                if (frame.PreviewSprite == null)
                    frame.PreviewSprite = CreatePreviewSprite(frame.Texture, frame.OriginalSprite);
                _customEditorSession.PreviewSprite = frame.PreviewSprite;
            }

            _customEditorSession.Dirty = true;
        }

        private int CountDirtyAnimationFrames()
        {
            int count = 0;
            for (int i = 0; _customEditorSession != null && _customEditorSession.AnimationFrames != null && i < _customEditorSession.AnimationFrames.Count; i++)
            {
                if (_customEditorSession.AnimationFrames[i] != null && _customEditorSession.AnimationFrames[i].Dirty)
                    count++;
            }

            return count;
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

            session.MarkDraftChanged(ScenarioDirtySection.Family, ScenarioEditCategory.Family);
            session.MarkDraftChanged(ScenarioDirtySection.Assets, ScenarioEditCategory.Family);
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
                    UserOwned = item.UserOwned,
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

        private static string ResolveSpriteSwapTargetPath(
            ScenarioAuthoringState state,
            SpritePickerModel model,
            ScenarioAuthoringTarget authoringTarget)
        {
            if (model != null && model.Target != null && !string.IsNullOrEmpty(model.Target.TargetPath))
                return model.Target.TargetPath;

            if (state != null && state.SpriteSwapPicker != null && !string.IsNullOrEmpty(state.SpriteSwapPicker.TargetPath))
                return state.SpriteSwapPicker.TargetPath;

            return authoringTarget != null ? authoringTarget.TransformPath : null;
        }

        private static string ResolveSpriteSwapTargetDisplay(
            ScenarioAuthoringState state,
            SpritePickerModel model,
            ScenarioAuthoringTarget authoringTarget)
        {
            if (authoringTarget != null && !string.IsNullOrEmpty(authoringTarget.DisplayName))
                return authoringTarget.DisplayName;

            if (state != null
                && state.SpriteSwapPicker != null
                && state.SpriteSwapPicker.Target != null
                && !string.IsNullOrEmpty(state.SpriteSwapPicker.Target.DisplayName))
            {
                return state.SpriteSwapPicker.Target.DisplayName;
            }

            if (model != null && model.Target != null && !string.IsNullOrEmpty(model.Target.TargetPath))
                return model.Target.TargetPath;

            if (state != null && state.SpriteSwapPicker != null)
                return state.SpriteSwapPicker.TargetPath;

            return "target";
        }

        private string ResolveCustomEditorTargetPath(ScenarioAuthoringState state)
        {
            if (_customEditorSession != null && !string.IsNullOrEmpty(_customEditorSession.TargetPath))
                return _customEditorSession.TargetPath;

            if (state != null && state.SpriteSwapPicker != null && !string.IsNullOrEmpty(state.SpriteSwapPicker.TargetPath))
                return state.SpriteSwapPicker.TargetPath;

            return state != null && state.SpriteSwapPicker != null && state.SpriteSwapPicker.Target != null
                ? state.SpriteSwapPicker.Target.TransformPath
                : null;
        }

        private string ResolveCustomEditorTargetDisplay(ScenarioAuthoringState state, string fallback)
        {
            if (state != null
                && state.SpriteSwapPicker != null
                && state.SpriteSwapPicker.Target != null
                && !string.IsNullOrEmpty(state.SpriteSwapPicker.Target.DisplayName))
            {
                return state.SpriteSwapPicker.Target.DisplayName;
            }

            return !string.IsNullOrEmpty(fallback) ? fallback : "target";
        }

        private bool IsCustomEditorBoundToPickerTarget(ScenarioAuthoringState state)
        {
            if (_customEditorSession == null || state == null || state.SpriteSwapPicker == null)
                return false;

            return !string.IsNullOrEmpty(_customEditorSession.TargetPath)
                && !string.IsNullOrEmpty(state.SpriteSwapPicker.TargetPath)
                && string.Equals(_customEditorSession.TargetPath, state.SpriteSwapPicker.TargetPath, StringComparison.OrdinalIgnoreCase);
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

        private string UpsertPatchSpriteAsset(
            ScenarioDefinition definition,
            string spriteId,
            string displayName,
            string scenarioFilePath,
            out string message)
        {
            message = null;
            if (definition == null || _customEditorSession == null || string.IsNullOrEmpty(spriteId))
            {
                message = "No active custom sprite edit was available to save.";
                return null;
            }

            return UpsertPatchSpriteAsset(
                definition,
                spriteId,
                displayName,
                _customEditorSession.BaseSpriteId,
                _customEditorSession.BaseRelativePath,
                _customEditorSession.BaseRuntimeSpriteKey,
                _customEditorSession.BaselineTexture,
                _customEditorSession.Texture,
                scenarioFilePath,
                out message);
        }

        private string UpsertPatchSpriteAsset(
            ScenarioDefinition definition,
            string spriteId,
            string displayName,
            string baseSpriteId,
            string baseRelativePath,
            string baseRuntimeSpriteKey,
            Texture2D baselineTexture,
            Texture2D editedTexture,
            string scenarioFilePath,
            out string message)
        {
            message = null;
            if (definition == null || string.IsNullOrEmpty(spriteId))
            {
                message = "No animation frame edit was available to save.";
                return null;
            }

            string packRoot = !string.IsNullOrEmpty(scenarioFilePath) ? Path.GetDirectoryName(scenarioFilePath) : null;
            if (baselineTexture == null || string.IsNullOrEmpty(packRoot))
            {
                message = "Scenario pack path and baseline pixels are required to save a deterministic sprite patch.";
                return null;
            }

            try
            {
                string safeFileName = spriteId;
                char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
                for (int i = 0; i < invalidFileNameChars.Length; i++)
                    safeFileName = safeFileName.Replace(invalidFileNameChars[i], '_');
                safeFileName = safeFileName.Replace('/', '_').Replace('\\', '_').Replace(':', '_');

                baseRelativePath = "Assets/PixelPatchBases/" + safeFileName + ".base.png";
                string baselinePath = Path.Combine(packRoot, baseRelativePath.Replace('/', Path.DirectorySeparatorChar));
                string baselineDirectory = Path.GetDirectoryName(baselinePath);
                if (!Directory.Exists(baselineDirectory))
                    Directory.CreateDirectory(baselineDirectory);

                byte[] encodedBaseline = baselineTexture.EncodeToPNG();
                if (encodedBaseline == null || encodedBaseline.Length == 0)
                {
                    message = "The pixel editor baseline could not be encoded for deterministic reloads.";
                    return null;
                }

                File.WriteAllBytes(baselinePath, encodedBaseline);
                baseSpriteId = null;
                baseRuntimeSpriteKey = null;
            }
            catch (Exception ex)
            {
                message = "The pixel editor baseline could not be saved: " + ex.Message;
                return null;
            }

            return _spritePatchAuthoringService.UpsertPatchSpriteAsset(
                definition,
                spriteId,
                displayName,
                baseSpriteId,
                baseRelativePath,
                baseRuntimeSpriteKey,
                baselineTexture,
                editedTexture,
                out message);
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
            rule.AnimationFrameIndex = null;
            rule.AnimationFrameRuntimeSpriteKey = null;
        }

        private static void ApplyAnimationFrameRule(
            ScenarioDefinition definition,
            ScenarioSpriteRuntimeResolver.ResolvedTarget target,
            AnimationFrameEdit frame,
            string spriteId)
        {
            if (definition == null || target == null || frame == null || string.IsNullOrEmpty(frame.SourceRuntimeSpriteKey))
                return;

            int currentDay = GetCurrentDay();
            ScenarioSpriteSwapRuleEditor.EnsureAssetReferences(definition);
            SpriteSwapRule rule = ScenarioSpriteSwapRuleEditor.FindAnimationFrameRule(
                definition,
                target.TargetPath,
                frame.Index,
                frame.SourceRuntimeSpriteKey,
                currentDay);

            if (rule == null)
            {
                rule = new SpriteSwapRule
                {
                    Id = ScenarioSpriteSwapRuleEditor.BuildRuleId(target.TargetPath) + "_frame_" + frame.Index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Day = 1
                };
                definition.AssetReferences.SpriteSwaps.Add(rule);
            }

            rule.TargetPath = target.TargetPath;
            rule.TargetComponent = target.Kind;
            rule.RuntimeSpriteKey = null;
            rule.SpriteId = spriteId;
            rule.RelativePath = null;
            rule.AnimationFrameIndex = frame.Index;
            rule.AnimationFrameRuntimeSpriteKey = frame.SourceRuntimeSpriteKey;
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

        private void ApplyCustomEditorPreview(ScenarioAuthoringState state)
        {
            if (!HasCustomEditor(state) || state.SpriteSwapPicker == null || _customEditorSession.PreviewSprite == null)
                return;

            if (IsAnimationEditor() && _customEditorSession.AnimationPlayingInWorld)
            {
                UpdateWorldAnimationPreview(state);
                return;
            }

            ScenarioEditorSession session;
            SpritePickerModel model;
            string message;
            if (!TryGetOpenPickerModel(state, out session, out model, out message) || model == null || model.Target == null)
                return;

            if (IsAnimationEditor())
                ScenarioSpriteRuntimeMutationService.TryPreviewEditedFrame(model.Target, _customEditorSession.PreviewSprite);
            else
                ScenarioSpriteRuntimeMutationService.TryApply(model.Target, _customEditorSession.PreviewSprite);
        }

        private void UpdateWorldAnimationPreview(ScenarioAuthoringState state)
        {
            if (!HasCustomEditor(state) || !IsAnimationEditor())
                return;

            ScenarioEditorSession session;
            SpritePickerModel model;
            string message;
            if (!TryGetOpenPickerModel(state, out session, out model, out message) || model == null || model.Target == null)
                return;

            List<Sprite> sprites = new List<Sprite>();
            List<float> durations = new List<float>();
            for (int i = 0; i < _customEditorSession.AnimationFrames.Count; i++)
            {
                AnimationFrameEdit frame = _customEditorSession.AnimationFrames[i];
                if (frame == null || frame.PreviewSprite == null)
                    continue;

                sprites.Add(frame.PreviewSprite);
                durations.Add(Mathf.Max(0.01f, frame.DurationSeconds));
            }

            ScenarioSpriteRuntimeMutationService.TryPlayEditedAnimation(
                model.Target,
                sprites,
                durations,
                ResolveAnimationPlaybackSpeed());
        }

        private void StopWorldAnimationPreview(ScenarioAuthoringState state)
        {
            string targetPath = _customEditorSession != null ? _customEditorSession.TargetPath : null;
            if (state != null && state.SpriteSwapPicker != null && !string.IsNullOrEmpty(state.SpriteSwapPicker.TargetPath))
                targetPath = state.SpriteSwapPicker.TargetPath;
            if (string.IsNullOrEmpty(targetPath))
                return;

            ScenarioSpriteTargetComponentKind targetKind = _previewSession != null
                ? _previewSession.TargetKind
                : ScenarioSpriteTargetComponentKind.SpriteRenderer;
            ScenarioSpriteRuntimeResolver.ResolvedTarget runtimeTarget;
            if (_runtimeResolver.TryResolve(targetPath, targetKind, out runtimeTarget) && runtimeTarget != null)
                ScenarioSpriteRuntimeMutationService.StopEditedAnimation(runtimeTarget);
        }

        private static void BeginCustomEditorCameraSession(ScenarioAuthoringState state, Rect editorWindowRect)
        {
            try
            {
                ScenarioAuthoringEditorCameraService cameraService = ScenarioCompositionRoot.Resolve<ScenarioAuthoringEditorCameraService>();
                if (cameraService != null && state != null && state.SpriteSwapPicker != null)
                    cameraService.BeginPixelEditorSession(
                        state.SpriteSwapPicker.Target,
                        state.SpriteSwapPicker.TargetPath,
                        editorWindowRect);
            }
            catch
            {
            }
        }

        private static void EndCustomEditorCameraSession()
        {
            try
            {
                ScenarioAuthoringEditorCameraService cameraService = ScenarioCompositionRoot.Resolve<ScenarioAuthoringEditorCameraService>();
                if (cameraService != null)
                    cameraService.EndPixelEditorSession();
            }
            catch
            {
            }
        }

        private static Rect PositionPixelEditorWindowBesideTarget(ScenarioAuthoringState state)
        {
            if (state == null || state.WindowStates == null || state.SpriteSwapPicker == null || state.SpriteSwapPicker.Target == null)
                return new Rect(0f, 0f, 1060f, 560f);

            ScenarioAuthoringWindowState window = FindWindowState(state, "pixel_editor");
            if (window == null)
                return new Rect(0f, 0f, 1060f, 560f);

            float width = Math.Max(900f, window.Width > 0f ? window.Width : 1060f);
            float height = Math.Max(500f, window.Height > 0f ? window.Height : 560f);
            Rect workspace = ResolveFreeWorkspaceRect();
            float x = workspace.x + Math.Max(0f, workspace.width - width - 18f);
            float y = workspace.y + Math.Max(0f, (workspace.height * 0.20f) - (height * 0.5f));

            window.HasCustomBounds = true;
            window.Width = width;
            window.Height = height;
            window.X = Mathf.Clamp(x, 18f, Math.Max(18f, Screen.width - width - 18f));
            window.Y = Mathf.Clamp(y, 112f, Math.Max(112f, Screen.height - height - 64f));
            window.Visible = true;
            window.Collapsed = false;
            return new Rect(window.X, window.Y, window.Width, window.Height);
        }

        private static Rect ResolveFreeWorkspaceRect()
        {
            float top = 112f;
            float bottom = 64f;
            return new Rect(
                18f,
                top,
                Math.Max(320f, Screen.width - 36f),
                Math.Max(240f, Screen.height - top - bottom));
        }

        private static ScenarioAuthoringWindowState FindWindowState(ScenarioAuthoringState state, string id)
        {
            for (int i = 0; state != null && state.WindowStates != null && i < state.WindowStates.Count; i++)
            {
                ScenarioAuthoringWindowState window = state.WindowStates[i];
                if (window != null && string.Equals(window.Id, id, StringComparison.OrdinalIgnoreCase))
                    return window;
            }

            return null;
        }

        private static void MarkAssetsDirty(ScenarioEditorSession session)
        {
            if (session == null)
                return;

            session.MarkDraftChanged(ScenarioDirtySection.Assets, ScenarioEditCategory.Assets);
        }

        private static void MarkDirty(
            ScenarioEditorSession session,
            ScenarioDirtySection dirtySection,
            ScenarioEditCategory editCategory)
        {
            if (session == null)
                return;

            if (dirtySection == ScenarioDirtySection.None)
                dirtySection = ScenarioDirtySection.Assets;
            session.MarkDraftChanged(dirtySection, editCategory);
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
            return ScenarioAuthoringActionCodec.DecodeToken(encoded);
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
