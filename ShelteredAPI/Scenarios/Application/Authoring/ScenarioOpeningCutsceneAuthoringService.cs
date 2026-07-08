using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

using ModAPI.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

using ShelteredAPI.Scenarios.Infrastructure.Unity;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Infrastructure;

namespace ShelteredAPI.Scenarios.Application.Authoring
{
    internal sealed class ScenarioOpeningCutsceneAuthoringService
    {
        private const float PreviewStartTimeoutSeconds = 4f;
        private static readonly BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly BindingFlags InstanceAny = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly BindingFlags StaticAny = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly FieldInfo PanelInputActiveField = typeof(UIPanelManager).GetField("m_bInputActive", InstancePrivate);
        private static readonly FieldInfo PanelNextFrameInputActiveField = typeof(UIPanelManager).GetField("m_bNextFrameInputActive", InstancePrivate);
        private static readonly FieldInfo PanelIgnoreInputField = typeof(UIPanelManager).GetField("m_bIgnoreInput", InstancePrivate);
        private static readonly FieldInfo PanelTimePausedField = typeof(UIPanelManager).GetField("m_bTimePaused", InstancePrivate);
        private static readonly FieldInfo InteractionSelectedMemberField = typeof(InteractionManager).GetField("selectedMember", InstancePrivate);
        private static readonly FieldInfo InteractionSelectedMemberIndexField = typeof(InteractionManager).GetField("selectedMemberIndex", InstancePrivate);
        private static readonly FieldInfo GameTimeCurrentDayField = typeof(GameTime).GetField("current_day", StaticAny);
        private static readonly FieldInfo GameTimeCurrentHourField = typeof(GameTime).GetField("current_hour", StaticAny);
        private static PreviewContext _activePreview;

        public static bool IsPreviewActive
        {
            get { return _activePreview != null; }
        }

        internal static bool HasVanillaOpeningCutscene(ScenarioBaseGameMode mode)
        {
            return mode == ScenarioBaseGameMode.Stasis || mode == ScenarioBaseGameMode.Surrounded;
        }

        internal static string BuildNoOpeningCutsceneMessage(ScenarioBaseGameMode mode)
        {
            if (mode == ScenarioBaseGameMode.Survival)
                return "Standard mode starts directly after family setup, so there is no vanilla opening cutscene to preview.";

            return ScenarioAuthoringBaseModeReloadService.FormatBaseMode(mode)
                + " does not expose a vanilla opening cutscene in the current shelter scene.";
        }

        public static void UpdateActivePreview()
        {
            PreviewContext preview = _activePreview;
            if (preview == null)
                return;

            CutsceneManager manager = preview.Manager;
            if (manager == null || preview.State == null || !preview.State.IsActive)
            {
                RestorePreview(preview, "Opening cutscene preview stopped because authoring is no longer active.");
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                SkipCutscene(preview.Manager, preview.Cutscene, "opening cutscene preview");
                RestorePreview(preview, "Opening cutscene preview skipped; editor restored.");
                return;
            }

            Cutscene active = manager.GetActiveCutscene;
            if (manager.CutSceneActive && active != null && !active.IsFinished
                && (active.IsIntro || ReferenceEquals(active, preview.Cutscene)))
            {
                preview.MarkStarted();
                return;
            }

            if (manager.CutSceneActive && active != null && active.IsFinished)
                manager.DeactivateCutscene();

            if (!preview.Started && Time.realtimeSinceStartup < preview.StartDeadlineSeconds)
                return;

            RestorePreview(preview, preview.Started
                ? "Opening cutscene preview finished; editor restored."
                : BuildStartFailureMessage());
        }

        internal static void UpdateAuthoringIntroCutsceneFallback()
        {
            if (_activePreview != null || !ScenarioAuthoringRuntimeGuards.IsAuthoringActive())
                return;

            CutsceneManager manager = CutsceneManager.Instance;
            Cutscene active = manager != null ? manager.GetActiveCutscene : null;
            if (manager == null || !manager.CutSceneActive || active == null || !active.IsIntro)
                return;

            if (!UnityEngine.Input.GetKeyDown(KeyCode.Escape))
                return;

            SkipCutscene(manager, active, "authoring intro cutscene");
            RestoreVanillaPanelInputForAuthoring("authoring intro cutscene");
            ScenarioAuthoringPauseService.Instance.EnsurePaused("Opening cutscene skipped from authoring.");
            ScenarioAuthoringBackendService.Instance.SetStatusMessage("Opening cutscene skipped; editor restored.");
        }

        private static void SkipCutscene(CutsceneManager manager, Cutscene cutscene, string reason)
        {
            if (manager == null)
                return;

            try
            {
                manager.pauseCutsceneManager = false;
                if (cutscene != null)
                    cutscene.SkipCutscene();
                manager.DeactivateCutscene();
            }
            catch (Exception ex)
            {
                RecordCutsceneFailure(
                    "scenario.cutscene.skip",
                    ex,
                    "Opening cutscene controls unavailable - scenario editor still usable.",
                    null);
            }
        }

        public bool TryWatchOpeningCutscene(ScenarioEditorSession session, ScenarioAuthoringState state, out string message)
        {
            message = null;
            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            if (definition == null)
            {
                message = "No active scenario draft is available.";
                return true;
            }
            if (!HasVanillaOpeningCutscene(definition.BaseGameMode))
            {
                message = BuildNoOpeningCutsceneMessage(definition.BaseGameMode);
                return true;
            }

            CutsceneManager manager = CutsceneManager.Instance;
            if (manager == null)
            {
                message = BuildNoCutsceneManagerMessage(definition);
                return true;
            }

            Cutscene active = manager.GetActiveCutscene;
            if (manager.CutSceneActive)
            {
                if (active != null && active.IsIntro)
                {
                    BeginPreview(state, manager, active).MarkStarted();
                    message = "Playing " + ScenarioAuthoringBaseModeReloadService.FormatBaseMode(definition.BaseGameMode) + " opening cutscene.";
                }
                else
                {
                    message = "Opening cutscene is unavailable while another cutscene is active.";
                }

                return true;
            }

            Cutscene intro = FindIntroCutscene(manager);
            if (intro == null)
            {
                message = "Opening cutscene is unavailable because this shelter scene does not expose an intro cutscene.";
                return true;
            }

            try
            {
                PreviewContext preview = BeginPreview(state, manager, intro);
                ResetCutsceneForReplay(intro);
                preview.OriginalPersonNames = RebindPreviewPeopleToLiveFamily(intro);
                preview.OriginalStagePersonNames = RebindPreviewStagesToLiveFamily(intro, preview.OriginalPersonNames);
                if (!TryPreparePreviewPrerequisites(preview, out message))
                {
                    RestorePreview(preview, null);
                    return true;
                }

                manager.pauseCutsceneManager = false;
                bool started = intro.CheckEntryCondition();
                if (!started)
                {
                    manager.PlayCutscene(intro);
                    intro.cutsceneWaiting = true;
                    started = intro.CheckEntryCondition();
                }

                if (started || manager.CutSceneActive)
                {
                    preview.MarkStarted();
                    message = "Playing " + ScenarioAuthoringBaseModeReloadService.FormatBaseMode(definition.BaseGameMode) + " opening cutscene.";
                    return true;
                }

                message = "Starting " + ScenarioAuthoringBaseModeReloadService.FormatBaseMode(definition.BaseGameMode) + " opening cutscene.";
                return true;
            }
            catch (Exception ex)
            {
                RecordCutsceneFailure(
                    "scenario.cutscene.preview",
                    ex,
                    "Opening cutscene preview unavailable - scenario editor still usable.",
                    delegate { RestorePreview(_activePreview, null); });
                message = "Opening cutscene preview unavailable - scenario editor still usable.";
                return true;
            }
        }

        private static bool TryPreparePreviewPrerequisites(PreviewContext preview, out string message)
        {
            message = null;
            if (preview == null || preview.Cutscene == null)
            {
                message = "Opening cutscene is unavailable right now.";
                return false;
            }

            preview.CaptureCutsceneWaiting();

            if (FamilySpawner.instance == null)
            {
                message = "Opening cutscene needs the shelter family spawner to be ready. Try again after the scene finishes loading.";
                return false;
            }

            List<FamilyMember> family = FamilyManager.Instance != null ? FamilyManager.Instance.GetAllFamilyMembers() : null;
            if (family == null || family.Count == 0)
            {
                message = "Opening cutscene needs at least one active survivor in the shelter.";
                return false;
            }

            InteractionManager interaction = InteractionManager.Instance;
            if (interaction == null)
            {
                message = "Opening cutscene needs shelter controls to be ready. Try again after the scene finishes loading.";
                return false;
            }

            preview.CaptureInteractionSelection(interaction);
            if (interaction.GetSelectedFamilyMember() == null && !TrySelectFirstFamilyMember(interaction))
            {
                message = "Opening cutscene needs at least one selectable survivor in the shelter.";
                return false;
            }

            preview.CaptureAndSatisfyGameTimeGate();
            return true;
        }

        private static bool TrySelectFirstFamilyMember(InteractionManager interaction)
        {
            if (interaction == null)
                return false;

            for (int i = 0; i < interaction.GetNumFamilyMembers(); i++)
            {
                FamilyMember member = interaction.GetFamilyMemberByIndex(i);
                if (member == null)
                    continue;

                interaction.SelectFamilyMemberByIndex(i);
                if (interaction.GetSelectedFamilyMember() != null)
                    return true;

                SetInteractionSelection(interaction, member, i);
                return interaction.GetSelectedFamilyMember() != null;
            }

            List<FamilyMember> family = FamilyManager.Instance != null ? FamilyManager.Instance.GetAllFamilyMembers() : null;
            for (int i = 0; family != null && i < family.Count; i++)
            {
                FamilyMember member = family[i];
                if (member == null)
                    continue;

                SetInteractionSelection(interaction, member, i);
                return interaction.GetSelectedFamilyMember() != null;
            }

            return false;
        }

        private static Cutscene FindIntroCutscene(CutsceneManager manager)
        {
            FieldInfo field = typeof(CutsceneManager).GetField("cutscenes", InstancePrivate);
            List<Cutscene> cutscenes = null;
            if (field != null)
            {
                string message;
                SeamGuard.Try<List<Cutscene>>(
                    "scenario.cutscene.preview.cutscenes",
                    SeamRecoveryPolicy.RetryOnce,
                    delegate { return field.GetValue(manager) as List<Cutscene>; },
                    null,
                    "Opening cutscene preview unavailable - scenario editor still usable.",
                    null,
                    out cutscenes,
                    out message);
            }
            for (int i = 0; cutscenes != null && i < cutscenes.Count; i++)
            {
                Cutscene cutscene = cutscenes[i];
                if (cutscene != null && cutscene.IsIntro)
                    return cutscene;
            }

            return null;
        }

        private static PreviewContext BeginPreview(ScenarioAuthoringState state, CutsceneManager manager, Cutscene cutscene)
        {
            if (_activePreview != null)
                RestorePreview(_activePreview, null);

            bool previousShellVisible = state != null && state.ShellVisible;
            if (state != null)
            {
                state.ShellVisible = false;
                state.WindowMenuOpen = false;
                state.SettingsWindowOpen = false;
                state.HelpWindowOpen = false;
                state.FocusedEditorKind = null;
                state.StatusMessage = "Playing opening cutscene. Press Escape to skip and return to the editor.";
            }

            _activePreview = new PreviewContext(state, manager, cutscene, previousShellVisible);
            RestoreVanillaPanelInputForAuthoring("opening cutscene preview start");
            ScenarioAuthoringPauseService.Instance.ReleasePause("Opening cutscene preview started.");
            if (Time.timeScale == 0f)
                Time.timeScale = 1f;

            manager.pauseCutsceneManager = false;
            MMLog.WriteInfo("[ScenarioOpeningCutsceneAuthoring] Opening cutscene preview started. scene="
                + SceneManager.GetActiveScene().name + ", cutscene=" + (cutscene != null ? cutscene.name : "<none>") + ".");
            return _activePreview;
        }

        private static void RestorePreview(PreviewContext preview, string statusMessage)
        {
            if (preview == null)
                return;

            if (ReferenceEquals(_activePreview, preview))
                _activePreview = null;

            if (preview.State != null && preview.State.IsActive)
            {
                preview.State.ShellVisible = preview.PreviousShellVisible;
                if (!string.IsNullOrEmpty(statusMessage))
                    preview.State.StatusMessage = statusMessage;
            }

            RestoreVanillaPanelInputForAuthoring("opening cutscene preview");
            RestorePreviewPeople(preview.Cutscene, preview.OriginalPersonNames);
            RestorePreviewStages(preview.Cutscene, preview.OriginalStagePersonNames);
            RestorePreviewPrerequisites(preview);
            ScenarioAuthoringPauseService.Instance.EnsurePaused("Opening cutscene preview finished.");
            MMLog.WriteInfo("[ScenarioOpeningCutsceneAuthoring] Opening cutscene preview restored authoring pause. scene="
                + SceneManager.GetActiveScene().name + ".");
        }

        private static void RestorePreviewPrerequisites(PreviewContext preview)
        {
            if (preview == null)
                return;

            preview.RestoreCutsceneWaiting();
            preview.RestoreInteractionSelection();
            preview.RestoreGameTimeGate();
        }

        internal static void RestoreStaleCutscenePanelIfAuthoringVisible()
        {
            if (!ScenarioAuthoringRuntimeGuards.IsAuthoringActive())
                return;

            CutsceneManager manager = CutsceneManager.Instance;
            if (manager != null && manager.CutSceneActive)
                return;

            UIPanelManager panelManager = UIPanelManager.instance;
            FadeManager fade = FadeManager.Instance;
            if (panelManager == null || fade == null || !panelManager.IsPanelOnStack(fade))
                return;

            BasePanel topPanel = panelManager.GetTopPanel();
            if (!ReferenceEquals(topPanel, fade))
                return;

            RestoreVanillaPanelInputForAuthoring("stale cutscene fade panel");
        }

        private static void RestoreVanillaPanelInputForAuthoring(string reason)
        {
            try
            {
                UIPanelManager panelManager = UIPanelManager.instance;
                if (panelManager == null)
                    return;

                FadeManager fade = FadeManager.Instance;
                if (fade != null && panelManager.IsPanelOnStack(fade))
                {
                    panelManager.PopPanel(fade);
                    if (fade.gameObject != null)
                        fade.gameObject.SetActive(false);
                }

                SetPanelBool(panelManager, PanelInputActiveField, true);
                SetPanelBool(panelManager, PanelNextFrameInputActiveField, true);
                SetPanelBool(panelManager, PanelIgnoreInputField, false);
                SetPanelBool(panelManager, PanelTimePausedField, false);
                AudioListener.pause = false;
                MMLog.WriteInfo("[ScenarioOpeningCutsceneAuthoring] Restored vanilla panel input after " + (reason ?? "cutscene") + ".");
            }
            catch (Exception ex)
            {
                RecordCutsceneFailure(
                    "scenario.cutscene.restore-panel-input",
                    ex,
                    "Opening cutscene preview restore degraded - scenario editor still usable.",
                    null);
            }
        }

        private static void ResetCutsceneForReplay(Cutscene cutscene)
        {
            SetBoolField(cutscene, "finished", false);
            SetBoolField(cutscene, "isActive", false);
            SetIntField(cutscene, "stageNumber", 0);
            FieldInfo field = typeof(Cutscene).GetField("stagesOfConversation", InstancePrivate);
            List<Cutscene_Stage> stages = field != null ? field.GetValue(cutscene) as List<Cutscene_Stage> : null;
            for (int i = 0; stages != null && i < stages.Count; i++)
            {
                if (stages[i] != null)
                    stages[i].started = false;
            }

            cutscene.cutsceneWaiting = true;
        }

        private static List<string> RebindPreviewPeopleToLiveFamily(Cutscene cutscene)
        {
            List<string> originalNames = new List<string>();
            FieldInfo field = typeof(Cutscene).GetField("peopleInvolved", InstancePrivate);
            List<Cutscene_Person> people = field != null ? field.GetValue(cutscene) as List<Cutscene_Person> : null;
            List<FamilyMember> family = FamilyManager.Instance != null ? FamilyManager.Instance.GetAllFamilyMembers() : null;
            int familyIndex = 0;
            for (int i = 0; people != null && i < people.Count; i++)
            {
                Cutscene_Person person = people[i];
                originalNames.Add(person != null ? person.name : null);
                if (person == null || person.isNPC || family == null)
                    continue;

                while (familyIndex < family.Count && family[familyIndex] == null)
                    familyIndex++;
                if (familyIndex >= family.Count)
                    continue;

                string firstName = family[familyIndex].firstName;
                if (!string.IsNullOrEmpty(firstName))
                    person.name = firstName;
                familyIndex++;
            }

            return originalNames;
        }

        private static List<string> RebindPreviewStagesToLiveFamily(Cutscene cutscene, List<string> originalPersonNames)
        {
            List<string> originalStageNames = new List<string>();
            if (cutscene == null)
                return originalStageNames;

            FieldInfo peopleField = typeof(Cutscene).GetField("peopleInvolved", InstancePrivate);
            List<Cutscene_Person> people = peopleField != null ? peopleField.GetValue(cutscene) as List<Cutscene_Person> : null;
            FieldInfo stagesField = typeof(Cutscene).GetField("stagesOfConversation", InstancePrivate);
            List<Cutscene_Stage> stages = stagesField != null ? stagesField.GetValue(cutscene) as List<Cutscene_Stage> : null;
            if (people == null || stages == null || originalPersonNames == null)
                return originalStageNames;

            Dictionary<string, string> rebinding = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < people.Count && i < originalPersonNames.Count; i++)
            {
                Cutscene_Person person = people[i];
                string originalName = originalPersonNames[i];
                string previewName = person != null ? person.name : null;
                if (string.IsNullOrEmpty(originalName) || string.IsNullOrEmpty(previewName) || string.Equals(originalName, previewName, StringComparison.Ordinal))
                    continue;

                if (!rebinding.ContainsKey(originalName))
                    rebinding.Add(originalName, previewName);
            }

            for (int i = 0; i < stages.Count; i++)
            {
                Cutscene_Stage stage = stages[i];
                string originalName = stage != null ? stage.nameOfPerson : null;
                originalStageNames.Add(originalName);
                if (stage == null || string.IsNullOrEmpty(originalName))
                    continue;

                string previewName;
                if (rebinding.TryGetValue(originalName, out previewName))
                    stage.nameOfPerson = previewName;
            }

            return originalStageNames;
        }

        private static void RestorePreviewPeople(Cutscene cutscene, List<string> originalNames)
        {
            if (cutscene == null || originalNames == null)
                return;

            FieldInfo field = typeof(Cutscene).GetField("peopleInvolved", InstancePrivate);
            List<Cutscene_Person> people = field != null ? field.GetValue(cutscene) as List<Cutscene_Person> : null;
            for (int i = 0; people != null && i < people.Count && i < originalNames.Count; i++)
            {
                if (people[i] != null)
                    people[i].name = originalNames[i];
            }
        }

        private static void RestorePreviewStages(Cutscene cutscene, List<string> originalNames)
        {
            if (cutscene == null || originalNames == null)
                return;

            FieldInfo field = typeof(Cutscene).GetField("stagesOfConversation", InstancePrivate);
            List<Cutscene_Stage> stages = field != null ? field.GetValue(cutscene) as List<Cutscene_Stage> : null;
            for (int i = 0; stages != null && i < stages.Count && i < originalNames.Count; i++)
            {
                if (stages[i] != null)
                    stages[i].nameOfPerson = originalNames[i];
            }
        }

        private static string BuildNoCutsceneManagerMessage(ScenarioDefinition definition)
        {
            if (definition != null && definition.BaseGameMode == ScenarioBaseGameMode.Survival)
                return BuildNoOpeningCutsceneMessage(definition.BaseGameMode);

            return "Opening cutscene is unavailable because this shelter scene has not finished preparing cutscenes yet.";
        }

        private static string BuildStartFailureMessage()
        {
            bool inputActive = UIPanelManager.instance != null && UIPanelManager.instance.IsGameInputActive();
            bool saveBusy = SaveManager.instance != null && (SaveManager.instance.isSaving || SaveManager.instance.isLoading);
            bool hasSelectedMember = InteractionManager.Instance != null && InteractionManager.Instance.GetSelectedFamilyMember() != null;
            int familyCount = FamilyManager.Instance != null && FamilyManager.Instance.GetAllFamilyMembers() != null
                ? FamilyManager.Instance.GetAllFamilyMembers().Count
                : 0;
            Cutscene intro = CutsceneManager.Instance != null ? FindIntroCutscene(CutsceneManager.Instance) : null;
            MMLog.WriteWarning("[ScenarioOpeningCutsceneAuthoring] Opening cutscene preview timed out. inputActive="
                + inputActive + ", saveBusy=" + saveBusy + ", selectedMember=" + hasSelectedMember
                + ", familyCount=" + familyCount + ", familySpawner=" + (FamilySpawner.instance != null)
                + ", timeScale=" + Time.timeScale + ", gameDay=" + GameTime.Day + ", gameHour=" + GameTime.Hour
                + ", introDay=" + (intro != null ? intro.dayToActivate.ToString(CultureInfo.InvariantCulture) : "<none>")
                + ", introHour=" + (intro != null ? intro.hourToActivate.ToString(CultureInfo.InvariantCulture) : "<none>") + ".");
            return "Opening cutscene could not start. Make sure the shelter has an active survivor, then try again.";
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

        private static void SetPanelBool(UIPanelManager panelManager, FieldInfo field, bool value)
        {
            if (panelManager != null && field != null && field.FieldType == typeof(bool))
                field.SetValue(panelManager, value);
        }

        private static void SetInteractionSelection(InteractionManager interaction, FamilyMember member, int index)
        {
            if (interaction == null)
                return;

            if (InteractionSelectedMemberField != null && InteractionSelectedMemberField.FieldType == typeof(FamilyMember))
                InteractionSelectedMemberField.SetValue(interaction, member);
            if (InteractionSelectedMemberIndexField != null && InteractionSelectedMemberIndexField.FieldType == typeof(int))
                InteractionSelectedMemberIndexField.SetValue(interaction, index);
        }

        private sealed class PreviewContext
        {
            public readonly ScenarioAuthoringState State;
            public readonly CutsceneManager Manager;
            public readonly Cutscene Cutscene;
            public readonly bool PreviousShellVisible;
            public readonly float StartDeadlineSeconds;
            public List<string> OriginalPersonNames;
            public List<string> OriginalStagePersonNames;
            public bool Started;
            private bool _cutsceneWaitingCaptured;
            private bool _originalCutsceneWaiting;
            private bool _interactionSelectionCaptured;
            private InteractionManager _interaction;
            private FamilyMember _originalSelectedMember;
            private int _originalSelectedMemberIndex;
            private bool _gameTimeGateCaptured;
            private int _originalGameDay;
            private int _originalGameHour;

            public PreviewContext(
                ScenarioAuthoringState state,
                CutsceneManager manager,
                Cutscene cutscene,
                bool previousShellVisible)
            {
                State = state;
                Manager = manager;
                Cutscene = cutscene;
                PreviousShellVisible = previousShellVisible;
                StartDeadlineSeconds = Time.realtimeSinceStartup + PreviewStartTimeoutSeconds;
            }

            public void MarkStarted()
            {
                Started = true;
            }

            public void CaptureCutsceneWaiting()
            {
                if (Cutscene == null || _cutsceneWaitingCaptured)
                    return;

                _originalCutsceneWaiting = Cutscene.cutsceneWaiting;
                _cutsceneWaitingCaptured = true;
                Cutscene.cutsceneWaiting = true;
            }

            public void RestoreCutsceneWaiting()
            {
                if (Cutscene == null || !_cutsceneWaitingCaptured)
                    return;

                Cutscene.cutsceneWaiting = _originalCutsceneWaiting;
            }

            public void CaptureInteractionSelection(InteractionManager interaction)
            {
                if (interaction == null || _interactionSelectionCaptured)
                    return;

                _interaction = interaction;
                _originalSelectedMember = interaction.GetSelectedFamilyMember();
                _originalSelectedMemberIndex = InteractionSelectedMemberIndexField != null && InteractionSelectedMemberIndexField.FieldType == typeof(int)
                    ? (int)InteractionSelectedMemberIndexField.GetValue(interaction)
                    : interaction.GetSelectedFamilyMemberIndex();
                _interactionSelectionCaptured = true;
            }

            public void CaptureAndSatisfyGameTimeGate()
            {
                if (Cutscene == null || _gameTimeGateCaptured)
                    return;

                if (Cutscene.dayToActivate <= 0)
                    return;

                if (GameTimeCurrentDayField == null || GameTimeCurrentHourField == null)
                    return;

                _originalGameDay = GameTime.Day;
                _originalGameHour = GameTime.Hour;
                _gameTimeGateCaptured = true;

                int targetDay = Cutscene.dayToActivate;
                int targetHour = Cutscene.hourToActivate > 0 ? Cutscene.hourToActivate : _originalGameHour;
                bool dayBlocked = _originalGameDay < targetDay;
                bool hourBlocked = _originalGameDay == targetDay && Cutscene.hourToActivate > 0 && _originalGameHour < Cutscene.hourToActivate;
                if (!dayBlocked && !hourBlocked)
                    return;

                if (dayBlocked)
                    SetStaticIntField(GameTimeCurrentDayField, targetDay);
                if (dayBlocked || hourBlocked)
                    SetStaticIntField(GameTimeCurrentHourField, targetHour);

                MMLog.WriteInfo("[ScenarioOpeningCutsceneAuthoring] Satisfied opening cutscene GameTime gate for preview. day "
                    + _originalGameDay + "->" + GameTime.Day + ", hour " + _originalGameHour + "->" + GameTime.Hour + ".");
            }

            public void RestoreInteractionSelection()
            {
                if (!_interactionSelectionCaptured)
                    return;

                SetInteractionSelection(_interaction, _originalSelectedMember, _originalSelectedMemberIndex);
            }

            public void RestoreGameTimeGate()
            {
                if (!_gameTimeGateCaptured)
                    return;

                if (GameTimeCurrentDayField != null)
                    SetStaticIntField(GameTimeCurrentDayField, _originalGameDay);
                if (GameTimeCurrentHourField != null)
                    SetStaticIntField(GameTimeCurrentHourField, _originalGameHour);
            }
        }

        private static void SetStaticIntField(FieldInfo field, int value)
        {
            if (field == null)
                return;

            string message;
            SeamGuard.Run(
                "scenario.cutscene.preview.field." + field.Name,
                SeamRecoveryPolicy.RestoreState,
                delegate { field.SetValue(null, value); },
                "Opening cutscene preview unavailable - scenario editor still usable.",
                null,
                out message);
        }

        private static void RecordCutsceneFailure(string seamName, Exception ex, string playerMessage, Action recovery)
        {
            string message;
            SeamGuard.Run(
                seamName,
                SeamRecoveryPolicy.RestoreState,
                delegate { throw ex; },
                playerMessage,
                recovery,
                out message);
        }
    }
}
