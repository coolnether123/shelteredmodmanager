using System;
using System.Text;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using ModAPI.Core;
using UnityEngine;

namespace Cortex.Services
{
    internal sealed class EditorCompletionService
    {
        private readonly DocumentLanguageInteractionService _documentLanguageInteractionService = new DocumentLanguageInteractionService();
        private readonly CompletionRankingService _completionRankingService = new CompletionRankingService();

        public bool ShouldDispatch(CortexEditorInteractionState editorState, bool requestInFlight)
        {
            if (requestInFlight || editorState == null)
            {
                return false;
            }

            var requestKey = editorState.RequestedCompletionKey ?? string.Empty;
            return !string.IsNullOrEmpty(requestKey) &&
                !string.Equals(requestKey, editorState.ActiveCompletionKey ?? string.Empty, StringComparison.Ordinal);
        }

        public LanguageServiceCompletionRequest BuildWorkerRequest(
            DocumentSession session,
            CortexSettings settings,
            CortexProjectDefinition project,
            string[] sourceRoots,
            CortexEditorInteractionState editorState)
        {
            if (session == null || editorState == null)
            {
                return null;
            }

            if (project == null || string.IsNullOrEmpty(project.ProjectFilePath))
            {
                MMLog.LogOnce("Cortex.Completion.Context.NoProject", delegate
                {
                    MMLog.WriteInfo("[Cortex.Completion] Completion is running without a resolved project file. Roslyn can still suggest symbols, but results may be broader until Cortex resolves project context.");
                });
            }

            return new LanguageServiceCompletionRequest
            {
                DocumentPath = session.FilePath ?? string.Empty,
                ProjectFilePath = project != null ? project.ProjectFilePath : string.Empty,
                WorkspaceRootPath = settings != null ? settings.WorkspaceRootPath : string.Empty,
                SourceRoots = sourceRoots ?? new string[0],
                DocumentText = session.Text ?? string.Empty,
                DocumentVersion = session.TextVersion,
                Line = editorState.RequestedCompletionLine,
                Column = editorState.RequestedCompletionColumn,
                AbsolutePosition = editorState.RequestedCompletionAbsolutePosition,
                ExplicitInvocation = editorState.RequestedCompletionExplicit,
                TriggerCharacter = editorState.RequestedCompletionTriggerCharacter ?? string.Empty
            };
        }

        public bool QueueRequest(
            DocumentSession session,
            CortexEditorInteractionState editorState,
            IEditorService editorService,
            bool explicitInvocation,
            string triggerCharacter)
        {
            if (editorState == null || session == null || session.EditorState == null || editorService == null || session.EditorState.HasMultipleSelections)
            {
                Reset(editorState);
                return false;
            }

            var caretIndex = Mathf.Max(0, session.EditorState.CaretIndex);
            var caret = editorService.GetCaretPosition(session, caretIndex);
            editorState.RequestedCompletionKey = _documentLanguageInteractionService.BuildCompletionRequestKey(
                session.FilePath,
                session.TextVersion,
                caretIndex,
                explicitInvocation,
                triggerCharacter);
            editorState.RequestedCompletionDocumentPath = session.FilePath ?? string.Empty;
            editorState.RequestedCompletionLine = caret.Line + 1;
            editorState.RequestedCompletionColumn = caret.Column + 1;
            editorState.RequestedCompletionAbsolutePosition = caretIndex;
            editorState.RequestedCompletionTriggerCharacter = triggerCharacter ?? string.Empty;
            editorState.RequestedCompletionExplicit = explicitInvocation;
            ClearActive(editorState);
            ResetSelection(editorState);
            LogQueueBehavior(session, editorState, triggerCharacter, explicitInvocation);
            return true;
        }

        public bool AcceptResponse(
            CortexEditorInteractionState editorState,
            DocumentSession target,
            DocumentLanguageCompletionRequestState pending,
            LanguageServiceCompletionResponse response)
        {
            if (editorState == null || pending == null)
            {
                return false;
            }

            if (!string.Equals(editorState.RequestedCompletionKey ?? string.Empty, pending.RequestKey ?? string.Empty, StringComparison.Ordinal))
            {
                return false;
            }

            ClearRequest(editorState);
            if (response == null ||
                target == null ||
                (response.DocumentVersion > 0 && target.TextVersion > 0 && response.DocumentVersion != target.TextVersion) ||
                !HasCompletionItems(response))
            {
                LogRejectedResponse(target, response);
                ClearActive(editorState);
                ResetSelection(editorState);
                return false;
            }

            response = _completionRankingService.Rank(target, editorState, response);
            var query = _completionRankingService.GetQuery(target, response);
            editorState.ActiveCompletionKey = pending.RequestKey ?? string.Empty;
            editorState.ActiveCompletionResponse = response;
            ResetSelection(editorState);
            SyncSelection(editorState);
            MMLog.LogOnce("Cortex.Completion.Response.Accepted", delegate
            {
                MMLog.WriteInfo("[Cortex.Completion] Roslyn completion responses are reaching the editor popup and Cortex is re-ranking them by typed prefix and context before display.");
            });
            MMLog.WriteInfo("[Cortex.Completion] Accepted response for " +
                BuildDocumentLabel(target) +
                " version " + response.DocumentVersion +
                ". Items=" + (response.Items != null ? response.Items.Length : 0) +
                ", ReplacementStart=" + (response.ReplacementRange != null ? response.ReplacementRange.Start.ToString() : "0") +
                ", ReplacementLength=" + (response.ReplacementRange != null ? response.ReplacementRange.Length.ToString() : "0") +
                ", Query='" + (query ?? string.Empty) + "'" +
                ", MatchSummary=" + BuildMatchSummary(response, query) +
                ", Preview=" + BuildCompletionPreview(response) + ".");
            return true;
        }

        public bool IsVisibleForSession(CortexEditorInteractionState editorState, DocumentSession session)
        {
            var response = editorState != null ? editorState.ActiveCompletionResponse : null;
            return HasCompletionItems(response) &&
                session != null &&
                string.Equals(response.DocumentPath ?? string.Empty, session.FilePath ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
                response.DocumentVersion == session.TextVersion &&
                session.EditorState != null &&
                !session.EditorState.HasMultipleSelections;
        }

        public bool HasVisibleCompletion(CortexEditorInteractionState editorState)
        {
            return editorState != null && HasCompletionItems(editorState.ActiveCompletionResponse);
        }

        public bool HasCompletionItems(LanguageServiceCompletionResponse response)
        {
            return _documentLanguageInteractionService.HasCompletionItems(response);
        }

        public bool ShouldTriggerCompletion(char character)
        {
            return _documentLanguageInteractionService.ShouldTriggerCompletion(character);
        }

        public bool ShouldContinueCompletion(DocumentSession session, int caretIndex)
        {
            return _documentLanguageInteractionService.ShouldContinueCompletion(session, caretIndex);
        }

        public void SyncSelection(CortexEditorInteractionState editorState)
        {
            if (editorState == null)
            {
                return;
            }

            var response = editorState.ActiveCompletionResponse;
            if (response == null)
            {
                ResetSelection(editorState);
                return;
            }

            var responseKey = (response.DocumentPath ?? string.Empty) + "|" +
                response.DocumentVersion + "|" +
                (response.Items != null ? response.Items.Length : 0);
            if (!string.Equals(editorState.CompletionPopupStateKey, responseKey, StringComparison.Ordinal))
            {
                editorState.CompletionPopupStateKey = responseKey;
                editorState.CompletionSelectedIndex = 0;
                if (response.Items != null)
                {
                    for (var i = 0; i < response.Items.Length; i++)
                    {
                        if (response.Items[i] != null && response.Items[i].IsPreselected)
                        {
                            editorState.CompletionSelectedIndex = i;
                            break;
                        }
                    }
                }
            }

            editorState.CompletionSelectedIndex = _documentLanguageInteractionService.NormalizeSelectedIndex(response, editorState.CompletionSelectedIndex);
        }

        public void MoveSelection(CortexEditorInteractionState editorState, int delta)
        {
            if (editorState == null || delta == 0)
            {
                return;
            }

            SyncSelection(editorState);
            var response = editorState.ActiveCompletionResponse;
            if (!HasCompletionItems(response))
            {
                return;
            }

            var next = editorState.CompletionSelectedIndex + delta;
            editorState.CompletionSelectedIndex = Mathf.Max(0, Mathf.Min(response.Items.Length - 1, next));
        }

        public bool ApplySelectedCompletion(DocumentSession session, CortexEditorInteractionState editorState, IEditorService editorService)
        {
            if (editorState == null || editorService == null)
            {
                return false;
            }

            var response = editorState.ActiveCompletionResponse;
            if (!HasCompletionItems(response))
            {
                return false;
            }

            SyncSelection(editorState);
            if (editorState.CompletionSelectedIndex < 0 || editorState.CompletionSelectedIndex >= response.Items.Length)
            {
                return false;
            }

            var applied = _documentLanguageInteractionService.ApplyCompletion(
                session,
                editorService,
                response,
                response.Items[editorState.CompletionSelectedIndex]);
            if (applied)
            {
                RecordAcceptedCompletion(editorState, session, response.Items[editorState.CompletionSelectedIndex]);
            }
            Reset(editorState);
            return applied;
        }

        public void Reset(CortexEditorInteractionState editorState)
        {
            if (editorState == null)
            {
                return;
            }

            ClearRequest(editorState);
            ClearActive(editorState);
            ResetSelection(editorState);
        }

        private static void ClearRequest(CortexEditorInteractionState editorState)
        {
            if (editorState == null)
            {
                return;
            }

            editorState.RequestedCompletionKey = string.Empty;
            editorState.RequestedCompletionDocumentPath = string.Empty;
            editorState.RequestedCompletionLine = 0;
            editorState.RequestedCompletionColumn = 0;
            editorState.RequestedCompletionAbsolutePosition = -1;
            editorState.RequestedCompletionTriggerCharacter = string.Empty;
            editorState.RequestedCompletionExplicit = false;
        }

        private static void ClearActive(CortexEditorInteractionState editorState)
        {
            if (editorState == null)
            {
                return;
            }

            editorState.ActiveCompletionKey = string.Empty;
            editorState.ActiveCompletionResponse = null;
        }

        private static void ResetSelection(CortexEditorInteractionState editorState)
        {
            if (editorState == null)
            {
                return;
            }

            editorState.CompletionPopupStateKey = string.Empty;
            editorState.CompletionSelectedIndex = -1;
        }

        private static void RecordAcceptedCompletion(
            CortexEditorInteractionState editorState,
            DocumentSession session,
            LanguageServiceCompletionItem item)
        {
            if (editorState == null || item == null)
            {
                return;
            }

            var completionText = !string.IsNullOrEmpty(item.InsertText)
                ? item.InsertText
                : (!string.IsNullOrEmpty(item.DisplayText) ? item.DisplayText : item.FilterText ?? string.Empty);
            if (string.IsNullOrEmpty(completionText))
            {
                return;
            }

            var documentPath = session != null ? session.FilePath ?? string.Empty : string.Empty;
            var entries = editorState.RecentAcceptedCompletions;
            for (var i = entries.Count - 1; i >= 0; i--)
            {
                var existing = entries[i];
                if (existing == null)
                {
                    entries.RemoveAt(i);
                    continue;
                }

                if (string.Equals(existing.DocumentPath ?? string.Empty, documentPath, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(existing.CompletionText ?? string.Empty, completionText, StringComparison.OrdinalIgnoreCase))
                {
                    entries.RemoveAt(i);
                }
            }

            entries.Add(new CortexAcceptedCompletionEntry
            {
                DocumentPath = documentPath,
                CompletionText = completionText,
                Sequence = ++editorState.CompletionAcceptanceSequence
            });

            const int maxAcceptedEntries = 12;
            while (entries.Count > maxAcceptedEntries)
            {
                entries.RemoveAt(0);
            }
        }

        private static void LogQueueBehavior(
            DocumentSession session,
            CortexEditorInteractionState editorState,
            string triggerCharacter,
            bool explicitInvocation)
        {
            if (explicitInvocation)
            {
                MMLog.LogOnce("Cortex.Completion.Trigger.Explicit", delegate
                {
                    MMLog.WriteInfo("[Cortex.Completion] Explicit completion requests are enabled through the editor completion command.");
                });
            }
            else if (!string.IsNullOrEmpty(triggerCharacter))
            {
                var trigger = triggerCharacter[0];
                if (trigger == '.')
                {
                    MMLog.LogOnce("Cortex.Completion.Trigger.Dot", delegate
                    {
                        MMLog.WriteInfo("[Cortex.Completion] Typing '.' auto-queues Roslyn member completion.");
                    });
                }
                else if (trigger == '_')
                {
                    MMLog.LogOnce("Cortex.Completion.Trigger.Underscore", delegate
                    {
                        MMLog.WriteInfo("[Cortex.Completion] Typing '_' currently auto-queues Roslyn completion.");
                    });
                }
                else if (char.IsLetterOrDigit(trigger))
                {
                    MMLog.LogOnce("Cortex.Completion.Trigger.Identifier", delegate
                    {
                        MMLog.WriteInfo("[Cortex.Completion] Identifier characters currently auto-queue completion. Typing a letter like 'E' will request Roslyn suggestions with the current caret context.");
                    });
                }
            }
            else
            {
                MMLog.LogOnce("Cortex.Completion.Trigger.Continue", delegate
                {
                    MMLog.WriteInfo("[Cortex.Completion] Completion can be re-queued while typing through an identifier so the popup stays in sync with the current token.");
                });
            }

        }

        private static void LogRejectedResponse(DocumentSession target, LanguageServiceCompletionResponse response)
        {
            if (response == null)
            {
                MMLog.LogOnce("Cortex.Completion.Response.Null", delegate
                {
                    MMLog.WriteInfo("[Cortex.Completion] A completion response was received but the payload was null, so the popup was cleared.");
                });
                return;
            }

            if (!response.Success || response.Items == null || response.Items.Length == 0)
            {
                MMLog.LogOnce("Cortex.Completion.Response.Empty", delegate
                {
                    MMLog.WriteInfo("[Cortex.Completion] Roslyn can return an empty completion result for the current caret context. When that happens Cortex clears the popup instead of leaving stale suggestions visible.");
                });
            }

            MMLog.WriteInfo("[Cortex.Completion] Rejected response for " +
                BuildDocumentLabel(target) +
                ". Success=" + response.Success +
                ", Status=" + (response.StatusMessage ?? string.Empty) +
                ", Items=" + (response.Items != null ? response.Items.Length : 0) + ".");
        }

        private static string BuildDocumentLabel(DocumentSession session)
        {
            return session != null && !string.IsNullOrEmpty(session.FilePath)
                ? System.IO.Path.GetFileName(session.FilePath)
                : "<unsaved>";
        }

        private static string BuildCompletionPreview(LanguageServiceCompletionResponse response)
        {
            if (response == null || response.Items == null || response.Items.Length == 0)
            {
                return "<empty>";
            }

            var builder = new StringBuilder();
            var count = Math.Min(5, response.Items.Length);
            for (var i = 0; i < count; i++)
            {
                var item = response.Items[i];
                if (item == null)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(item.DisplayText ?? string.Empty);
            }

            return builder.ToString();
        }

        private static string BuildMatchSummary(LanguageServiceCompletionResponse response, string query)
        {
            if (response == null || response.Items == null || response.Items.Length == 0 || string.IsNullOrEmpty(query))
            {
                return "none";
            }

            var prefixMatches = 0;
            var wordPartMatches = 0;
            var containsMatches = 0;
            for (var i = 0; i < response.Items.Length; i++)
            {
                var item = response.Items[i];
                if (item == null)
                {
                    continue;
                }

                var candidate = !string.IsNullOrEmpty(item.FilterText)
                    ? item.FilterText
                    : (!string.IsNullOrEmpty(item.DisplayText) ? item.DisplayText : item.InsertText ?? string.Empty);
                if (candidate.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                {
                    prefixMatches++;
                }
                else if (StartsWithWordPart(candidate, query))
                {
                    wordPartMatches++;
                }
                else if (candidate.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    containsMatches++;
                }
            }

            return "prefix=" + prefixMatches +
                ", wordPart=" + wordPartMatches +
                ", contains=" + containsMatches;
        }

        private static bool StartsWithWordPart(string candidate, string query)
        {
            if (string.IsNullOrEmpty(candidate) || string.IsNullOrEmpty(query))
            {
                return false;
            }

            for (var i = 1; i < candidate.Length; i++)
            {
                var current = candidate[i];
                var previous = candidate[i - 1];
                if (current == '_')
                {
                    continue;
                }

                if (previous == '_' ||
                    previous == '.' ||
                    previous == ':' ||
                    (char.IsUpper(current) && !char.IsUpper(previous)))
                {
                    var remaining = candidate.Length - i;
                    if (remaining >= query.Length &&
                        string.Compare(candidate, i, query, 0, query.Length, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }

    internal sealed class DocumentLanguageCompletionRequestState
    {
        public string RequestId;
        public int Generation;
        public string RequestKey;
        public string DocumentPath;
        public int DocumentVersion;
    }
}
