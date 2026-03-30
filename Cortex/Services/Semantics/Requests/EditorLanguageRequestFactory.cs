using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;

namespace Cortex.Services.Semantics.Requests
{
    internal sealed class EditorLanguageRequestFactory : IEditorLanguageRequestFactory
    {
        public LanguageServiceSymbolContextRequest BuildSymbolContextRequest(DocumentSession session, CortexSettings settings, CortexProjectDefinition project, string[] sourceRoots, int line, int column, int absolutePosition)
        {
            var request = new LanguageServiceSymbolContextRequest();
            PopulateSymbolRequest(request, session, settings, project, sourceRoots, line, column, absolutePosition);
            return request;
        }

        public LanguageServiceHoverRequest BuildHoverRequest(DocumentSession session, CortexSettings settings, CortexProjectDefinition project, string[] sourceRoots, int line, int column, int absolutePosition)
        {
            return new LanguageServiceHoverRequest
            {
                DocumentPath = session != null ? session.FilePath : string.Empty,
                ProjectFilePath = project != null ? project.ProjectFilePath : string.Empty,
                WorkspaceRootPath = settings != null ? settings.WorkspaceRootPath : string.Empty,
                SourceRoots = sourceRoots ?? new string[0],
                DocumentText = session != null ? session.Text : string.Empty,
                DocumentVersion = session != null ? session.TextVersion : 0,
                Line = line,
                Column = column,
                AbsolutePosition = absolutePosition
            };
        }

        public LanguageServiceDefinitionRequest BuildDefinitionRequest(DocumentSession session, CortexSettings settings, CortexProjectDefinition project, string[] sourceRoots, int line, int column, int absolutePosition)
        {
            return new LanguageServiceDefinitionRequest
            {
                DocumentPath = session != null ? session.FilePath : string.Empty,
                ProjectFilePath = project != null ? project.ProjectFilePath : string.Empty,
                WorkspaceRootPath = settings != null ? settings.WorkspaceRootPath : string.Empty,
                SourceRoots = sourceRoots ?? new string[0],
                DocumentText = session != null ? session.Text : string.Empty,
                DocumentVersion = session != null ? session.TextVersion : 0,
                Line = line,
                Column = column,
                AbsolutePosition = absolutePosition
            };
        }

        public LanguageServiceRenameRequest BuildRenameRequest(DocumentSession session, CortexSettings settings, CortexProjectDefinition project, string[] sourceRoots, int line, int column, int absolutePosition, string newName)
        {
            var request = new LanguageServiceRenameRequest
            {
                NewName = newName ?? string.Empty
            };
            PopulateSymbolRequest(request, session, settings, project, sourceRoots, line, column, absolutePosition);
            return request;
        }

        public LanguageServiceReferencesRequest BuildReferencesRequest(DocumentSession session, CortexSettings settings, CortexProjectDefinition project, string[] sourceRoots, int line, int column, int absolutePosition)
        {
            var request = new LanguageServiceReferencesRequest();
            PopulateSymbolRequest(request, session, settings, project, sourceRoots, line, column, absolutePosition);
            return request;
        }

        public LanguageServiceBaseSymbolRequest BuildBaseSymbolRequest(DocumentSession session, CortexSettings settings, CortexProjectDefinition project, string[] sourceRoots, int line, int column, int absolutePosition)
        {
            var request = new LanguageServiceBaseSymbolRequest();
            PopulateSymbolRequest(request, session, settings, project, sourceRoots, line, column, absolutePosition);
            return request;
        }

        public LanguageServiceImplementationRequest BuildImplementationRequest(DocumentSession session, CortexSettings settings, CortexProjectDefinition project, string[] sourceRoots, int line, int column, int absolutePosition)
        {
            var request = new LanguageServiceImplementationRequest();
            PopulateSymbolRequest(request, session, settings, project, sourceRoots, line, column, absolutePosition);
            return request;
        }

        public LanguageServiceCallHierarchyRequest BuildCallHierarchyRequest(DocumentSession session, CortexSettings settings, CortexProjectDefinition project, string[] sourceRoots, int line, int column, int absolutePosition)
        {
            var request = new LanguageServiceCallHierarchyRequest();
            PopulateSymbolRequest(request, session, settings, project, sourceRoots, line, column, absolutePosition);
            return request;
        }

        public LanguageServiceValueSourceRequest BuildValueSourceRequest(DocumentSession session, CortexSettings settings, CortexProjectDefinition project, string[] sourceRoots, int line, int column, int absolutePosition)
        {
            var request = new LanguageServiceValueSourceRequest();
            PopulateSymbolRequest(request, session, settings, project, sourceRoots, line, column, absolutePosition);
            return request;
        }

        public string BuildCompletionRequestKey(string documentPath, int documentVersion, int absolutePosition, bool explicitInvocation, string triggerCharacter)
        {
            return (documentPath ?? string.Empty) +
                "|" + documentVersion +
                "|" + absolutePosition +
                "|" + explicitInvocation +
                "|" + (triggerCharacter ?? string.Empty);
        }

        public LanguageServiceSignatureHelpRequest BuildSignatureHelpRequest(DocumentSession session, CortexSettings settings, CortexProjectDefinition project, string[] sourceRoots, int line, int column, int absolutePosition, bool explicitInvocation, string triggerCharacter)
        {
            return new LanguageServiceSignatureHelpRequest
            {
                DocumentPath = session != null ? session.FilePath : string.Empty,
                ProjectFilePath = project != null ? project.ProjectFilePath : string.Empty,
                WorkspaceRootPath = settings != null ? settings.WorkspaceRootPath : string.Empty,
                SourceRoots = sourceRoots ?? new string[0],
                DocumentText = session != null ? session.Text : string.Empty,
                DocumentVersion = session != null ? session.TextVersion : 0,
                Line = line,
                Column = column,
                AbsolutePosition = absolutePosition,
                ExplicitInvocation = explicitInvocation,
                TriggerCharacter = triggerCharacter ?? string.Empty
            };
        }

        public LanguageServiceDocumentTransformRequest BuildDocumentTransformRequest(DocumentSession session, CortexSettings settings, CortexProjectDefinition project, string[] sourceRoots, string commandId, string title, string applyLabel, bool organizeImports, bool simplifyNames, bool formatDocument)
        {
            return new LanguageServiceDocumentTransformRequest
            {
                CommandId = commandId ?? string.Empty,
                Title = title ?? string.Empty,
                ApplyLabel = applyLabel ?? string.Empty,
                DocumentPath = session != null ? session.FilePath : string.Empty,
                ProjectFilePath = project != null ? project.ProjectFilePath : string.Empty,
                WorkspaceRootPath = settings != null ? settings.WorkspaceRootPath : string.Empty,
                SourceRoots = sourceRoots ?? new string[0],
                DocumentText = session != null ? session.Text : string.Empty,
                DocumentVersion = session != null ? session.TextVersion : 0,
                OrganizeImports = organizeImports,
                SimplifyNames = simplifyNames,
                FormatDocument = formatDocument
            };
        }

        private static void PopulateSymbolRequest(LanguageServiceSymbolRequest request, DocumentSession session, CortexSettings settings, CortexProjectDefinition project, string[] sourceRoots, int line, int column, int absolutePosition)
        {
            if (request == null)
            {
                return;
            }

            request.DocumentPath = session != null ? session.FilePath : string.Empty;
            request.ProjectFilePath = project != null ? project.ProjectFilePath : string.Empty;
            request.WorkspaceRootPath = settings != null ? settings.WorkspaceRootPath : string.Empty;
            request.SourceRoots = sourceRoots ?? new string[0];
            request.DocumentText = session != null ? session.Text : string.Empty;
            request.DocumentVersion = session != null ? session.TextVersion : 0;
            request.Line = line;
            request.Column = column;
            request.AbsolutePosition = absolutePosition;
        }
    }
}
