using System;
using Cortex.Core.Models;
using Cortex.Modules.Shared;
using Cortex.Services.Editor.Context;
using Cortex.LanguageService.Protocol;

namespace Cortex.Services.Harmony.Resolution
{
    internal sealed class HarmonyResolutionTargetClassifier
    {
        private readonly EditorCommandContextFactory _commandContextFactory = new EditorCommandContextFactory();
        private readonly EditorDocumentModeService _documentModeService = new EditorDocumentModeService();

        public bool TryClassifyEditorTarget(CortexShellState state, EditorCommandTarget target, out HarmonyResolutionTargetRequest request, out string reason)
        {
            request = null;
            reason = string.Empty;
            if (target == null || string.IsNullOrEmpty(target.DocumentPath))
            {
                reason = "Select a resolvable method before using Harmony actions.";
                return false;
            }

            request = CreateRequest(state, target);
            return true;
        }

        public bool TryClassifyDocument(CortexShellState state, DocumentSession session, out HarmonyResolutionTargetRequest request, out string reason)
        {
            request = null;
            reason = string.Empty;
            if (session == null || string.IsNullOrEmpty(session.FilePath))
            {
                reason = "Open a source or decompiled method first.";
                return false;
            }

            var absolutePosition = session.EditorState != null ? session.EditorState.CaretIndex : 0;
            var target = BuildDocumentTarget(state, session, absolutePosition);
            if (target == null)
            {
                target = new EditorCommandTarget
                {
                    DocumentPath = session.FilePath ?? string.Empty,
                    AbsolutePosition = absolutePosition,
                    SymbolText = string.Empty
                };
            }

            request = CreateRequest(state, target);
            return true;
        }

        private HarmonyResolutionTargetRequest CreateRequest(CortexShellState state, EditorCommandTarget target)
        {
            return new HarmonyResolutionTargetRequest
            {
                Kind = CortexModuleUtil.IsDecompilerDocumentPath(state, target != null ? target.DocumentPath : string.Empty)
                    ? HarmonyResolutionTargetKind.Decompiled
                    : HarmonyResolutionTargetKind.Source,
                Target = target
            };
        }

        private EditorCommandTarget BuildDocumentTarget(CortexShellState state, DocumentSession session, int absolutePosition)
        {
            if (session == null)
            {
                return null;
            }

            EditorCommandInvocation invocation;
            if (!_commandContextFactory.TryCreateSourceInteractionInvocation(
                session,
                state,
                _documentModeService.IsEditingEnabled(state != null ? state.Settings : null, session),
                absolutePosition,
                GetMatchingHoverResponse(state, session != null ? session.FilePath : string.Empty, absolutePosition),
                out invocation) ||
                invocation == null)
            {
                return null;
            }

            return invocation.Target;
        }

        private static LanguageServiceHoverResponse GetMatchingHoverResponse(CortexShellState state, string documentPath, int absolutePosition)
        {
            if (state == null || state.EditorContext == null)
            {
                return null;
            }

            var key = (documentPath ?? string.Empty) + "|" + absolutePosition;
            foreach (var pair in state.EditorContext.ContextsByKey)
            {
                var snapshot = pair.Value;
                if (snapshot == null ||
                    snapshot.Semantic == null ||
                    !string.Equals(snapshot.HoverKey ?? string.Empty, key, StringComparison.Ordinal))
                {
                    continue;
                }

                return snapshot.Semantic.HoverResponse;
            }

            return null;
        }
    }
}
