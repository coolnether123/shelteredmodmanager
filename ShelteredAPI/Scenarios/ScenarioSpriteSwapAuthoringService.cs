using System;
using System.Collections.Generic;
using System.Text;
using ModAPI.Core;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    // Thin orchestrator for the sprite-swap authoring workflow. Catalog queries live
    // in ScenarioSpriteCatalogService; rule mutation in ScenarioSpriteSwapRuleEditor;
    // undo/redo in ScenarioAuthoringHistoryService; clipboard in
    // ScenarioSpriteSwapClipboard. This class composes them and translates inspector
    // action ids into concrete intent.
    internal sealed class ScenarioSpriteSwapAuthoringService
    {
        internal sealed class SpritePickerModel
        {
            public ScenarioSpriteRuntimeResolver.ResolvedTarget Target;
            public List<ScenarioSpriteCatalogService.SpriteCandidate> VanillaCandidates;
            public List<ScenarioSpriteCatalogService.SpriteCandidate> ModdedCandidates;
            public bool HasActiveRule;
            public string ActiveRuleSummary;
            public bool FamilyFiltered;
            public string CompatibilitySummary;
            public string GuidanceMessage;
            public string XmlPathHint;
        }

        private static readonly ScenarioSpriteSwapAuthoringService _instance = new ScenarioSpriteSwapAuthoringService();
        private readonly ScenarioSpriteCatalogService _catalogService = new ScenarioSpriteCatalogService();

        public static ScenarioSpriteSwapAuthoringService Instance
        {
            get { return _instance; }
        }

        private ScenarioSpriteSwapAuthoringService()
        {
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
            MarkActiveCandidate(model.VanillaCandidates, activeRule);
            MarkActiveCandidate(model.ModdedCandidates, activeRule);
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

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapClear, StringComparison.Ordinal)
                || string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapRevert, StringComparison.Ordinal))
            {
                handled = true;
                return ClearActiveSwap(state, out message);
            }

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapCopy, StringComparison.Ordinal))
            {
                handled = true;
                return CopyActiveSwap(state, out message);
            }

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapPaste, StringComparison.Ordinal))
            {
                handled = true;
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

            return ApplyCandidate(state, token, out message);
        }

        public static string BuildApplyActionId(string token)
        {
            if (string.IsNullOrEmpty(token))
                return ScenarioAuthoringActionIds.ActionSpriteSwapApplyPrefix;

            byte[] bytes = Encoding.UTF8.GetBytes(token);
            return ScenarioAuthoringActionIds.ActionSpriteSwapApplyPrefix + Convert.ToBase64String(bytes);
        }

        private bool ApplyCandidate(ScenarioAuthoringState state, string token, out string message)
        {
            message = null;
            ScenarioEditorSession session = ScenarioEditorController.Instance.CurrentSession;
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
            ScenarioAuthoringHistoryService.Instance.RecordSpriteSwapChange(definition, "Apply sprite to " + SafeLabel(targetDisplay));
            ScenarioSpriteSwapRuleEditor.ApplyCandidate(definition, model.Target, candidate, GetCurrentDay());

            string kindLabel = candidate.SourceKind == ScenarioSpriteCatalogService.SpriteCandidateSourceKind.VanillaRuntime
                ? "vanilla sprite preview"
                : "modded sprite preview";
            message = "Applied " + kindLabel + " '" + SafeLabel(candidate.Label) + "' to '" + SafeLabel(targetDisplay) + "'.";

            MarkAssetsDirty(session);
            ScenarioSpriteSwapService.Instance.Activate(definition, state.ActiveScenarioFilePath, null);
            Invalidate();
            MMLog.WriteInfo("[ScenarioSpriteSwapAuthoring] " + message);
            return true;
        }

        private bool ClearActiveSwap(ScenarioAuthoringState state, out string message)
        {
            message = null;
            ScenarioEditorSession session = ScenarioEditorController.Instance.CurrentSession;
            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            if (definition == null || state.SelectedTarget == null)
            {
                message = "No active sprite swap is available for the selected target.";
                return false;
            }

            SpritePickerModel model = GetPickerModel(session, state.SelectedTarget, state.ActiveScenarioFilePath);
            string targetPath = model != null && model.Target != null ? model.Target.TargetPath : state.SelectedTarget.TransformPath;
            string targetDisplay = state.SelectedTarget.DisplayName;

            ScenarioAuthoringHistoryService.Instance.RecordSpriteSwapChange(definition, "Revert sprite on " + SafeLabel(targetDisplay));
            if (!ScenarioSpriteSwapRuleEditor.ClearActiveRule(definition, targetPath, GetCurrentDay()))
            {
                message = "The selected target does not have an active sprite swap.";
                // Undo the pointless snapshot we just pushed.
                string ignored;
                ScenarioAuthoringHistoryService.Instance.Undo(definition, out ignored);
                return false;
            }

            MarkAssetsDirty(session);
            ScenarioSpriteSwapService.Instance.Activate(definition, state.ActiveScenarioFilePath, null);
            Invalidate();
            message = "Reverted the active sprite swap on '" + SafeLabel(targetDisplay) + "'.";
            MMLog.WriteInfo("[ScenarioSpriteSwapAuthoring] " + message);
            return true;
        }

        private bool CopyActiveSwap(ScenarioAuthoringState state, out string message)
        {
            message = null;
            ScenarioEditorSession session = ScenarioEditorController.Instance.CurrentSession;
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
            if (!ScenarioSpriteSwapClipboard.HasRule)
            {
                message = "Clipboard is empty. Copy a sprite swap first.";
                return false;
            }

            ScenarioEditorSession session = ScenarioEditorController.Instance.CurrentSession;
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

            ScenarioAuthoringHistoryService.Instance.RecordSpriteSwapChange(definition, "Paste sprite to " + SafeLabel(state.SelectedTarget.DisplayName));

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
            ScenarioSpriteSwapService.Instance.Activate(definition, state.ActiveScenarioFilePath, null);
            Invalidate();
            message = "Pasted sprite swap '" + ScenarioSpriteSwapRuleEditor.DescribeRuleShort(rule)
                + "' onto '" + SafeLabel(state.SelectedTarget.DisplayName) + "'.";
            MMLog.WriteInfo("[ScenarioSpriteSwapAuthoring] " + message);
            return true;
        }

        private bool Undo(ScenarioAuthoringState state, out string message)
        {
            message = null;
            ScenarioEditorSession session = ScenarioEditorController.Instance.CurrentSession;
            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            if (definition == null)
            {
                message = "No active authoring session.";
                return false;
            }

            string description;
            if (!ScenarioAuthoringHistoryService.Instance.Undo(definition, out description))
            {
                message = "Nothing to undo.";
                return false;
            }

            MarkAssetsDirty(session);
            ScenarioSpriteSwapService.Instance.Activate(definition, state.ActiveScenarioFilePath, null);
            Invalidate();
            message = "Undid: " + (description ?? "last change") + ".";
            MMLog.WriteInfo("[ScenarioSpriteSwapAuthoring] " + message);
            return true;
        }

        private bool Redo(ScenarioAuthoringState state, out string message)
        {
            message = null;
            ScenarioEditorSession session = ScenarioEditorController.Instance.CurrentSession;
            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            if (definition == null)
            {
                message = "No active authoring session.";
                return false;
            }

            string description;
            if (!ScenarioAuthoringHistoryService.Instance.Redo(definition, out description))
            {
                message = "Nothing to redo.";
                return false;
            }

            MarkAssetsDirty(session);
            ScenarioSpriteSwapService.Instance.Activate(definition, state.ActiveScenarioFilePath, null);
            Invalidate();
            message = "Redid: " + (description ?? "change") + ".";
            MMLog.WriteInfo("[ScenarioSpriteSwapAuthoring] " + message);
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

        private static void MarkActiveCandidate(List<ScenarioSpriteCatalogService.SpriteCandidate> candidates, SpriteSwapRule activeRule)
        {
            for (int i = 0; candidates != null && i < candidates.Count; i++)
            {
                ScenarioSpriteCatalogService.SpriteCandidate candidate = candidates[i];
                if (candidate == null)
                    continue;

                candidate.Hint = BuildCandidateHint(candidate, activeRule);
                if (ScenarioSpriteSwapRuleEditor.RuleMatchesCandidate(activeRule, candidate))
                    candidate.Label = candidate.Label + " *";
            }
        }

        private static string BuildCandidateHint(ScenarioSpriteCatalogService.SpriteCandidate candidate, SpriteSwapRule activeRule)
        {
            string hint = candidate != null ? candidate.Hint : null;
            if (candidate != null && ScenarioSpriteSwapRuleEditor.RuleMatchesCandidate(activeRule, candidate))
                return string.IsNullOrEmpty(hint) ? "Currently active for this target." : (hint + " | Currently active for this target.");
            return hint;
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
