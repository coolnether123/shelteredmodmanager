using System;

using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredAPI.Scenarios.Application.Authoring.Templates
{
    /// <summary>Instantiates bundled template XML into an existing wizard draft.</summary>
    internal static class ScenarioStarterTemplateDraftService
    {
        internal static bool TryApply(
            ScenarioEditorSession editorSession,
            string templateKey,
            string activeDraftId,
            ScenarioAuthoringBaseModeReloadService reloadService,
            out string templateTitle,
            out string message)
        {
            templateTitle = null;
            message = null;
            ScenarioStarterTemplate template;
            if (editorSession == null || editorSession.WorkingDefinition == null
                || !ScenarioStarterTemplateCatalog.TryGet(templateKey, out template))
            {
                message = "Starter template could not be loaded.";
                return false;
            }

            ScenarioDefinition definition;
            try
            {
                definition = template.CreateDefinition();
            }
            catch (Exception ex)
            {
                message = "Starter template XML could not be loaded: " + ex.Message;
                return false;
            }

            if (definition == null)
            {
                message = "Starter template produced no scenario definition.";
                return false;
            }

            definition.Id = !string.IsNullOrEmpty(activeDraftId) ? activeDraftId : editorSession.WorkingDefinition.Id;
            if (definition.SelectionRules == null)
                definition.SelectionRules = ScenarioSelectionRulesDefinition.ForBaseMode(definition.BaseGameMode);
            templateTitle = template.Title;
            editorSession.WorkingDefinition = definition;
            MarkAllSectionsChanged(editorSession);

            if (reloadService == null
                || !reloadService.SaveAndReloadBaseline(editorSession, definition.BaseGameMode, "from the " + template.Title + " template", out message))
            {
                if (string.IsNullOrEmpty(message))
                    message = "Starter template world could not be loaded.";
                return false;
            }

            return true;
        }

        private static void MarkAllSectionsChanged(ScenarioEditorSession session)
        {
            session.MarkDraftChanged(ScenarioDirtySection.Meta, ScenarioEditCategory.Family);
            session.MarkDraftChanged(ScenarioDirtySection.Family, ScenarioEditCategory.Family);
            session.MarkDraftChanged(ScenarioDirtySection.Inventory, ScenarioEditCategory.Inventory);
            session.MarkDraftChanged(ScenarioDirtySection.Bunker, ScenarioEditCategory.Bunker);
            session.MarkDraftChanged(ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
            session.MarkDraftChanged(ScenarioDirtySection.WinLoss, ScenarioEditCategory.WinLoss);
            session.MarkDraftChanged(ScenarioDirtySection.Assets, ScenarioEditCategory.Assets);
            session.MarkDraftChanged(ScenarioDirtySection.Map, ScenarioEditCategory.Map);
        }
    }
}
