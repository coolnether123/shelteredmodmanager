using System;
using System.Collections.Generic;
using ShelteredScenarioEditor.Infrastructure.Persistence;

namespace ShelteredScenarioEditor.Application.Authoring
{
    /// <summary>
    /// Canonical per-draft editor metadata. This state belongs to the editor
    /// session and is persisted only through the transactional editor sidecar.
    /// </summary>
    internal sealed class ScenarioEditorState
    {
        private readonly List<string> _completedTours = new List<string>();

        public ScenarioEditorState()
        {
            AuthorTestChecklist = new ScenarioAuthorTestChecklist();
            ChecklistDismissed = true;
        }

        public ScenarioAuthorTestChecklist AuthorTestChecklist { get; set; }
        public bool SetupFlowEnabled { get; set; }
        public bool ChecklistDismissed { get; set; }
        public string UpdatedAtUtc { get; set; }

        public List<string> CompletedTours
        {
            get { return _completedTours; }
        }

        public bool HasPersistedContent
        {
            get
            {
                return SetupFlowEnabled
                    || !ChecklistDismissed
                    || _completedTours.Count > 0
                    || (AuthorTestChecklist != null && AuthorTestChecklist.HasAuthoredContent);
            }
        }

        internal static ScenarioEditorState CreateForNewDraft()
        {
            return new ScenarioEditorState
            {
                SetupFlowEnabled = true,
                ChecklistDismissed = false
            };
        }

        public bool HasCompletedTour(string tourId)
        {
            if (string.IsNullOrEmpty(tourId))
                return false;

            for (int i = 0; i < _completedTours.Count; i++)
            {
                if (string.Equals(_completedTours[i], tourId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public bool AddCompletedTour(string tourId)
        {
            if (string.IsNullOrEmpty(tourId) || HasCompletedTour(tourId))
                return false;

            _completedTours.Add(tourId);
            return true;
        }

        public ScenarioEditorState Copy()
        {
            ScenarioEditorState copy = new ScenarioEditorState();
            copy.AuthorTestChecklist = AuthorTestChecklist != null
                ? AuthorTestChecklist.Copy()
                : new ScenarioAuthorTestChecklist();
            copy.SetupFlowEnabled = SetupFlowEnabled;
            copy.ChecklistDismissed = ChecklistDismissed;
            copy.UpdatedAtUtc = UpdatedAtUtc;
            for (int i = 0; i < _completedTours.Count; i++)
                copy.CompletedTours.Add(_completedTours[i]);
            return copy;
        }
    }

    /// <summary>
    /// Owns access to the active session's editor metadata and its transactional
    /// sidecar. UI services never load or write editor metadata independently.
    /// </summary>
    internal sealed class ScenarioEditorStateSessionService
    {
        private readonly IScenarioEditorSessionStore _sessionStore;
        private readonly ScenarioAuthoringSidecarStore _sidecarStore;

        internal ScenarioEditorStateSessionService(
            IScenarioEditorSessionStore sessionStore,
            ScenarioAuthoringSidecarStore sidecarStore)
        {
            _sessionStore = sessionStore;
            _sidecarStore = sidecarStore;
        }

        internal ScenarioEditorState Current
        {
            get
            {
                ScenarioEditorSession session = _sessionStore.Current;
                if (session == null)
                    return null;
                if (session.EditorState == null)
                    session.EditorState = new ScenarioEditorState();
                return session.EditorState;
            }
        }

        internal bool SaveCurrent()
        {
            ScenarioEditorSession session = _sessionStore.Current;
            string path = _sessionStore.CurrentFilePath;
            if (session == null || session.EditorState == null || string.IsNullOrEmpty(path))
                return false;

            _sidecarStore.Save(path, session.EditorState);
            session.MarkChecklistChanged();
            return true;
        }
    }
}
