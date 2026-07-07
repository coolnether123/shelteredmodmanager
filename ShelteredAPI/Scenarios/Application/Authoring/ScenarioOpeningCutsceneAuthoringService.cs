using System;
using System.Collections.Generic;
using System.Reflection;

using ModAPI.Core;

using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredAPI.Scenarios.Application.Authoring
{
    internal sealed class ScenarioOpeningCutsceneAuthoringService
    {
        private static readonly BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly BindingFlags InstanceAny = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public bool TryWatchOpeningCutscene(ScenarioEditorSession session, out string message)
        {
            message = null;
            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            if (definition == null)
            {
                message = "No active scenario draft is available.";
                return true;
            }

            CutsceneManager manager = CutsceneManager.Instance;
            if (manager == null)
            {
                message = "Opening cutscene is unavailable because CutsceneManager is not ready.";
                return true;
            }

            Cutscene active = manager.GetActiveCutscene;
            if (manager.CutSceneActive)
            {
                message = active != null && active.IsIntro
                    ? "Opening cutscene is already playing."
                    : "Opening cutscene is unavailable while another cutscene is active.";
                return true;
            }

            Cutscene intro = FindIntroCutscene(manager);
            if (intro == null)
            {
                message = "Opening cutscene is unavailable because this backend scene does not expose an intro cutscene.";
                return true;
            }

            try
            {
                ResetCutsceneForReplay(intro);
                manager.pauseCutsceneManager = false;
                bool started = intro.CheckEntryCondition();
                if (!started)
                {
                    manager.PlayCutscene(intro);
                    started = intro.CheckEntryCondition();
                }

                if (started || manager.CutSceneActive)
                {
                    message = "Playing " + ScenarioAuthoringBaseModeReloadService.FormatBaseMode(definition.BaseGameMode) + " opening cutscene.";
                    return true;
                }

                message = "Opening cutscene could not start from the editor context. Try again after the backend finishes loading and no panels are blocking game input.";
                return true;
            }
            catch (Exception ex)
            {
                message = "Opening cutscene could not start: " + ex.Message;
                MMLog.WriteWarning("[ScenarioOpeningCutsceneAuthoring] Failed to play opening cutscene: " + ex + ".");
                return true;
            }
        }

        private static Cutscene FindIntroCutscene(CutsceneManager manager)
        {
            FieldInfo field = typeof(CutsceneManager).GetField("cutscenes", InstancePrivate);
            List<Cutscene> cutscenes = field != null ? field.GetValue(manager) as List<Cutscene> : null;
            for (int i = 0; cutscenes != null && i < cutscenes.Count; i++)
            {
                Cutscene cutscene = cutscenes[i];
                if (cutscene != null && cutscene.IsIntro)
                    return cutscene;
            }

            return null;
        }

        private static void ResetCutsceneForReplay(Cutscene cutscene)
        {
            SetBoolField(cutscene, "finished", false);
            SetBoolField(cutscene, "isActive", false);
            SetIntField(cutscene, "stageNumber", 0);
            cutscene.cutsceneWaiting = true;
        }

        private static void SetBoolField(object target, string fieldName, bool value)
        {
            if (target == null || string.IsNullOrEmpty(fieldName))
                return;

            FieldInfo field = target.GetType().GetField(fieldName, InstanceAny);
            if (field != null && field.FieldType == typeof(bool))
                field.SetValue(target, value);
        }

        private static void SetIntField(object target, string fieldName, int value)
        {
            if (target == null || string.IsNullOrEmpty(fieldName))
                return;

            FieldInfo field = target.GetType().GetField(fieldName, InstanceAny);
            if (field != null && field.FieldType == typeof(int))
                field.SetValue(target, value);
        }
    }
}
