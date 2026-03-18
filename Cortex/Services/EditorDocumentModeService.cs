using Cortex.Core.Models;

namespace Cortex.Services
{
    internal sealed class EditorDocumentModeService
    {
        public bool UsesUnifiedSourceSurface(DocumentSession session)
        {
            return session != null && session.Kind == DocumentKind.SourceCode;
        }

        public bool CanToggleEditing(CortexSettings settings, DocumentSession session)
        {
            return settings != null &&
                settings.EnableFileEditing &&
                session != null &&
                session.SupportsEditing;
        }

        public bool IsEditingEnabled(CortexSettings settings, DocumentSession session)
        {
            return CanToggleEditing(settings, session) &&
                session.EditorState != null &&
                session.EditorState.EditModeEnabled;
        }

        public bool SetEditingEnabled(CortexSettings settings, DocumentSession session, bool enabled)
        {
            if (!CanToggleEditing(settings, session) || session.EditorState == null)
            {
                return false;
            }

            session.EditorState.EditModeEnabled = enabled;
            return true;
        }

        public string BuildEditModeTooltip(DocumentSession session, CortexSettings settings)
        {
            var editingAllowed = settings != null && settings.EnableFileEditing;
            var canEditDocument = CanToggleEditing(settings, session);
            var isEditing = IsEditingEnabled(settings, session);
            if (!editingAllowed)
            {
                return "Enable File Editing in Settings to allow source tabs to switch into edit mode.";
            }

            if (!canEditDocument)
            {
                return session != null && session.Kind == DocumentKind.DecompiledCode
                    ? "Decompiler output is read-only. Open the source file instead to edit code."
                    : "This document type is read-only and cannot switch into edit mode.";
            }

            return isEditing
                ? "Edit mode is already active for this tab."
                : "Switch this source tab into in-memory edit mode.";
        }
    }
}
