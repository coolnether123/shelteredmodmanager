using System.IO;
using Cortex.Core.Diagnostics;
using UnityEngine;

namespace Cortex.Modules.Editor
{
    /// <summary>
    /// Centralized editor diagnostics sink.
    /// Hover diagnostics stay disabled by default to avoid noisy hit-test logging,
    /// while edit diagnostics can be enabled later without scattering direct log calls
    /// through the editor stack.
    /// </summary>
    internal static class EditorInteractionLog
    {
        private static readonly CortexDiagnosticLogger HoverDiagnostics = CortexDiagnostics.ForChannel("editor.hover", "Cortex.HoverUI");
        private static readonly CortexDiagnosticLogger EditDiagnostics = CortexDiagnostics.ForChannel("editor.edit", "Cortex.Editor");
        private static readonly CortexDiagnosticLogger SelectionDiagnostics = CortexDiagnostics.ForChannel("editor.selection", "Cortex.Editor");
        private static readonly CortexDiagnosticLogger ContextMenuDiagnostics = CortexDiagnostics.ForChannel("editor.context-menu", "Cortex.ContextMenuDiag");
        private static readonly CortexDiagnosticLogger ScrollDiagnostics = CortexDiagnostics.ForChannel("editor.scroll", "Cortex.ScrollDiag");
        private static string _lastScrollLogKey = string.Empty;
        private static float _lastScrollLogRealtime;

        public static bool IsSelectionDiagnosticsEnabled
        {
            get { return SelectionDiagnostics.IsEnabled(CortexLogLevel.Info); }
        }

        public static void WriteHover(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            HoverDiagnostics.WriteInfo(message);
        }

        public static void WriteEdit(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            EditDiagnostics.WriteInfo(message);
        }

        public static void WriteEditorStateBootstrap(string filePath, int requestedHighlightedLine, int line, int column, int index, bool hasExplicitCaretPlacement)
        {
            if (!SelectionDiagnostics.IsEnabled(CortexLogLevel.Info))
            {
                return;
            }

            SelectionDiagnostics.WriteInfo(
                "EnsureDocumentState bootstrapped caret. File=" + Path.GetFileName(filePath ?? string.Empty) +
                ", RequestedHighlightedLine=" + requestedHighlightedLine +
                ", ResultLine=" + line +
                ", ResultColumn=" + column +
                ", ResultIndex=" + index +
                ", HasExplicitCaretPlacement=" + hasExplicitCaretPlacement + ".");
        }

        public static void WritePointerSelection(
            string filePath,
            string action,
            int line,
            int column,
            int index,
            bool hasSelection,
            bool preGroupHit,
            bool usedRectOffset,
            Rect surfaceRect,
            Vector2 rawMouse,
            Vector2 surfaceMouse,
            Vector2 contentMouse,
            float gutterWidth,
            int textLength,
            int textVersion,
            int selectionCount,
            int hitLineIndex,
            int hitLineNumber,
            int hitLineStartIndex,
            int hitRawLength,
            int hitCharacterIndex,
            int hitCandidateColumn,
            float hitTargetX,
            float hitPreviousWidth,
            float hitCandidateWidth,
            float lineHeight)
        {
            if (!SelectionDiagnostics.IsEnabled(CortexLogLevel.Info) || string.IsNullOrEmpty(action))
            {
                return;
            }

            SelectionDiagnostics.WriteInfo(
                "Pointer selection. Action=" + action +
                ", File=" + Path.GetFileName(filePath ?? string.Empty) +
                ", Line=" + line +
                ", Column=" + column +
                ", Index=" + index +
                ", HasSelection=" + hasSelection +
                ", PreGroupHit=" + preGroupHit +
                ", UsedRectOffset=" + usedRectOffset +
                ", SurfaceRect=(" + surfaceRect.x.ToString("F1") + "," + surfaceRect.y.ToString("F1") + "," + surfaceRect.width.ToString("F1") + "," + surfaceRect.height.ToString("F1") + ")" +
                ", RawMouse=(" + rawMouse.x.ToString("F1") + "," + rawMouse.y.ToString("F1") + ")" +
                ", SurfaceMouse=(" + surfaceMouse.x.ToString("F1") + "," + surfaceMouse.y.ToString("F1") + ")" +
                ", ContentMouse=(" + contentMouse.x.ToString("F1") + "," + contentMouse.y.ToString("F1") + ")" +
                ", GutterWidth=" + gutterWidth.ToString("F1") +
                ", TextLength=" + textLength +
                ", TextVersion=" + textVersion +
                ", SelectionCount=" + selectionCount +
                ", HitLineIndex=" + hitLineIndex +
                ", HitLineNumber=" + hitLineNumber +
                ", HitLineStartIndex=" + hitLineStartIndex +
                ", HitRawLength=" + hitRawLength +
                ", HitCharacterIndex=" + hitCharacterIndex +
                ", HitCandidateColumn=" + hitCandidateColumn +
                ", HitTargetX=" + hitTargetX.ToString("F1") +
                ", HitPreviousWidth=" + hitPreviousWidth.ToString("F1") +
                ", HitCandidateWidth=" + hitCandidateWidth.ToString("F1") +
                ", LineHeight=" + lineHeight.ToString("F1") + ".");
        }

        public static void WriteKeyboardSelectionState(
            string filePath,
            string commandId,
            char character,
            int selectionCountBefore,
            int selectionCountAfter,
            int caretIndexBefore,
            int caretIndexAfter,
            int anchorIndexAfter,
            int lineAfter,
            int columnAfter)
        {
            if (!SelectionDiagnostics.IsEnabled(CortexLogLevel.Info))
            {
                return;
            }

            SelectionDiagnostics.WriteInfo(
                "Keyboard selection state. File=" + Path.GetFileName(filePath ?? string.Empty) +
                ", CommandId=" + (string.IsNullOrEmpty(commandId) ? "<text>" : commandId) +
                ", Character=" + ((int)character).ToString() +
                ", SelectionCountBefore=" + selectionCountBefore +
                ", SelectionCountAfter=" + selectionCountAfter +
                ", CaretIndexBefore=" + caretIndexBefore +
                ", CaretIndexAfter=" + caretIndexAfter +
                ", AnchorIndexAfter=" + anchorIndexAfter +
                ", LineAfter=" + lineAfter +
                ", ColumnAfter=" + columnAfter + ".");
        }

        public static void WriteContextMenu(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            ContextMenuDiagnostics.WriteInfo("Frame=" + Time.frameCount + " " + message);
        }

        public static void WriteScrollOwner(string owner, string message, bool force)
        {
            if (!ScrollDiagnostics.IsEnabled(CortexLogLevel.Info) || string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(message))
            {
                return;
            }

            var key = owner + "|" + message;
            var now = Time.realtimeSinceStartup;
            if (!force &&
                string.Equals(_lastScrollLogKey, key, System.StringComparison.Ordinal) &&
                (now - _lastScrollLogRealtime) < 0.35f)
            {
                return;
            }

            _lastScrollLogKey = key;
            _lastScrollLogRealtime = now;
            ScrollDiagnostics.WriteInfo("Frame=" + Time.frameCount + " Owner=" + owner + " " + message);
        }
    }
}
