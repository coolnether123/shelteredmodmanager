using System;
using System.IO;
using Cortex.Core.Models;
using Cortex.Modules.Shared;

namespace Cortex.Services.Editor.Context
{
    internal sealed class EditorLogicalDocumentTarget
    {
        public string DocumentPath = string.Empty;
        public int Line;
        public int Column;
        public int AbsolutePosition = -1;
        public DocumentSession Session;
        public bool SupportsEditing;
        public bool CanApplyEdits;
    }

    internal sealed class EditorLogicalDocumentTargetResolutionService
    {
        public bool TryResolveSourceDocument(CortexShellState state, EditorCommandTarget target, out EditorLogicalDocumentTarget resolvedTarget, out string reason)
        {
            resolvedTarget = null;
            reason = string.Empty;
            if (target == null)
            {
                reason = "Open a document to use this action.";
                return false;
            }

            if (target.DocumentKind == DocumentKind.SourceCode &&
                !string.IsNullOrEmpty(target.DocumentPath) &&
                !CortexModuleUtil.IsDecompilerDocumentPath(state, target.DocumentPath))
            {
                return TryBuildResolvedTarget(state, target.DocumentPath, target.Line, target.Column, target.AbsolutePosition, out resolvedTarget, out reason);
            }

            if (!string.IsNullOrEmpty(target.DefinitionDocumentPath) &&
                !CortexModuleUtil.IsDecompilerDocumentPath(state, target.DefinitionDocumentPath))
            {
                return TryBuildResolvedTarget(
                    state,
                    target.DefinitionDocumentPath,
                    target.DefinitionLine,
                    target.DefinitionColumn,
                    target.DefinitionStart,
                    out resolvedTarget,
                    out reason);
            }

            reason = "The current context does not resolve to an editable source document.";
            return false;
        }

        private static bool TryBuildResolvedTarget(
            CortexShellState state,
            string documentPath,
            int line,
            int column,
            int absolutePosition,
            out EditorLogicalDocumentTarget resolvedTarget,
            out string reason)
        {
            resolvedTarget = null;
            reason = string.Empty;
            if (string.IsNullOrEmpty(documentPath))
            {
                reason = "The current context does not resolve to a source document.";
                return false;
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(documentPath);
            }
            catch (Exception ex)
            {
                reason = "The resolved source path was invalid: " + ex.Message;
                return false;
            }

            if (!File.Exists(fullPath))
            {
                reason = "The resolved source file could not be found.";
                return false;
            }

            var session = CortexModuleUtil.FindOpenDocument(state, fullPath);
            resolvedTarget = new EditorLogicalDocumentTarget
            {
                DocumentPath = fullPath,
                Line = Math.Max(1, line),
                Column = Math.Max(1, column),
                AbsolutePosition = absolutePosition,
                Session = session,
                SupportsEditing = session != null ? session.SupportsEditing : !IsReadOnlyFile(fullPath),
                CanApplyEdits = session != null ? session.SupportsSaving || session.SupportsEditing : !IsReadOnlyFile(fullPath)
            };
            return true;
        }

        private static bool IsReadOnlyFile(string filePath)
        {
            try
            {
                var attributes = File.GetAttributes(filePath);
                return (attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly;
            }
            catch
            {
                return true;
            }
        }
    }
}
