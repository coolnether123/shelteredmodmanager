using System;
using System.Collections.Generic;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.Plugins.Abstractions;

namespace Cortex.Plugin.Harmony
{
    internal sealed class HarmonyTemplateNavigationService
    {
        private readonly HarmonyModuleStateStore _stateStore;
        private readonly IEditorService _editorService;

        public HarmonyTemplateNavigationService(HarmonyModuleStateStore stateStore)
            : this(stateStore, new EditorService())
        {
        }

        internal HarmonyTemplateNavigationService(HarmonyModuleStateStore stateStore, IEditorService editorService)
        {
            _stateStore = stateStore ?? new HarmonyModuleStateStore();
            _editorService = editorService ?? new EditorService();
        }

        public void StartSession(
            IWorkbenchModuleRuntime runtime,
            DocumentSession session,
            GeneratedTemplatePlaceholder[] placeholders,
            int startOffset,
            int endOffset)
        {
            if (runtime == null || session == null || string.IsNullOrEmpty(session.FilePath))
            {
                return;
            }

            var documentState = _stateStore.GetDocument(runtime, session.FilePath, true);
            if (documentState == null)
            {
                return;
            }

            var ordered = CopyPlaceholders(placeholders);
            documentState.ActiveTemplateSession = new GeneratedTemplateSession
            {
                DocumentPath = session.FilePath ?? string.Empty,
                StartOffset = startOffset,
                EndOffset = endOffset,
                ActivePlaceholderIndex = ordered.Length > 0 ? 0 : -1,
                DocumentVersion = session.TextVersion,
                LastKnownTextLength = session.Text != null ? session.Text.Length : 0,
                Completed = ordered.Length == 0,
                Placeholders = ordered
            };

            if (!HasValidPlaceholderBounds(documentState.ActiveTemplateSession, session))
            {
                documentState.ActiveTemplateSession = null;
                return;
            }

            FocusActivePlaceholder(session, documentState.ActiveTemplateSession);
        }

        public bool TryHandleNavigation(IWorkbenchModuleRuntime runtime, DocumentSession session, bool reverse)
        {
            if (runtime == null || session == null)
            {
                return false;
            }

            SyncSession(runtime, session);
            var templateSession = GetTemplateSession(runtime, session);
            if (templateSession == null ||
                templateSession.Completed ||
                templateSession.Placeholders == null ||
                templateSession.Placeholders.Length == 0 ||
                !HasValidPlaceholderBounds(templateSession, session))
            {
                ClearSession(runtime, session);
                return false;
            }

            var nextIndex = templateSession.ActivePlaceholderIndex + (reverse ? -1 : 1);
            if (nextIndex < 0)
            {
                nextIndex = 0;
            }

            if (nextIndex >= templateSession.Placeholders.Length)
            {
                templateSession.Completed = true;
                templateSession.ActivePlaceholderIndex = templateSession.Placeholders.Length - 1;
                _editorService.SetCaret(session, templateSession.EndOffset, false, false);
                return true;
            }

            templateSession.ActivePlaceholderIndex = nextIndex;
            FocusActivePlaceholder(session, templateSession);
            return true;
        }

        public void SyncSession(IWorkbenchModuleRuntime runtime, DocumentSession session)
        {
            if (runtime == null || session == null)
            {
                return;
            }

            var templateSession = GetTemplateSession(runtime, session);
            if (templateSession == null ||
                templateSession.Completed ||
                templateSession.DocumentVersion == session.TextVersion)
            {
                return;
            }

            var invalidation = session.EditorState != null ? session.EditorState.PendingInvalidation : null;
            if (invalidation != null)
            {
                ApplyInvalidation(templateSession, invalidation);
            }

            templateSession.DocumentVersion = session.TextVersion;
            templateSession.LastKnownTextLength = session.Text != null ? session.Text.Length : 0;
            if (!HasValidPlaceholderBounds(templateSession, session))
            {
                ClearSession(runtime, session);
            }
        }

        public void ClearSession(IWorkbenchModuleRuntime runtime, DocumentSession session)
        {
            var documentState = _stateStore.GetDocument(runtime, session != null ? session.FilePath : string.Empty, false);
            if (documentState != null)
            {
                documentState.ActiveTemplateSession = null;
            }
        }

        public bool HasActiveSession(IWorkbenchModuleRuntime runtime, DocumentSession session)
        {
            var templateSession = GetTemplateSession(runtime, session);
            return templateSession != null && !templateSession.Completed;
        }

        private GeneratedTemplateSession GetTemplateSession(IWorkbenchModuleRuntime runtime, DocumentSession session)
        {
            var documentState = _stateStore.GetDocument(runtime, session != null ? session.FilePath : string.Empty, false);
            return documentState != null ? documentState.ActiveTemplateSession : null;
        }

        private void FocusActivePlaceholder(DocumentSession session, GeneratedTemplateSession templateSession)
        {
            if (session == null ||
                templateSession == null ||
                templateSession.Placeholders == null ||
                templateSession.ActivePlaceholderIndex < 0 ||
                templateSession.ActivePlaceholderIndex >= templateSession.Placeholders.Length)
            {
                return;
            }

            var placeholder = templateSession.Placeholders[templateSession.ActivePlaceholderIndex];
            if (placeholder == null)
            {
                return;
            }

            var start = Math.Max(0, placeholder.Start);
            var end = Math.Max(start, placeholder.Start + placeholder.Length);
            if (end > start)
            {
                _editorService.SetSelection(session, start, end);
            }
            else
            {
                _editorService.SetCaret(session, start, false, false);
            }
        }

        private static GeneratedTemplatePlaceholder[] CopyPlaceholders(GeneratedTemplatePlaceholder[] placeholders)
        {
            if (placeholders == null || placeholders.Length == 0)
            {
                return new GeneratedTemplatePlaceholder[0];
            }

            var results = new List<GeneratedTemplatePlaceholder>();
            for (var i = 0; i < placeholders.Length; i++)
            {
                var current = placeholders[i];
                if (current == null)
                {
                    continue;
                }

                results.Add(new GeneratedTemplatePlaceholder
                {
                    PlaceholderId = current.PlaceholderId,
                    Start = current.Start,
                    Length = current.Length,
                    DefaultText = current.DefaultText,
                    Description = current.Description
                });
            }

            return results.ToArray();
        }

        private static void ApplyInvalidation(GeneratedTemplateSession session, EditorInvalidation invalidation)
        {
            if (session == null || invalidation == null || session.Placeholders == null)
            {
                return;
            }

            var start = invalidation.Start;
            var oldEnd = invalidation.Start + invalidation.OldLength;
            var delta = invalidation.NewLength - invalidation.OldLength;
            for (var i = 0; i < session.Placeholders.Length; i++)
            {
                var placeholder = session.Placeholders[i];
                if (placeholder == null)
                {
                    continue;
                }

                var placeholderEnd = placeholder.Start + placeholder.Length;
                if (placeholder.Start >= oldEnd)
                {
                    placeholder.Start += delta;
                }
                else if (placeholderEnd > start)
                {
                    placeholder.Length = Math.Max(0, placeholder.Length + delta);
                }
            }

            session.EndOffset = Math.Max(session.StartOffset, session.EndOffset + delta);
        }

        private static bool HasValidPlaceholderBounds(GeneratedTemplateSession session, DocumentSession documentSession)
        {
            if (session == null || documentSession == null)
            {
                return false;
            }

            var textLength = documentSession.Text != null ? documentSession.Text.Length : 0;
            if (session.StartOffset < 0 || session.EndOffset < session.StartOffset || session.EndOffset > textLength)
            {
                return false;
            }

            if (session.Placeholders == null)
            {
                return true;
            }

            for (var i = 0; i < session.Placeholders.Length; i++)
            {
                var placeholder = session.Placeholders[i];
                if (placeholder == null)
                {
                    continue;
                }

                if (placeholder.Start < session.StartOffset ||
                    placeholder.Start > session.EndOffset ||
                    placeholder.Length < 0 ||
                    placeholder.Start + placeholder.Length > textLength)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
