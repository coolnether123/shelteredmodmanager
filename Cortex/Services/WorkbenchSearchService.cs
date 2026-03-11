using System;
using System.Collections.Generic;
using System.IO;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Modules.Shared;

namespace Cortex.Services
{
    public sealed class WorkbenchSearchService
    {
        private readonly IEditorService _editorService = new Cortex.Core.Services.EditorService();

        public TextSearchResultSet Search(
            TextSearchQuery query,
            CortexShellState state,
            IProjectCatalog projectCatalog,
            ISourceLookupIndex sourceLookupIndex,
            ITextSearchService textSearchService)
        {
            if (textSearchService == null)
            {
                return new TextSearchResultSet
                {
                    Query = CloneQuery(query),
                    GeneratedUtc = DateTime.UtcNow,
                    StatusMessage = "Text search service was not available."
                };
            }

            var documents = CollectScopeDocuments(query, state, projectCatalog, sourceLookupIndex);
            return textSearchService.Search(query, documents);
        }

        public int CountMatches(TextSearchResultSet results)
        {
            return results != null ? results.TotalMatchCount : 0;
        }

        public TextSearchMatch GetMatchAt(TextSearchResultSet results, int flatIndex)
        {
            if (results == null || flatIndex < 0)
            {
                return null;
            }

            var runningIndex = 0;
            for (var i = 0; i < results.Documents.Count; i++)
            {
                var document = results.Documents[i];
                if (document == null)
                {
                    continue;
                }

                for (var j = 0; j < document.Matches.Count; j++)
                {
                    if (runningIndex == flatIndex)
                    {
                        return document.Matches[j];
                    }

                    runningIndex++;
                }
            }

            return null;
        }

        public bool NavigateToMatch(TextSearchMatch match, CortexShellState state, CortexNavigationService navigationService)
        {
            if (match == null || state == null || navigationService == null || string.IsNullOrEmpty(match.DocumentPath))
            {
                return false;
            }

            var session = CortexModuleUtil.FindOpenDocument(state, match.DocumentPath);
            if (session == null)
            {
                session = navigationService.OpenDocument(
                    state,
                    match.DocumentPath,
                    match.LineNumber,
                    string.Empty,
                    "Could not open " + (Path.GetFileName(match.DocumentPath) ?? match.DocumentPath) + ".");
            }

            if (session == null)
            {
                return false;
            }

            _editorService.EnsureDocumentState(session);
            _editorService.SetSelection(session, match.AbsoluteIndex, match.AbsoluteIndex + Math.Max(1, match.Length));
            session.HighlightedLine = match.LineNumber;
            state.Documents.ActiveDocument = session;
            state.Documents.ActiveDocumentPath = session.FilePath;
            state.Workbench.RequestedContainerId = CortexWorkbenchIds.EditorContainer;
            state.StatusMessage = "Match " + match.LineNumber + ":" + match.ColumnNumber + " in " + Path.GetFileName(match.DocumentPath) + ".";
            return true;
        }

        public string BuildFingerprint(TextSearchQuery query)
        {
            return (query != null ? query.SearchText ?? string.Empty : string.Empty) +
                "|" + (query != null ? query.Scope.ToString() : string.Empty) +
                "|" + (query != null && query.MatchCase) +
                "|" + (query != null && query.WholeWord);
        }

        private List<TextSearchDocumentInput> CollectScopeDocuments(
            TextSearchQuery query,
            CortexShellState state,
            IProjectCatalog projectCatalog,
            ISourceLookupIndex sourceLookupIndex)
        {
            var inputs = new List<TextSearchDocumentInput>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var scope = query != null ? query.Scope : SearchScopeKind.CurrentDocument;

            if (state == null)
            {
                return inputs;
            }

            if (scope == SearchScopeKind.CurrentDocument)
            {
                AddOpenDocumentInput(inputs, seen, state.Documents.ActiveDocument);
                return inputs;
            }

            if (scope == SearchScopeKind.AllOpenDocuments)
            {
                for (var i = 0; i < state.Documents.OpenDocuments.Count; i++)
                {
                    AddOpenDocumentInput(inputs, seen, state.Documents.OpenDocuments[i]);
                }

                return inputs;
            }

            var scopeRoots = ResolveScopeRoots(scope, state, projectCatalog);
            for (var rootIndex = 0; rootIndex < scopeRoots.Count; rootIndex++)
            {
                AddTreeDocuments(inputs, seen, state, sourceLookupIndex, scopeRoots[rootIndex]);
            }

            return inputs;
        }

        private static List<string> ResolveScopeRoots(
            SearchScopeKind scope,
            CortexShellState state,
            IProjectCatalog projectCatalog)
        {
            var roots = new List<string>();
            if (scope == SearchScopeKind.CurrentProject)
            {
                AddRoot(roots, state != null && state.SelectedProject != null ? state.SelectedProject.SourceRootPath : string.Empty);
                return roots;
            }

            AddRoot(roots, state != null && state.Settings != null ? state.Settings.WorkspaceRootPath : string.Empty);
            if (roots.Count > 0)
            {
                return roots;
            }

            var projects = projectCatalog != null ? projectCatalog.GetProjects() : null;
            if (projects == null)
            {
                return roots;
            }

            for (var i = 0; i < projects.Count; i++)
            {
                AddRoot(roots, projects[i] != null ? projects[i].SourceRootPath : string.Empty);
            }

            return roots;
        }

        private static void AddRoot(List<string> roots, string root)
        {
            if (roots == null || string.IsNullOrEmpty(root))
            {
                return;
            }

            try
            {
                var fullPath = Path.GetFullPath(root);
                if (Directory.Exists(fullPath) && !ContainsPath(roots, fullPath))
                {
                    roots.Add(fullPath);
                }
            }
            catch
            {
            }
        }

        private static bool ContainsPath(List<string> values, string candidate)
        {
            if (values == null || string.IsNullOrEmpty(candidate))
            {
                return false;
            }

            for (var i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i], candidate, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddOpenDocumentInput(List<TextSearchDocumentInput> inputs, HashSet<string> seen, DocumentSession session)
        {
            if (inputs == null || seen == null || session == null || string.IsNullOrEmpty(session.FilePath))
            {
                return;
            }

            if (!seen.Add(session.FilePath))
            {
                return;
            }

            inputs.Add(new TextSearchDocumentInput
            {
                DocumentPath = session.FilePath,
                DisplayPath = session.FilePath,
                Text = session.Text ?? string.Empty
            });
        }

        private static void AddTreeDocuments(
            List<TextSearchDocumentInput> inputs,
            HashSet<string> seen,
            CortexShellState state,
            ISourceLookupIndex sourceLookupIndex,
            string rootPath)
        {
            if (inputs == null || seen == null || sourceLookupIndex == null || string.IsNullOrEmpty(rootPath))
            {
                return;
            }

            var tree = sourceLookupIndex.BuildTree(rootPath, WorkspaceTreeKind.ProjectSource);
            AddTreeDocumentsRecursive(inputs, seen, state, tree);
        }

        private static void AddTreeDocumentsRecursive(
            List<TextSearchDocumentInput> inputs,
            HashSet<string> seen,
            CortexShellState state,
            WorkspaceTreeNode node)
        {
            if (node == null)
            {
                return;
            }

            if (node.NodeKind == WorkspaceTreeNodeKind.File)
            {
                AddFileInput(inputs, seen, state, node.FullPath);
                return;
            }

            for (var i = 0; i < node.Children.Count; i++)
            {
                AddTreeDocumentsRecursive(inputs, seen, state, node.Children[i]);
            }
        }

        private static void AddFileInput(
            List<TextSearchDocumentInput> inputs,
            HashSet<string> seen,
            CortexShellState state,
            string filePath)
        {
            if (inputs == null || seen == null || string.IsNullOrEmpty(filePath))
            {
                return;
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(filePath);
            }
            catch
            {
                return;
            }

            if (!File.Exists(fullPath) || !seen.Add(fullPath))
            {
                return;
            }

            var openSession = CortexModuleUtil.FindOpenDocument(state, fullPath);
            if (openSession != null)
            {
                inputs.Add(new TextSearchDocumentInput
                {
                    DocumentPath = fullPath,
                    DisplayPath = fullPath,
                    Text = openSession.Text ?? string.Empty
                });
                return;
            }

            try
            {
                inputs.Add(new TextSearchDocumentInput
                {
                    DocumentPath = fullPath,
                    DisplayPath = fullPath,
                    Text = File.ReadAllText(fullPath)
                });
            }
            catch
            {
            }
        }

        private static TextSearchQuery CloneQuery(TextSearchQuery query)
        {
            return query != null
                ? new TextSearchQuery
                {
                    SearchText = query.SearchText ?? string.Empty,
                    Scope = query.Scope,
                    MatchCase = query.MatchCase,
                    WholeWord = query.WholeWord
                }
                : new TextSearchQuery();
        }
    }
}
