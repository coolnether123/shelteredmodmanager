using System;
using System.Collections.Generic;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;

namespace Cortex.Services.Harmony.Workflow
{
    internal sealed class GeneratedTemplateNavigationService
    {
        private readonly IEditorService _editorService = new EditorService();

        public void StartSession(CortexShellState state, DocumentSession session, GeneratedTemplatePlaceholder[] placeholders, int startOffset, int endOffset)
        {
            if (state == null || state.Harmony == null || session == null)
            {
                return;
            }

            var ordered = CopyPlaceholders(placeholders);
            state.Harmony.ActiveTemplateSession = new GeneratedTemplateSession
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

            if (!HasValidPlaceholderBounds(state.Harmony.ActiveTemplateSession, session))
            {
                ClearSession(state);
                return;
            }

            FocusActivePlaceholder(state, session);
        }

        public bool TryHandleNavigation(CortexShellState state, DocumentSession session, bool reverse)
        {
            if (state == null || state.Harmony == null || session == null)
            {
                return false;
            }

            SyncSession(state, session);
            var templateSession = state.Harmony.ActiveTemplateSession;
            if (templateSession == null ||
                templateSession.Completed ||
                !string.Equals(templateSession.DocumentPath, session.FilePath ?? string.Empty, StringComparison.OrdinalIgnoreCase) ||
                templateSession.Placeholders == null ||
                templateSession.Placeholders.Length == 0 ||
                !HasValidPlaceholderBounds(templateSession, session))
            {
                ClearSession(state);
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
            FocusActivePlaceholder(state, session);
            return true;
        }

        public void SyncSession(CortexShellState state, DocumentSession session)
        {
            if (state == null || state.Harmony == null || session == null)
            {
                return;
            }

            var templateSession = state.Harmony.ActiveTemplateSession;
            if (templateSession == null ||
                templateSession.Completed ||
                !string.Equals(templateSession.DocumentPath, session.FilePath ?? string.Empty, StringComparison.OrdinalIgnoreCase) ||
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
                ClearSession(state);
            }
        }

        public void ClearSession(CortexShellState state)
        {
            if (state == null || state.Harmony == null)
            {
                return;
            }

            state.Harmony.ActiveTemplateSession = null;
        }

        private void FocusActivePlaceholder(CortexShellState state, DocumentSession session)
        {
            var templateSession = state != null && state.Harmony != null ? state.Harmony.ActiveTemplateSession : null;
            if (templateSession == null ||
                templateSession.ActivePlaceholderIndex < 0 ||
                templateSession.Placeholders == null ||
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
