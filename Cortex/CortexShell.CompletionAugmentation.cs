using System;
using Cortex.CompletionProviders;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using Cortex.Services;
using ModAPI.Core;

namespace Cortex
{
    public sealed partial class CortexShell
    {
        private ICompletionAugmentationClient _completionAugmentationClient;
        private bool _completionAugmentationInFlight;
        private PendingCompletionAugmentationRequest _pendingCompletionAugmentation;

        private void InitializeCompletionAugmentation(CortexSettings settings)
        {
            ShutdownCompletionAugmentation();
            MMLog.WriteInfo("[Cortex.Completion.Augmentation] Initializing. EnableCompletionAugmentation=" +
                (settings != null && settings.EnableCompletionAugmentation) +
                ", RequestedProvider=" + (settings != null ? settings.CompletionAugmentationProviderId ?? string.Empty : string.Empty) +
                ", EnableTabby=" + (settings != null && settings.EnableTabbyCompletion) +
                ", TabbyServerUrl=" + (settings != null ? settings.TabbyServerUrl ?? string.Empty : string.Empty) +
                ", OllamaModel=" + (settings != null ? settings.OllamaModel ?? string.Empty : string.Empty) + ".");
            _completionAugmentationClient = CompletionAugmentationBootstrapper.Create(settings, delegate(string message)
            {
                MMLog.WriteInfo(message);
            });
            MMLog.WriteInfo("[Cortex.Completion.Augmentation] Provider initialized: " +
                (_completionAugmentationClient != null ? _completionAugmentationClient.ProviderId ?? string.Empty : "<none>") +
                ", Enabled=" + (_completionAugmentationClient != null && _completionAugmentationClient.IsEnabled) +
                ", RequestedProvider=" + (settings != null ? settings.CompletionAugmentationProviderId ?? string.Empty : string.Empty) + ".");
        }

        private void ShutdownCompletionAugmentation()
        {
            if (_completionAugmentationClient != null)
            {
                _completionAugmentationClient.Dispose();
            }

            _completionAugmentationClient = null;
            _completionAugmentationInFlight = false;
            _pendingCompletionAugmentation = null;
        }

        private void ProcessCompletionAugmentationResponses()
        {
            if (_completionAugmentationClient == null)
            {
                return;
            }

            CompletionAugmentationResult result;
            while (_completionAugmentationClient.TryDequeueResponse(out result))
            {
                if (result == null || _pendingCompletionAugmentation == null ||
                    !string.Equals(result.RequestId, _pendingCompletionAugmentation.RequestId, StringComparison.Ordinal))
                {
                    continue;
                }

                HandleCompletionAugmentationResponse(result, _pendingCompletionAugmentation);
                _pendingCompletionAugmentation = null;
            }
        }

        private bool TryQueueCompletionAugmentation(
            DocumentLanguageCompletionRequestState pending,
            CompletionAugmentationRequest request,
            LanguageServiceCompletionResponse primaryResponse)
        {
            if (_completionAugmentationClient == null)
            {
                MMLog.WriteInfo("[Cortex.Completion.Augmentation] Queue skipped. Reason=NoClient.");
                return false;
            }

            if (!_completionAugmentationClient.IsEnabled)
            {
                MMLog.WriteInfo("[Cortex.Completion.Augmentation] Queue skipped. Reason=ClientDisabled, Provider=" +
                    (_completionAugmentationClient.ProviderId ?? string.Empty) +
                    ", LastError=" + (_completionAugmentationClient.LastError ?? string.Empty) + ".");
                return false;
            }

            if (_completionAugmentationInFlight)
            {
                MMLog.WriteInfo("[Cortex.Completion.Augmentation] Queue skipped. Reason=InFlight, PendingRequestId=" +
                    (_pendingCompletionAugmentation != null ? _pendingCompletionAugmentation.RequestId ?? string.Empty : string.Empty) + ".");
                return false;
            }

            if (pending == null)
            {
                MMLog.WriteInfo("[Cortex.Completion.Augmentation] Queue skipped. Reason=PendingStateMissing.");
                return false;
            }

            if (request == null)
            {
                MMLog.WriteInfo("[Cortex.Completion.Augmentation] Queue skipped. Reason=RequestMissing, Document=" +
                    (pending.DocumentPath ?? string.Empty) +
                    ", Position=" + pending.AbsolutePosition + ".");
                return false;
            }

            if (_completionAugmentationClient == null ||
                !_completionAugmentationClient.IsEnabled ||
                _completionAugmentationInFlight ||
                pending == null ||
                request == null)
            {
                return false;
            }

            var requestId = _completionAugmentationClient.QueueCompletion(request);
            if (string.IsNullOrEmpty(requestId))
            {
                MMLog.WriteInfo("[Cortex.Completion.Augmentation] Request was not queued. Provider=" +
                    (_completionAugmentationClient.ProviderId ?? string.Empty) +
                    ", LastError=" + (_completionAugmentationClient.LastError ?? string.Empty) + ".");
                return false;
            }

            _completionAugmentationInFlight = true;
            _pendingCompletionAugmentation = new PendingCompletionAugmentationRequest
            {
                RequestId = requestId,
                RequestKey = pending.RequestKey ?? string.Empty,
                DocumentPath = pending.DocumentPath ?? string.Empty,
                DocumentVersion = pending.DocumentVersion,
                AbsolutePosition = pending.AbsolutePosition,
                PreferredReplacementRange = primaryResponse != null ? CloneRange(primaryResponse.ReplacementRange) : null
            };
            MMLog.WriteInfo("[Cortex.Completion.Augmentation] Queued request " + requestId +
                ". Provider=" + (_completionAugmentationClient.ProviderId ?? string.Empty) +
                ", Document=" + (request.DocumentPath ?? string.Empty) +
                ", Position=" + request.AbsolutePosition + ".");
            return true;
        }

        private void HandleCompletionAugmentationResponse(
            CompletionAugmentationResult result,
            PendingCompletionAugmentationRequest pending)
        {
            _completionAugmentationInFlight = false;
            if (pending == null || _state.Editor == null)
            {
                return;
            }

            var target = FindOpenDocument(result.Response != null ? result.Response.DocumentPath : pending.DocumentPath);
            if (target == null || result.Response == null || !result.Response.Success)
            {
                MMLog.WriteInfo("[Cortex.Completion.Augmentation] Response was not applied. Provider=" +
                    (result != null ? result.ProviderId ?? string.Empty : string.Empty) +
                    ", HasTarget=" + (target != null) +
                    ", Success=" + (result != null && result.Response != null && result.Response.Success) +
                    ", Status=" + (result != null && result.Response != null ? result.Response.StatusMessage ?? string.Empty : string.Empty) + ".");
                if (_state.Editor.ActiveCompletionResponse == null)
                {
                    _editorCompletionService.ClearPendingRequest(_state.Editor);
                }
                return;
            }

            if (pending.DocumentVersion > 0 &&
                target.TextVersion > 0 &&
                target.TextVersion != pending.DocumentVersion)
            {
                MMLog.WriteInfo("[Cortex.Completion.Augmentation] Accepting version-shifted response. Provider=" +
                    (result != null ? result.ProviderId ?? string.Empty : string.Empty) +
                    ", PendingVersion=" + pending.DocumentVersion +
                    ", LiveVersion=" + target.TextVersion +
                    ", Document=" + (target.FilePath ?? string.Empty) + ".");
            }

            if (pending.PreferredReplacementRange != null)
            {
                result.Response.ReplacementRange = CloneRange(pending.PreferredReplacementRange);
            }

            // The editor may continue typing while the AI request is in flight.
            // Once we accept the shifted response above, normalize it to the live
            // document version so the merge path does not reject it as stale.
            result.Response.DocumentPath = target.FilePath ?? result.Response.DocumentPath ?? pending.DocumentPath ?? string.Empty;
            if (target.TextVersion > 0)
            {
                result.Response.DocumentVersion = target.TextVersion;
            }

            var merged = _editorCompletionService.MergeSupplementalResponse(
                _state.Editor,
                target,
                new DocumentLanguageCompletionRequestState
                {
                    RequestKey = pending.RequestKey,
                    DocumentPath = pending.DocumentPath,
                    DocumentVersion = pending.DocumentVersion,
                    AbsolutePosition = pending.AbsolutePosition
                },
                result.Response);
            MMLog.WriteInfo("[Cortex.Completion.Augmentation] Response processed. Provider=" +
                (result != null ? result.ProviderId ?? string.Empty : string.Empty) +
                ", Merged=" + merged +
                ", Items=" + (result != null && result.Response != null && result.Response.Items != null ? result.Response.Items.Length : 0) +
                ", Document=" + (target != null ? target.FilePath ?? string.Empty : string.Empty) + ".");
            if (!merged && _state.Editor.ActiveCompletionResponse == null)
            {
                _editorCompletionService.ClearPendingRequest(_state.Editor);
            }
        }

        private CompletionAugmentationRequest BuildCompletionAugmentationRequest(
            DocumentSession session,
            DocumentLanguageCompletionRequestState pending)
        {
            if (session == null || pending == null)
            {
                return null;
            }

            var prefixText = BuildPrefixText(session.Text, pending.AbsolutePosition);
            var suffixText = BuildSuffixText(session.Text, pending.AbsolutePosition);
            return new CompletionAugmentationRequest
            {
                ProviderId = _state.Settings != null ? _state.Settings.CompletionAugmentationProviderId ?? string.Empty : string.Empty,
                DocumentPath = session.FilePath ?? pending.DocumentPath ?? string.Empty,
                ProjectFilePath = string.Empty,
                WorkspaceRootPath = _state.Settings != null ? _state.Settings.WorkspaceRootPath ?? string.Empty : string.Empty,
                RelativeDocumentPath = BuildRelativePath(
                    session.FilePath ?? pending.DocumentPath ?? string.Empty,
                    _state.Settings != null ? _state.Settings.WorkspaceRootPath ?? string.Empty : string.Empty),
                LanguageId = MapLanguageId(session.FilePath ?? pending.DocumentPath ?? string.Empty),
                DocumentText = session.Text ?? string.Empty,
                DocumentVersion = session.TextVersion,
                AbsolutePosition = pending.AbsolutePosition,
                ExplicitInvocation = _state.Editor != null && _state.Editor.RequestedCompletionExplicit,
                TriggerCharacter = _state.Editor != null ? _state.Editor.RequestedCompletionTriggerCharacter ?? string.Empty : string.Empty,
                PrefixText = prefixText,
                SuffixText = suffixText,
                SelectedText = BuildSelectedText(session),
                AdditionalInstructions = _state.Settings != null ? _state.Settings.CompletionAugmentationAdditionalInstructions ?? string.Empty : string.Empty,
                ReplaceProviderPrompt = _state.Settings != null && _state.Settings.CompletionAugmentationReplaceProviderPrompt,
                Declarations = new string[0],
                RelatedSnippets = BuildRelatedSnippets(session)
            };
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

        private sealed class PendingCompletionAugmentationRequest
        {
            public string RequestId;
            public string RequestKey;
            public string DocumentPath;
            public int DocumentVersion;
            public int AbsolutePosition;
            public LanguageServiceRange PreferredReplacementRange;
        }

        private static string BuildPrefixText(string text, int absolutePosition)
        {
            var value = text ?? string.Empty;
            var caret = Math.Max(0, Math.Min(value.Length, absolutePosition));
            return caret > 0 ? value.Substring(0, caret) : string.Empty;
        }

        private static string BuildSuffixText(string text, int absolutePosition)
        {
            var value = text ?? string.Empty;
            var caret = Math.Max(0, Math.Min(value.Length, absolutePosition));
            return caret < value.Length ? value.Substring(caret) : string.Empty;
        }

        private CompletionAugmentationSnippet[] BuildRelatedSnippets(DocumentSession activeSession)
        {
            if (_state == null ||
                _state.Settings == null ||
                !_state.Settings.CompletionAugmentationIncludeOpenDocumentSnippets ||
                _state.Documents == null ||
                _state.Documents.OpenDocuments == null)
            {
                return new CompletionAugmentationSnippet[0];
            }

            var maxDocuments = Math.Max(0, _state.Settings.CompletionAugmentationSnippetDocumentLimit);
            var maxCharacters = Math.Max(64, _state.Settings.CompletionAugmentationSnippetCharacterLimit);
            if (maxDocuments == 0)
            {
                return new CompletionAugmentationSnippet[0];
            }

            var snippets = new System.Collections.Generic.List<CompletionAugmentationSnippet>();
            for (var i = 0; i < _state.Documents.OpenDocuments.Count; i++)
            {
                var candidate = _state.Documents.OpenDocuments[i];
                if (candidate == null ||
                    candidate == activeSession ||
                    candidate.Kind != DocumentKind.SourceCode ||
                    string.IsNullOrEmpty(candidate.Text))
                {
                    continue;
                }

                snippets.Add(new CompletionAugmentationSnippet
                {
                    SourceId = candidate.FilePath ?? string.Empty,
                    DisplayName = System.IO.Path.GetFileName(candidate.FilePath ?? string.Empty),
                    RelativePath = BuildRelativePath(
                        candidate.FilePath ?? string.Empty,
                        _state.Settings.WorkspaceRootPath ?? string.Empty),
                    Content = TrimSnippet(candidate.Text, maxCharacters),
                    Score = candidate.IsDirty ? 1f : 0.5f
                });
                if (snippets.Count >= maxDocuments)
                {
                    break;
                }
            }

            return snippets.ToArray();
        }

        private static string BuildSelectedText(DocumentSession session)
        {
            if (session == null || session.EditorState == null || string.IsNullOrEmpty(session.Text))
            {
                return string.Empty;
            }

            var selection = session.EditorState.PrimarySelection;
            if (selection == null || !selection.HasSelection)
            {
                return string.Empty;
            }

            var start = Math.Max(0, Math.Min(session.Text.Length, selection.Start));
            var end = Math.Max(start, Math.Min(session.Text.Length, selection.End));
            return end > start ? session.Text.Substring(start, end - start) : string.Empty;
        }

        private static string TrimSnippet(string text, int maxCharacters)
        {
            var value = text ?? string.Empty;
            if (value.Length <= maxCharacters)
            {
                return value;
            }

            return value.Substring(0, maxCharacters);
        }

        private static string BuildRelativePath(string documentPath, string workspaceRootPath)
        {
            if (string.IsNullOrEmpty(documentPath))
            {
                return string.Empty;
            }

            if (string.IsNullOrEmpty(workspaceRootPath))
            {
                return documentPath.Replace('\\', '/');
            }

            try
            {
                var normalizedRoot = workspaceRootPath.EndsWith("\\", StringComparison.Ordinal) || workspaceRootPath.EndsWith("/", StringComparison.Ordinal)
                    ? workspaceRootPath
                    : workspaceRootPath + "\\";
                var rootUri = new Uri(normalizedRoot, UriKind.Absolute);
                var pathUri = new Uri(documentPath, UriKind.Absolute);
                return Uri.UnescapeDataString(rootUri.MakeRelativeUri(pathUri).ToString());
            }
            catch
            {
                return documentPath.Replace('\\', '/');
            }
        }

        private static string MapLanguageId(string documentPath)
        {
            var extension = !string.IsNullOrEmpty(documentPath)
                ? System.IO.Path.GetExtension(documentPath)
                : string.Empty;
            switch ((extension ?? string.Empty).ToLowerInvariant())
            {
                case ".cs":
                    return "csharp";
                case ".js":
                    return "javascript";
                case ".ts":
                    return "typescript";
                case ".tsx":
                    return "typescriptreact";
                case ".jsx":
                    return "javascriptreact";
                case ".py":
                    return "python";
                case ".json":
                    return "json";
                case ".xml":
                    return "xml";
                case ".java":
                    return "java";
                case ".cpp":
                case ".cc":
                case ".cxx":
                    return "cpp";
                case ".c":
                    return "c";
                default:
                    return "plaintext";
            }
        }
    }
}
