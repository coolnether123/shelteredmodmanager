using System;
using System.Collections.Generic;
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
            editorState.ActiveCompletionKey = pending.RequestKey ?? string.Empty;
            editorState.ActiveCompletionResponse = response;
            ResetSelection(editorState);
            SyncSelection(editorState);
            return true;
        }

        public bool MergeSupplementalResponse(
            CortexEditorInteractionState editorState,
            DocumentSession target,
            DocumentLanguageCompletionRequestState pending,
            LanguageServiceCompletionResponse response)
        {
            if (editorState == null || pending == null || target == null || response == null)
            {
                return false;
            }

            if ((response.DocumentVersion > 0 && target.TextVersion > 0 && response.DocumentVersion != target.TextVersion) ||
                !HasCompletionItems(response))
            {
                return false;
            }

            var active = editorState.ActiveCompletionResponse;
            if (active != null &&
                string.Equals(active.DocumentPath ?? string.Empty, target.FilePath ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
                active.DocumentVersion == target.TextVersion &&
                active.ReplacementRange != null &&
                pending.AbsolutePosition >= active.ReplacementRange.Start)
            {
                var replacementPrefixLength = pending.AbsolutePosition - active.ReplacementRange.Start;
                var replacementPrefix = replacementPrefixLength > 0 &&
                    !string.IsNullOrEmpty(target.Text) &&
                    active.ReplacementRange.Start >= 0 &&
                    active.ReplacementRange.Start + replacementPrefixLength <= target.Text.Length
                    ? target.Text.Substring(active.ReplacementRange.Start, replacementPrefixLength)
                    : string.Empty;
                NormalizeSupplementalItems(response, replacementPrefix);
                response.ReplacementRange = CloneRange(active.ReplacementRange);
            }
            else if (response.ReplacementRange == null)
            {
                response.ReplacementRange = new LanguageServiceRange
                {
                    Start = Math.Max(0, pending.AbsolutePosition),
                    Length = 0
                };
            }

            var combined = CloneCompletionResponse(active);
            if (combined == null)
            {
                combined = CloneCompletionResponse(response);
            }
            else
            {
                combined.Items = MergeItems(combined.Items, response.Items);
            }

            combined.DocumentPath = target.FilePath ?? response.DocumentPath ?? pending.DocumentPath ?? string.Empty;
            combined.DocumentVersion = target.TextVersion > 0 ? target.TextVersion : response.DocumentVersion;
            if (combined.ReplacementRange == null)
            {
                combined.ReplacementRange = CloneRange(response.ReplacementRange);
            }

            combined = _completionRankingService.Rank(target, editorState, combined);
            editorState.ActiveCompletionKey = pending.RequestKey ?? string.Empty;
            editorState.ActiveCompletionResponse = combined;
            ResetSelection(editorState);
            SyncSelection(editorState);
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

        public void ClearPendingRequest(CortexEditorInteractionState editorState)
        {
            ClearRequest(editorState);
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

        private static void NormalizeSupplementalItems(LanguageServiceCompletionResponse response, string replacementPrefix)
        {
            if (response == null || response.Items == null)
            {
                return;
            }

            for (var i = 0; i < response.Items.Length; i++)
            {
                var item = response.Items[i];
                if (item == null)
                {
                    continue;
                }

                var insertText = item.InsertText ?? string.Empty;
                if (!string.IsNullOrEmpty(replacementPrefix) &&
                    !insertText.StartsWith(replacementPrefix, StringComparison.Ordinal))
                {
                    item.InsertText = replacementPrefix + insertText;
                }

                if (string.IsNullOrEmpty(item.FilterText))
                {
                    item.FilterText = item.InsertText ?? item.DisplayText ?? string.Empty;
                }
            }
        }

        private static LanguageServiceCompletionResponse CloneCompletionResponse(LanguageServiceCompletionResponse response)
        {
            if (response == null)
            {
                return null;
            }

            var clone = new LanguageServiceCompletionResponse
            {
                Success = response.Success,
                StatusMessage = response.StatusMessage,
                DocumentPath = response.DocumentPath,
                ProjectFilePath = response.ProjectFilePath,
                DocumentVersion = response.DocumentVersion,
                ReplacementRange = CloneRange(response.ReplacementRange)
            };

            if (response.Items == null || response.Items.Length == 0)
            {
                clone.Items = new LanguageServiceCompletionItem[0];
                return clone;
            }

            clone.Items = new LanguageServiceCompletionItem[response.Items.Length];
            for (var i = 0; i < response.Items.Length; i++)
            {
                clone.Items[i] = CloneItem(response.Items[i]);
            }

            return clone;
        }

        private static LanguageServiceRange CloneRange(LanguageServiceRange range)
        {
            return range == null
                ? null
                : new LanguageServiceRange
                {
                    StartLine = range.StartLine,
                    StartColumn = range.StartColumn,
                    EndLine = range.EndLine,
                    EndColumn = range.EndColumn,
                    Start = range.Start,
                    Length = range.Length
                };
        }

        private static LanguageServiceCompletionItem CloneItem(LanguageServiceCompletionItem item)
        {
            return item == null
                ? null
                : new LanguageServiceCompletionItem
                {
                    DisplayText = item.DisplayText,
                    InsertText = item.InsertText,
                    FilterText = item.FilterText,
                    SortText = item.SortText,
                    InlineDescription = item.InlineDescription,
                    Kind = item.Kind,
                    IsPreselected = item.IsPreselected
                };
        }

        private static LanguageServiceCompletionItem[] MergeItems(
            LanguageServiceCompletionItem[] existing,
            LanguageServiceCompletionItem[] incoming)
        {
            if ((existing == null || existing.Length == 0) &&
                (incoming == null || incoming.Length == 0))
            {
                return new LanguageServiceCompletionItem[0];
            }

            var results = new List<LanguageServiceCompletionItem>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AppendItems(existing, results, seen);
            AppendItems(incoming, results, seen);
            return results.ToArray();
        }

        private static void AppendItems(
            LanguageServiceCompletionItem[] items,
            List<LanguageServiceCompletionItem> results,
            HashSet<string> seen)
        {
            if (items == null)
            {
                return;
            }

            for (var i = 0; i < items.Length; i++)
            {
                var item = CloneItem(items[i]);
                if (item == null)
                {
                    continue;
                }

                var key = (item.InsertText ?? string.Empty) + "|" +
                    (item.FilterText ?? string.Empty) + "|" +
                    (item.DisplayText ?? string.Empty);
                if (!seen.Add(key))
                {
                    continue;
                }

                results.Add(item);
            }
        }

        private static void LogQueueBehavior(
            DocumentSession session,
            CortexEditorInteractionState editorState,
            string triggerCharacter,
            bool explicitInvocation)
        {
            // Roslyn completion triggers are expected editor behavior; keep runtime logs focused on failures.
        }

        private static void LogRejectedResponse(DocumentSession target, LanguageServiceCompletionResponse response)
        {
            if (response == null)
            {
                return;
            }

            if (!response.Success)
            {
                MMLog.WriteWarning("[Cortex.Completion] Rejected Roslyn response for " +
                    BuildDocumentLabel(target) +
                    ". Status=" + (response.StatusMessage ?? string.Empty) +
                    ", Items=" + (response.Items != null ? response.Items.Length : 0) + ".");
            }
        }

        private static string BuildDocumentLabel(DocumentSession session)
        {
            return session != null && !string.IsNullOrEmpty(session.FilePath)
                ? System.IO.Path.GetFileName(session.FilePath)
                : "<unsaved>";
        }

    }

    internal sealed class DocumentLanguageCompletionRequestState
    {
        public string RequestId;
        public int Generation;
        public string RequestKey;
        public string DocumentPath;
        public int DocumentVersion;
        public int AbsolutePosition;
    }
}
