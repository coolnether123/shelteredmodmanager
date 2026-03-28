using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Cortex.LanguageService.Protocol;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.QuickInfo;
using Microsoft.CodeAnalysis.Text;

namespace Cortex.Roslyn.Worker
{
    internal sealed partial class RoslynLanguageServiceServer
    {
        private const int CompletionCandidateLimit = 512;
        private readonly Dictionary<string, Project> _projectCache = new Dictionary<string, Project>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, MSBuildWorkspace> _workspaceCache = new Dictionary<string, MSBuildWorkspace>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, CachedDocumentContext> _documentContextCache = new Dictionary<string, CachedDocumentContext>(StringComparer.OrdinalIgnoreCase);

        private LanguageServiceAnalysisResponse AnalyzeDocument(LanguageServiceDocumentRequest request)
        {
            var documentContext = ResolveDocument(
                request.DocumentPath,
                request.ProjectFilePath,
                request.WorkspaceRootPath,
                request.SourceRoots,
                request.DocumentText,
                request.DocumentVersion);

            if (documentContext.Document == null)
            {
                return new LanguageServiceAnalysisResponse
                {
                    Success = false,
                    StatusMessage = "Roslyn could not resolve a document for analysis.",
                    DocumentPath = string.Empty,
                    ProjectFilePath = request.ProjectFilePath ?? string.Empty,
                    DocumentVersion = request.DocumentVersion,
                    Diagnostics = new LanguageServiceDiagnostic[0],
                    Classifications = new LanguageServiceClassifiedSpan[0]
                };
            }

            var text = documentContext.Document.GetTextAsync(CurrentCancellationToken).GetAwaiter().GetResult();
            return new LanguageServiceAnalysisResponse
            {
                Success = true,
                StatusMessage = "Roslyn analysis completed.",
                DocumentPath = documentContext.Document.FilePath ?? request.DocumentPath ?? string.Empty,
                ProjectFilePath = documentContext.ProjectPath ?? string.Empty,
                DocumentVersion = request.DocumentVersion,
                Diagnostics = request.IncludeDiagnostics
                    ? CollectDiagnostics(documentContext.Document)
                    : new LanguageServiceDiagnostic[0],
                Classifications = request.IncludeClassifications
                    ? CollectClassifications(documentContext.Document, text, request.ClassificationRangeStart, request.ClassificationRangeLength)
                    : new LanguageServiceClassifiedSpan[0]
            };
        }

        private LanguageServiceHoverResponse GetHover(LanguageServiceHoverRequest request)
        {
            var documentContext = ResolveDocument(
                request.DocumentPath,
                request.ProjectFilePath,
                request.WorkspaceRootPath,
                request.SourceRoots,
                request.DocumentText,
                request.DocumentVersion);

            if (documentContext.Document == null)
            {
                return new LanguageServiceHoverResponse
                {
                    Success = false,
                    StatusMessage = "Roslyn could not resolve a document for hover info.",
                    DocumentPath = request.DocumentPath ?? string.Empty,
                    ProjectFilePath = request.ProjectFilePath ?? string.Empty,
                    DocumentVersion = request.DocumentVersion,
                    SymbolDisplay = string.Empty,
                    SymbolKind = string.Empty,
                    DocumentationXml = string.Empty,
                    DocumentationText = string.Empty,
                    DisplayParts = new LanguageServiceHoverDisplayPart[0]
                };
            }

            var text = documentContext.Document.GetTextAsync(CurrentCancellationToken).GetAwaiter().GetResult();
            var position = ResolveRequestPosition(text, request.Line, request.Column, request.AbsolutePosition);
            if (position < 0 || position > text.Length)
            {
                return new LanguageServiceHoverResponse
                {
                    Success = false,
                    StatusMessage = "Hover position is outside the document.",
                    DocumentPath = documentContext.Document.FilePath ?? request.DocumentPath ?? string.Empty,
                    ProjectFilePath = documentContext.ProjectPath ?? string.Empty,
                    DocumentVersion = request.DocumentVersion,
                    SymbolDisplay = string.Empty,
                    SymbolKind = string.Empty,
                    DocumentationXml = string.Empty,
                    DocumentationText = string.Empty,
                    DisplayParts = new LanguageServiceHoverDisplayPart[0]
                };
            }

            var symbol = ResolveSymbol(documentContext.Document, position);
            return BuildHoverResponse(documentContext, request, text, position, symbol);
        }

        private LanguageServiceDefinitionResponse GoToDefinition(LanguageServiceDefinitionRequest request)
        {
            var documentContext = ResolveDocument(
                request.DocumentPath,
                request.ProjectFilePath,
                request.WorkspaceRootPath,
                request.SourceRoots,
                request.DocumentText,
                request.DocumentVersion);

            if (documentContext.Document == null)
            {
                return new LanguageServiceDefinitionResponse
                {
                    Success = false,
                    StatusMessage = "Roslyn could not resolve a document for definition lookup.",
                    DocumentPath = request.DocumentPath ?? string.Empty,
                    ProjectFilePath = request.ProjectFilePath ?? string.Empty,
                    DocumentVersion = request.DocumentVersion,
                    SymbolDisplay = string.Empty,
                    Range = new LanguageServiceRange()
                };
            }

            var text = documentContext.Document.GetTextAsync(CurrentCancellationToken).GetAwaiter().GetResult();
            var position = ResolveRequestPosition(text, request.Line, request.Column, request.AbsolutePosition);
            if (position < 0 || position > text.Length)
            {
                return new LanguageServiceDefinitionResponse
                {
                    Success = false,
                    StatusMessage = "Definition lookup position is outside the document.",
                    DocumentPath = request.DocumentPath ?? string.Empty,
                    ProjectFilePath = request.ProjectFilePath ?? string.Empty,
                    DocumentVersion = request.DocumentVersion,
                    SymbolDisplay = string.Empty,
                    Range = new LanguageServiceRange()
                };
            }

            var symbol = ResolveSymbol(documentContext.Document, position);
            if (symbol == null)
            {
                return new LanguageServiceDefinitionResponse
                {
                    Success = false,
                    StatusMessage = "No Roslyn symbol was found at that position.",
                    DocumentPath = request.DocumentPath ?? string.Empty,
                    ProjectFilePath = request.ProjectFilePath ?? string.Empty,
                    DocumentVersion = request.DocumentVersion,
                    SymbolDisplay = string.Empty,
                    Range = new LanguageServiceRange()
                };
            }

            var sourceLocation = symbol.Locations.FirstOrDefault(location => location.IsInSource);
            if (sourceLocation == null)
            {
                return new LanguageServiceDefinitionResponse
                {
                    Success = true,
                    StatusMessage = "Metadata definition resolved.",
                    DocumentPath = string.Empty,
                    ProjectFilePath = request.ProjectFilePath ?? string.Empty,
                    DocumentVersion = request.DocumentVersion,
                    SymbolDisplay = symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                    SymbolKind = symbol.Kind.ToString(),
                    MetadataName = symbol.MetadataName ?? string.Empty,
                    ContainingTypeName = GetContainingTypeName(symbol),
                    ContainingAssemblyName = symbol.ContainingAssembly != null ? symbol.ContainingAssembly.Identity.Name : string.Empty,
                    DocumentationCommentId = symbol.GetDocumentationCommentId() ?? string.Empty,
                    DocumentationXml = symbol.GetDocumentationCommentXml() ?? string.Empty,
                    DocumentationText = FlattenDocumentation(symbol.GetDocumentationCommentXml()),
                    Range = new LanguageServiceRange()
                };
            }

            var definitionTree = sourceLocation.SourceTree;
            var definitionText = definitionTree != null ? definitionTree.GetText() : text;
            var definitionPreviewStartLine = 0;
            var definitionPreviewText = string.Empty;
            if (definitionText != null)
            {
                BuildLocationText(definitionText, sourceLocation.SourceSpan, out _, out definitionPreviewText);
                var startPosition = definitionText.Lines.GetLinePosition(sourceLocation.SourceSpan.Start);
                definitionPreviewStartLine = startPosition.Line + 1;
            }
            return new LanguageServiceDefinitionResponse
            {
                Success = true,
                StatusMessage = "Definition resolved.",
                DocumentPath = sourceLocation.SourceTree != null ? sourceLocation.SourceTree.FilePath ?? string.Empty : request.DocumentPath ?? string.Empty,
                ProjectFilePath = documentContext.ProjectPath ?? string.Empty,
                DocumentVersion = request.DocumentVersion,
                SymbolDisplay = symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                SymbolKind = symbol.Kind.ToString(),
                MetadataName = symbol.MetadataName ?? string.Empty,
                ContainingTypeName = GetContainingTypeName(symbol),
                ContainingAssemblyName = symbol.ContainingAssembly != null ? symbol.ContainingAssembly.Identity.Name : string.Empty,
                DocumentationCommentId = symbol.GetDocumentationCommentId() ?? string.Empty,
                DocumentationXml = symbol.GetDocumentationCommentXml() ?? string.Empty,
                DocumentationText = FlattenDocumentation(symbol.GetDocumentationCommentXml()),
                Range = BuildRange(definitionText, sourceLocation.SourceSpan),
                PreviewText = definitionPreviewText,
                PreviewStartLine = definitionPreviewStartLine
            };
        }

        private LanguageServiceCompletionResponse GetCompletion(LanguageServiceCompletionRequest request)
        {
            var documentContext = ResolveDocument(
                request.DocumentPath,
                request.ProjectFilePath,
                request.WorkspaceRootPath,
                request.SourceRoots,
                request.DocumentText,
                request.DocumentVersion);

            if (documentContext.Document == null)
            {
                return new LanguageServiceCompletionResponse
                {
                    Success = false,
                    StatusMessage = "Roslyn could not resolve a document for completion.",
                    DocumentPath = request.DocumentPath ?? string.Empty,
                    ProjectFilePath = request.ProjectFilePath ?? string.Empty,
                    DocumentVersion = request.DocumentVersion,
                    ReplacementRange = new LanguageServiceRange(),
                    Items = new LanguageServiceCompletionItem[0]
                };
            }

            var text = documentContext.Document.GetTextAsync(CurrentCancellationToken).GetAwaiter().GetResult();
            var position = ResolveRequestPosition(text, request.Line, request.Column, request.AbsolutePosition);
            if (position < 0 || position > text.Length)
            {
                return new LanguageServiceCompletionResponse
                {
                    Success = false,
                    StatusMessage = "Completion position is outside the document.",
                    DocumentPath = documentContext.Document.FilePath ?? request.DocumentPath ?? string.Empty,
                    ProjectFilePath = documentContext.ProjectPath ?? string.Empty,
                    DocumentVersion = request.DocumentVersion,
                    ReplacementRange = new LanguageServiceRange(),
                    Items = new LanguageServiceCompletionItem[0]
                };
            }

            var completionService = CompletionService.GetService(documentContext.Document);
            if (completionService == null)
            {
                return new LanguageServiceCompletionResponse
                {
                    Success = false,
                    StatusMessage = "Roslyn completion service is unavailable for this document.",
                    DocumentPath = documentContext.Document.FilePath ?? request.DocumentPath ?? string.Empty,
                    ProjectFilePath = documentContext.ProjectPath ?? string.Empty,
                    DocumentVersion = request.DocumentVersion,
                    ReplacementRange = new LanguageServiceRange(),
                    Items = new LanguageServiceCompletionItem[0]
                };
            }

            var trigger = request.ExplicitInvocation
                ? CompletionTrigger.Invoke
                : BuildCompletionTrigger(request.TriggerCharacter);
            var completionList = completionService.GetCompletionsAsync(documentContext.Document, position, trigger: trigger, cancellationToken: CurrentCancellationToken).GetAwaiter().GetResult();
            if (completionList == null)
            {
                return new LanguageServiceCompletionResponse
                {
                    Success = true,
                    StatusMessage = "No completion items were available.",
                    DocumentPath = documentContext.Document.FilePath ?? request.DocumentPath ?? string.Empty,
                    ProjectFilePath = documentContext.ProjectPath ?? string.Empty,
                    DocumentVersion = request.DocumentVersion,
                    ReplacementRange = new LanguageServiceRange(),
                    Items = new LanguageServiceCompletionItem[0]
                };
            }

            var items = completionList.ItemsList
                .Take(CompletionCandidateLimit)
                .Select(item => ToProtocolCompletionItem(completionService, documentContext.Document, completionList, item))
                .Where(item => item != null)
                .ToArray();
            return new LanguageServiceCompletionResponse
            {
                Success = true,
                StatusMessage = "Completion resolved.",
                DocumentPath = documentContext.Document.FilePath ?? request.DocumentPath ?? string.Empty,
                ProjectFilePath = documentContext.ProjectPath ?? string.Empty,
                DocumentVersion = request.DocumentVersion,
                ReplacementRange = BuildRange(text, completionList.Span),
                Items = items
            };
        }

        private static string GetContainingTypeName(ISymbol symbol)
        {
            if (symbol == null)
            {
                return string.Empty;
            }

            if (symbol is INamedTypeSymbol namedType)
            {
                return namedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty);
            }

            return symbol.ContainingType != null
                ? symbol.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty)
                : string.Empty;
        }

        private static CompletionTrigger BuildCompletionTrigger(string triggerCharacter)
        {
            if (!string.IsNullOrEmpty(triggerCharacter))
            {
                var trigger = triggerCharacter[0];
                if (trigger == '.' || trigger == ':' || trigger == '#' || trigger == '>')
                {
                    return CompletionTrigger.CreateInsertionTrigger(trigger);
                }
            }

            return CompletionTrigger.Invoke;
        }

        private static LanguageServiceCompletionItem ToProtocolCompletionItem(
            CompletionService completionService,
            Document document,
            CompletionList completionList,
            CompletionItem item)
        {
            if (completionService == null || document == null || completionList == null || item == null)
            {
                return null;
            }

            var change = completionService.GetChangeAsync(document, item, cancellationToken: CurrentCancellationToken).GetAwaiter().GetResult();
            return new LanguageServiceCompletionItem
            {
                DisplayText = item.DisplayText ?? string.Empty,
                InsertText = change.TextChange.NewText ?? item.DisplayText ?? string.Empty,
                FilterText = item.FilterText ?? item.DisplayText ?? string.Empty,
                SortText = item.SortText ?? item.DisplayText ?? string.Empty,
                InlineDescription = string.Empty,
                Kind = item.Tags.Length > 0 ? item.Tags[0] ?? string.Empty : string.Empty,
                IsPreselected = completionList.SuggestionModeItem != null && item == completionList.SuggestionModeItem
            };
        }

        private static string GetQualifiedSymbolDisplay(ISymbol symbol)
        {
            if (symbol == null)
            {
                return string.Empty;
            }

            return symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty);
        }

        private static LanguageServiceHoverDisplayPart[] BuildHoverDisplayParts(ISymbol symbol)
        {
            if (symbol == null)
            {
                return new LanguageServiceHoverDisplayPart[0];
            }

            var parts = symbol.ToDisplayParts(SymbolDisplayFormat.CSharpErrorMessageFormat);
            if (parts.IsDefaultOrEmpty)
            {
                return new LanguageServiceHoverDisplayPart[0];
            }

            var results = new List<LanguageServiceHoverDisplayPart>(parts.Length);
            for (var i = 0; i < parts.Length; i++)
            {
                var displayPart = parts[i];
                var part = new LanguageServiceHoverDisplayPart
                {
                    Text = displayPart.ToString(),
                    Classification = displayPart.Kind.ToString(),
                    IsInteractive = displayPart.Symbol != null
                };

                if (displayPart.Symbol != null)
                {
                    PopulateHoverDisplayPart(part, displayPart.Symbol);
                }

                results.Add(part);
            }

            return results.ToArray();
        }

        private static void PopulateHoverDisplayPart(LanguageServiceHoverDisplayPart part, ISymbol symbol)
        {
            if (part == null || symbol == null)
            {
                return;
            }

            var definitionLocation = symbol.Locations.FirstOrDefault(location => location.IsInSource);
            var definitionText = definitionLocation != null && definitionLocation.SourceTree != null
                ? definitionLocation.SourceTree.GetText()
                : null;
            var documentationXml = symbol.GetDocumentationCommentXml();

            part.SymbolDisplay = symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
            part.SymbolKind = symbol.Kind.ToString();
            part.MetadataName = symbol.MetadataName ?? string.Empty;
            part.ContainingTypeName = GetContainingTypeName(symbol);
            part.ContainingAssemblyName = symbol.ContainingAssembly != null ? symbol.ContainingAssembly.Identity.Name : string.Empty;
            part.DocumentationCommentId = symbol.GetDocumentationCommentId() ?? string.Empty;
            part.DocumentationXml = documentationXml ?? string.Empty;
            part.DocumentationText = FlattenDocumentation(documentationXml);
            part.SupplementalSections = BuildSymbolSupplementalSections(symbol);
            part.DefinitionDocumentPath = definitionLocation != null && definitionLocation.SourceTree != null
                ? definitionLocation.SourceTree.FilePath ?? string.Empty
                : string.Empty;
            part.DefinitionRange = BuildRange(definitionText, definitionLocation != null ? definitionLocation.SourceSpan : default(TextSpan));
        }

        private DocumentContext ResolveDocument(string documentPath, string projectPath, string workspaceRootPath, string[] sourceRoots, string documentText, int documentVersion)
        {
            var effectivePath = NormalizePath(documentPath);
            var cacheKey = BuildDocumentCacheKey(effectivePath, documentVersion);
            if (!string.IsNullOrEmpty(cacheKey))
            {
                CachedDocumentContext cachedContext;
                if (_documentContextCache.TryGetValue(cacheKey, out cachedContext) && cachedContext != null && cachedContext.Context != null)
                {
                    return cachedContext.Context;
                }
            }

            var project = ResolveProject(effectivePath, projectPath, workspaceRootPath, sourceRoots);
            Document document = null;
            if (project != null)
            {
                document = FindDocument(project, effectivePath);
            }

            if (document == null)
            {
                document = CreateAdhocDocument(effectivePath, documentText);
            }
            else if (documentText != null)
            {
                var solution = document.Project.Solution.WithDocumentText(document.Id, SourceText.From(documentText, Encoding.UTF8));
                document = solution.GetDocument(document.Id);
            }

            var context = new DocumentContext(document, project != null ? project.FilePath : NormalizePath(projectPath));
            if (!string.IsNullOrEmpty(cacheKey))
            {
                _documentContextCache[cacheKey] = new CachedDocumentContext(context);
            }

            return context;
        }

        private Project ResolveProject(string documentPath, string projectPath, string workspaceRootPath, string[] sourceRoots)
        {
            var explicitProjectPath = NormalizePath(projectPath);
            if (!string.IsNullOrEmpty(explicitProjectPath))
            {
                var loadedProject = LoadProject(explicitProjectPath);
                if (loadedProject != null)
                {
                    return loadedProject;
                }
            }

            foreach (var cached in _projectCache.Values)
            {
                if (FindDocument(cached, documentPath) != null)
                {
                    return cached;
                }
            }

            var candidateProjectPaths = new List<string>();
            AddCandidateProjects(candidateProjectPaths, _projectFilePaths);
            AddCandidateProjects(candidateProjectPaths, FindProjectsUnderRoots(sourceRoots));
            AddCandidateProjects(candidateProjectPaths, FindProjectsUnderRoots(new[] { workspaceRootPath, _workspaceRootPath }));
            AddCandidateProjects(candidateProjectPaths, FindProjectsNearDocument(documentPath));

            for (var i = 0; i < candidateProjectPaths.Count; i++)
            {
                var candidate = candidateProjectPaths[i];
                var loaded = LoadProject(candidate);
                if (loaded != null && (string.IsNullOrEmpty(documentPath) || FindDocument(loaded, documentPath) != null))
                {
                    return loaded;
                }
            }

            return null;
        }

        private void AddCandidateProjects(List<string> candidates, IEnumerable<string> paths)
        {
            if (candidates == null || paths == null)
            {
                return;
            }

            foreach (var path in paths)
            {
                var normalized = NormalizePath(path);
                if (string.IsNullOrEmpty(normalized) || IsBuildArtifact(normalized) || candidates.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                candidates.Add(normalized);
            }
        }

        private IEnumerable<string> FindProjectsUnderRoots(IEnumerable<string> roots)
        {
            var results = new List<string>();
            if (roots == null)
            {
                return results;
            }

            foreach (var root in roots)
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                {
                    continue;
                }

                try
                {
                    results.AddRange(Directory.GetFiles(root, "*.csproj", SearchOption.AllDirectories));
                }
                catch
                {
                }
            }

            return results;
        }

        private IEnumerable<string> FindProjectsNearDocument(string documentPath)
        {
            var results = new List<string>();
            var directory = string.IsNullOrEmpty(documentPath) ? string.Empty : Path.GetDirectoryName(documentPath);
            while (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                try
                {
                    results.AddRange(Directory.GetFiles(directory, "*.csproj", SearchOption.TopDirectoryOnly));
                }
                catch
                {
                }

                directory = Path.GetDirectoryName(directory);
            }

            return results;
        }

        private Project LoadProject(string projectPath)
        {
            var normalized = NormalizePath(projectPath);
            if (string.IsNullOrEmpty(normalized) || !File.Exists(normalized))
            {
                return null;
            }

            Project project;
            if (_projectCache.TryGetValue(normalized, out project))
            {
                return project;
            }

            try
            {
                var workspace = MSBuildWorkspace.Create();
                workspace.WorkspaceFailed += delegate(object sender, WorkspaceDiagnosticEventArgs args)
                {
                    Console.Error.WriteLine(args.Diagnostic.Message);
                };
                project = workspace.OpenProjectAsync(normalized, cancellationToken: CurrentCancellationToken).GetAwaiter().GetResult();
                project = AddSupplementalMetadataReferences(project);

                _projectCache[normalized] = project;
                _workspaceCache[normalized] = workspace;
                return project;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Project load failed for " + normalized + ": " + ex.Message);
                return null;
            }
        }

        private void WarmProjectCache(IEnumerable<string> projectPaths)
        {
            if (projectPaths == null)
            {
                return;
            }

            foreach (var projectPath in projectPaths)
            {
                LoadProject(projectPath);
            }
        }

        private static Document FindDocument(Project project, string documentPath)
        {
            if (project == null || string.IsNullOrEmpty(documentPath))
            {
                return null;
            }

            return project.Documents.FirstOrDefault(document =>
                string.Equals(NormalizePath(document.FilePath), documentPath, StringComparison.OrdinalIgnoreCase));
        }

        private Document CreateAdhocDocument(string documentPath, string documentText)
        {
            var host = MefHostServices.Create(MefHostServices.DefaultAssemblies);
            var workspace = new AdhocWorkspace(host);
            var project = workspace.AddProject(ProjectInfo.Create(
                ProjectId.CreateNewId(),
                VersionStamp.Create(),
                "AdhocProject",
                "AdhocProject",
                LanguageNames.CSharp,
                parseOptions: new CSharpParseOptions(LanguageVersion.Latest)));
            project = AddMetadataReferenceIfPossible(project, typeof(object).Assembly.Location);
            project = AddMetadataReferenceIfPossible(project, typeof(Enumerable).Assembly.Location);
            project = AddMetadataReferenceIfPossible(project, typeof(Uri).Assembly.Location);
            project = AddSupplementalMetadataReferences(project);
            var text = SourceText.From(documentText ?? ReadAllTextSafe(documentPath), Encoding.UTF8);
            var documentId = DocumentId.CreateNewId(project.Id);
            var documentInfo = DocumentInfo.Create(
                documentId,
                Path.GetFileName(documentPath ?? "Document.cs"),
                loader: TextLoader.From(TextAndVersion.Create(text, VersionStamp.Create(), documentPath ?? "Document.cs")),
                filePath: documentPath ?? "Document.cs");
            return workspace.AddDocument(documentInfo);
        }

        private Project AddSupplementalMetadataReferences(Project project)
        {
            if (project == null || _referenceAssemblyPaths == null || _referenceAssemblyPaths.Length == 0)
            {
                return project;
            }

            var existingPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var reference in project.MetadataReferences)
            {
                var portableReference = reference as PortableExecutableReference;
                if (portableReference == null || string.IsNullOrEmpty(portableReference.FilePath))
                {
                    continue;
                }

                existingPaths.Add(NormalizePath(portableReference.FilePath));
            }

            var addedCount = 0;
            var solution = project.Solution;
            for (var i = 0; i < _referenceAssemblyPaths.Length; i++)
            {
                var referencePath = NormalizePath(_referenceAssemblyPaths[i]);
                if (string.IsNullOrEmpty(referencePath) || existingPaths.Contains(referencePath))
                {
                    continue;
                }

                var reference = GetMetadataReference(referencePath);
                if (reference == null)
                {
                    continue;
                }

                solution = solution.AddMetadataReference(project.Id, reference);
                existingPaths.Add(referencePath);
                addedCount++;
            }

            if (addedCount > 0)
            {
                Console.Error.WriteLine("Applied " + addedCount + " supplemental metadata references to " +
                    (project.FilePath ?? project.Name ?? "project") + ".");
            }

            return solution.GetProject(project.Id) ?? project;
        }

        private Project AddMetadataReferenceIfPossible(Project project, string referencePath)
        {
            if (project == null)
            {
                return null;
            }

            var reference = GetMetadataReference(referencePath);
            return reference != null ? project.AddMetadataReference(reference) : project;
        }

        private PortableExecutableReference GetMetadataReference(string referencePath)
        {
            var normalized = NormalizePath(referencePath);
            if (string.IsNullOrEmpty(normalized) || !File.Exists(normalized))
            {
                return null;
            }

            PortableExecutableReference cached;
            if (_metadataReferenceCache.TryGetValue(normalized, out cached))
            {
                return cached;
            }

            try
            {
                cached = MetadataReference.CreateFromFile(normalized);
                _metadataReferenceCache[normalized] = cached;
                return cached;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Skipping metadata reference '" + normalized + "': " + ex.Message);
                return null;
            }
        }

        private static LanguageServiceDiagnostic[] CollectDiagnostics(Document document)
        {
            var syntaxTree = document.GetSyntaxTreeAsync(CurrentCancellationToken).GetAwaiter().GetResult();
            if (syntaxTree == null)
            {
                return new LanguageServiceDiagnostic[0];
            }

            var diagnostics = new List<Diagnostic>();
            diagnostics.AddRange(syntaxTree.GetDiagnostics());

            var compilation = document.Project.GetCompilationAsync(CurrentCancellationToken).GetAwaiter().GetResult();
            if (compilation != null)
            {
                diagnostics.AddRange(compilation.GetDiagnostics().Where(diagnostic =>
                    diagnostic.Location.IsInSource &&
                    diagnostic.Location.SourceTree == syntaxTree));
            }

            return diagnostics
                .GroupBy(diagnostic => diagnostic.Id + "|" + diagnostic.Location.SourceSpan.Start + "|" + diagnostic.GetMessage())
                .Select(group => ToProtocolDiagnostic(group.First()))
                .ToArray();
        }

        private static LanguageServiceClassifiedSpan[] CollectClassifications(Document document, SourceText text, int rangeStart, int rangeLength)
        {
            var classificationSpan = BuildClassificationSpan(text, rangeStart, rangeLength);
            var spans = Classifier.GetClassifiedSpansAsync(document, classificationSpan, CurrentCancellationToken).GetAwaiter().GetResult();
            var classificationService = new RoslynSemanticTokenClassificationService();
            var merged = new Dictionary<string, LanguageServiceClassifiedSpan>(StringComparer.Ordinal);
            foreach (var span in spans)
            {
                var classifiedSpan = classificationService.CreateClassifiedSpan(
                    text,
                    span.ClassificationType,
                    span.TextSpan,
                    ResolveSymbol(document, span.TextSpan.Start));
                var key = classifiedSpan.Start + ":" + classifiedSpan.Length;
                LanguageServiceClassifiedSpan existing;
                if (!merged.TryGetValue(key, out existing))
                {
                    merged[key] = classifiedSpan;
                    continue;
                }

                merged[key] = classificationService.ChoosePreferredClassification(existing, classifiedSpan);
            }

            return merged.Values
                .OrderBy(span => span.Start)
                .ThenByDescending(span => span.Length)
                .ToArray();
        }

        private static TextSpan BuildClassificationSpan(SourceText text, int rangeStart, int rangeLength)
        {
            if (text == null || text.Length <= 0)
            {
                return new TextSpan(0, 0);
            }

            if (rangeStart < 0)
            {
                return new TextSpan(0, text.Length);
            }

            var start = Math.Max(0, Math.Min(rangeStart, text.Length));
            var length = Math.Max(0, Math.Min(rangeLength, text.Length - start));
            return new TextSpan(start, length);
        }

        private static LanguageServiceDiagnostic ToProtocolDiagnostic(Diagnostic diagnostic)
        {
            var lineSpan = diagnostic.Location.GetLineSpan();
            var start = lineSpan.StartLinePosition;
            var end = lineSpan.EndLinePosition;
            return new LanguageServiceDiagnostic
            {
                Id = diagnostic.Id,
                Severity = diagnostic.Severity.ToString(),
                Message = diagnostic.GetMessage(),
                Category = diagnostic.Descriptor.Category ?? string.Empty,
                FilePath = lineSpan.Path ?? string.Empty,
                Line = start.Line + 1,
                Column = start.Character + 1,
                EndLine = end.Line + 1,
                EndColumn = end.Character + 1
            };
        }

        private static ISymbol ResolveSymbol(Document document, int position)
        {
            var text = document != null ? document.GetTextAsync(CurrentCancellationToken).GetAwaiter().GetResult() : null;
            if (document == null || text == null)
            {
                return null;
            }

            var candidatePositions = BuildSymbolCandidatePositions(text, position);
            for (var i = 0; i < candidatePositions.Count; i++)
            {
                var symbol = TryResolveSymbolAtPosition(document, candidatePositions[i]);
                if (symbol != null)
                {
                    return symbol;
                }
            }

            return null;
        }

        private static ISymbol TryResolveSymbolAtPosition(Document document, int position)
        {
            var semanticModel = document.GetSemanticModelAsync(CurrentCancellationToken).GetAwaiter().GetResult();
            var root = document.GetSyntaxRootAsync(CurrentCancellationToken).GetAwaiter().GetResult();
            if (semanticModel == null || root == null)
            {
                return null;
            }

            if (IsCommentTriviaPosition(root, position))
            {
                return null;
            }

            var token = root.FindToken(position, true);
            if (token.Parent == null || token.RawKind == 0 || !token.Span.Contains(position))
            {
                return null;
            }

            var symbol = SymbolFinder.FindSymbolAtPositionAsync(document, position, cancellationToken: CurrentCancellationToken).GetAwaiter().GetResult();
            if (symbol != null)
            {
                return symbol;
            }

            var symbolFromAncestors = TryResolveSymbolFromAncestors(semanticModel, token, position);
            if (symbolFromAncestors != null)
            {
                return symbolFromAncestors;
            }

            return null;
        }

        private static ISymbol TryResolveSymbolFromAncestors(SemanticModel semanticModel, SyntaxToken token, int position)
        {
            for (var node = token.Parent; node != null; node = node.Parent)
            {
                ISymbol symbol;
                if (TryResolveNodeSymbol(semanticModel, node, position, out symbol))
                {
                    return symbol;
                }
            }

            return null;
        }

        private static List<int> BuildSymbolCandidatePositions(SourceText text, int position)
        {
            var candidates = new List<int>();
            AddCandidatePosition(candidates, text, position);
            AddCandidatePosition(candidates, text, position - 1);
            AddCandidatePosition(candidates, text, position + 1);

            var identifierSpan = FindIdentifierSpanAt(text, position);
            if (identifierSpan.Length > 0)
            {
                AddCandidatePosition(candidates, text, identifierSpan.Start);
                AddCandidatePosition(candidates, text, identifierSpan.End - 1);
                AddCandidatePosition(candidates, text, identifierSpan.Start + (identifierSpan.Length / 2));
            }

            return candidates;
        }

        private static void AddCandidatePosition(List<int> candidates, SourceText text, int position)
        {
            if (candidates == null || text == null || position < 0 || position >= text.Length || candidates.Contains(position))
            {
                return;
            }

            candidates.Add(position);
        }

        private static TextSpan FindIdentifierSpanAt(SourceText text, int position)
        {
            if (text == null || text.Length == 0)
            {
                return default(TextSpan);
            }

            var pivot = Math.Max(0, Math.Min(position, text.Length - 1));
            if (!IsIdentifierCharacter(text[pivot]) && pivot > 0 && IsIdentifierCharacter(text[pivot - 1]))
            {
                pivot--;
            }

            if (!IsIdentifierCharacter(text[pivot]))
            {
                return default(TextSpan);
            }

            var start = pivot;
            while (start > 0 && IsIdentifierCharacter(text[start - 1]))
            {
                start--;
            }

            var end = pivot + 1;
            while (end < text.Length && IsIdentifierCharacter(text[end]))
            {
                end++;
            }

            return TextSpan.FromBounds(start, end);
        }

        private static bool IsIdentifierCharacter(char value)
        {
            return char.IsLetterOrDigit(value) || value == '_' || value == '@';
        }

        private static int ResolveRequestPosition(SourceText text, int line, int column, int absolutePosition)
        {
            if (text == null)
            {
                return -1;
            }

            if (absolutePosition >= 0)
            {
                return Math.Min(absolutePosition, text.Length);
            }

            return ToAbsolutePosition(text, line, column);
        }

        private static bool IsCommentTriviaPosition(SyntaxNode root, int position)
        {
            if (root == null || position < 0 || position > root.FullSpan.End)
            {
                return false;
            }

            var trivia = root.FindTrivia(position, true);
            if (trivia.RawKind == 0 || !trivia.FullSpan.Contains(position))
            {
                return false;
            }

            switch ((SyntaxKind)trivia.RawKind)
            {
                case SyntaxKind.SingleLineCommentTrivia:
                case SyntaxKind.MultiLineCommentTrivia:
                case SyntaxKind.SingleLineDocumentationCommentTrivia:
                case SyntaxKind.MultiLineDocumentationCommentTrivia:
                case SyntaxKind.DocumentationCommentExteriorTrivia:
                case SyntaxKind.DisabledTextTrivia:
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryResolveNodeSymbol(SemanticModel semanticModel, SyntaxNode node, int position, out ISymbol symbol)
        {
            symbol = null;
            if (semanticModel == null || node == null || !node.Span.Contains(position))
            {
                return false;
            }

            if (node is IdentifierNameSyntax ||
                node is GenericNameSyntax ||
                node is QualifiedNameSyntax ||
                node is AliasQualifiedNameSyntax ||
                node is MemberAccessExpressionSyntax ||
                node is InvocationExpressionSyntax)
            {
                symbol = semanticModel.GetSymbolInfo(node).Symbol;
                return symbol != null;
            }

            if (node is VariableDeclaratorSyntax variable && variable.Identifier.Span.Contains(position))
            {
                symbol = semanticModel.GetDeclaredSymbol(variable);
                return symbol != null;
            }

            if (node is ParameterSyntax parameter && parameter.Identifier.Span.Contains(position))
            {
                symbol = semanticModel.GetDeclaredSymbol(parameter);
                return symbol != null;
            }

            if (node is TypeParameterSyntax typeParameter && typeParameter.Identifier.Span.Contains(position))
            {
                symbol = semanticModel.GetDeclaredSymbol(typeParameter);
                return symbol != null;
            }

            if (node is MethodDeclarationSyntax method && method.Identifier.Span.Contains(position))
            {
                symbol = semanticModel.GetDeclaredSymbol(method);
                return symbol != null;
            }

            if (node is ConstructorDeclarationSyntax constructor && constructor.Identifier.Span.Contains(position))
            {
                symbol = semanticModel.GetDeclaredSymbol(constructor);
                return symbol != null;
            }

            if (node is PropertyDeclarationSyntax property && property.Identifier.Span.Contains(position))
            {
                symbol = semanticModel.GetDeclaredSymbol(property);
                return symbol != null;
            }

            if (node is EventDeclarationSyntax eventDeclaration && eventDeclaration.Identifier.Span.Contains(position))
            {
                symbol = semanticModel.GetDeclaredSymbol(eventDeclaration);
                return symbol != null;
            }

            if (node is DelegateDeclarationSyntax delegateDeclaration && delegateDeclaration.Identifier.Span.Contains(position))
            {
                symbol = semanticModel.GetDeclaredSymbol(delegateDeclaration);
                return symbol != null;
            }

            if (node is EnumMemberDeclarationSyntax enumMember && enumMember.Identifier.Span.Contains(position))
            {
                symbol = semanticModel.GetDeclaredSymbol(enumMember);
                return symbol != null;
            }

            if (node is EnumDeclarationSyntax enumDeclaration && enumDeclaration.Identifier.Span.Contains(position))
            {
                symbol = semanticModel.GetDeclaredSymbol(enumDeclaration);
                return symbol != null;
            }

            if (node is BaseTypeDeclarationSyntax typeDeclaration && typeDeclaration.Identifier.Span.Contains(position))
            {
                symbol = semanticModel.GetDeclaredSymbol(typeDeclaration);
                return symbol != null;
            }

            if (node is NamespaceDeclarationSyntax namespaceDeclaration &&
                namespaceDeclaration.Name != null &&
                namespaceDeclaration.Name.Span.Contains(position))
            {
                symbol = semanticModel.GetSymbolInfo(namespaceDeclaration.Name).Symbol;
                return symbol != null;
            }

            if (node is FileScopedNamespaceDeclarationSyntax fileScopedNamespaceDeclaration &&
                fileScopedNamespaceDeclaration.Name != null &&
                fileScopedNamespaceDeclaration.Name.Span.Contains(position))
            {
                symbol = semanticModel.GetSymbolInfo(fileScopedNamespaceDeclaration.Name).Symbol;
                return symbol != null;
            }

            if (node is UsingDirectiveSyntax usingDirective &&
                usingDirective.Name != null &&
                usingDirective.Name.Span.Contains(position))
            {
                symbol = semanticModel.GetSymbolInfo(usingDirective.Name).Symbol;
                return symbol != null;
            }

            return false;
        }

        private static LanguageServiceRange BuildRange(SourceText text, TextSpan span)
        {
            if (text == null || span == default(TextSpan))
            {
                return new LanguageServiceRange();
            }

            var start = text.Lines.GetLinePosition(span.Start);
            var end = text.Lines.GetLinePosition(span.End);
            return new LanguageServiceRange
            {
                StartLine = start.Line + 1,
                StartColumn = start.Character + 1,
                EndLine = end.Line + 1,
                EndColumn = end.Character + 1,
                Start = span.Start,
                Length = span.Length
            };
        }

        private static int ToAbsolutePosition(SourceText text, int line, int column)
        {
            if (text == null || line <= 0)
            {
                return -1;
            }

            var lineIndex = line - 1;
            if (lineIndex < 0 || lineIndex >= text.Lines.Count)
            {
                return -1;
            }

            var lineSpan = text.Lines[lineIndex];
            var character = Math.Max(0, column - 1);
            return Math.Min(lineSpan.Start + character, lineSpan.End);
        }

        private static string FlattenDocumentation(string documentationXml)
        {
            if (string.IsNullOrEmpty(documentationXml))
            {
                return string.Empty;
            }

            try
            {
                var xml = XElement.Parse("<root>" + documentationXml + "</root>");
                return string.Join(
                    Environment.NewLine,
                    xml.DescendantNodes()
                        .OfType<XText>()
                        .Select(node => node.Value.Trim())
                        .Where(value => !string.IsNullOrEmpty(value))
                        .ToArray());
            }
            catch
            {
                return documentationXml.Trim();
            }
        }

        private static string ReadAllTextSafe(string documentPath)
        {
            try
            {
                return !string.IsNullOrEmpty(documentPath) && File.Exists(documentPath)
                    ? File.ReadAllText(documentPath)
                    : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool IsBuildArtifact(string path)
        {
            return path.IndexOf("\\bin\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("\\obj\\", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFullPath(path.Trim());
            }
            catch
            {
                return path.Trim();
            }
        }

        private static string BuildDocumentCacheKey(string documentPath, int documentVersion)
        {
            if (string.IsNullOrEmpty(documentPath) || documentVersion <= 0)
            {
                return string.Empty;
            }

            return documentPath + "|" + documentVersion;
        }

        private int GetCachedProjectCount()
        {
            return _projectCache.Count;
        }

        private string[] GetLoadedProjectPaths()
        {
            return _projectCache.Keys.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private static void RegisterMsBuild()
        {
            if (MSBuildLocator.IsRegistered)
            {
                return;
            }

            try
            {
                var instances = MSBuildLocator.QueryVisualStudioInstances()
                    .OrderByDescending(instance => instance.Version)
                    .ToArray();
                if (instances.Length > 0)
                {
                    MSBuildLocator.RegisterInstance(instances[0]);
                    return;
                }
            }
            catch
            {
            }

            var candidates = new[]
            {
                BuildMsBuildPath(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Community"),
                BuildMsBuildPath(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Professional"),
                BuildMsBuildPath(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Enterprise"),
                BuildMsBuildPath(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "BuildTools"),
                BuildMsBuildPath(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Community"),
                BuildMsBuildPath(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Professional"),
                BuildMsBuildPath(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Enterprise"),
                BuildMsBuildPath(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "BuildTools")
            };

            for (var i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i];
                if (string.IsNullOrEmpty(candidate) || !Directory.Exists(candidate))
                {
                    continue;
                }

                MSBuildLocator.RegisterMSBuildPath(candidate);
                return;
            }
        }

        private static string BuildMsBuildPath(string programFilesRoot, string edition)
        {
            if (string.IsNullOrEmpty(programFilesRoot) || string.IsNullOrEmpty(edition))
            {
                return string.Empty;
            }

            return Path.Combine(programFilesRoot, "Microsoft Visual Studio", "2022", edition, "MSBuild", "Current", "Bin");
        }

        private sealed class DocumentContext
        {
            public DocumentContext(Document document, string projectPath)
            {
                Document = document;
                ProjectPath = projectPath ?? string.Empty;
            }

            public Document Document;
            public string ProjectPath;
        }

        private sealed class CachedDocumentContext
        {
            public CachedDocumentContext(DocumentContext context)
            {
                Context = context;
            }

            public DocumentContext Context;
        }
    }
}
