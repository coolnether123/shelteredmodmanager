using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Cortex.LanguageService.Protocol;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;

namespace Cortex.Roslyn.Worker
{
    internal sealed partial class RoslynLanguageServiceServer
    {
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

            var text = documentContext.Document.GetTextAsync().Result;
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
                    ? CollectClassifications(documentContext.Document, text)
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
                    DocumentationText = string.Empty
                };
            }

            var text = documentContext.Document.GetTextAsync().Result;
            var position = ToAbsolutePosition(text, request.Line, request.Column);
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
                    DocumentationText = string.Empty
                };
            }

            var symbol = ResolveSymbol(documentContext.Document, position);

            if (symbol == null)
            {
                return new LanguageServiceHoverResponse
                {
                    Success = false,
                    StatusMessage = "No Roslyn symbol was found at that position.",
                    DocumentPath = documentContext.Document.FilePath ?? request.DocumentPath ?? string.Empty,
                    ProjectFilePath = documentContext.ProjectPath ?? string.Empty,
                    DocumentVersion = request.DocumentVersion,
                    SymbolDisplay = string.Empty,
                    SymbolKind = string.Empty,
                    DocumentationXml = string.Empty,
                    DocumentationText = string.Empty
                };
            }

            var syntaxTree = documentContext.Document.GetSyntaxTreeAsync().Result;
            var sourceLocation = symbol.Locations.FirstOrDefault(location =>
                location.IsInSource &&
                location.SourceTree == syntaxTree);
            var span = sourceLocation != null ? sourceLocation.SourceSpan : default(TextSpan);
            var documentationXml = symbol.GetDocumentationCommentXml();

            return new LanguageServiceHoverResponse
            {
                Success = true,
                StatusMessage = "Hover info resolved.",
                DocumentPath = documentContext.Document.FilePath ?? request.DocumentPath ?? string.Empty,
                ProjectFilePath = documentContext.ProjectPath ?? string.Empty,
                DocumentVersion = request.DocumentVersion,
                SymbolDisplay = symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                SymbolKind = symbol.Kind.ToString(),
                MetadataName = symbol.MetadataName ?? string.Empty,
                ContainingTypeName = GetContainingTypeName(symbol),
                ContainingAssemblyName = symbol.ContainingAssembly != null ? symbol.ContainingAssembly.Identity.Name : string.Empty,
                DocumentationCommentId = symbol.GetDocumentationCommentId() ?? string.Empty,
                DocumentationXml = documentationXml ?? string.Empty,
                DocumentationText = FlattenDocumentation(documentationXml),
                Range = BuildRange(text, span)
            };
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

            var text = documentContext.Document.GetTextAsync().Result;
            var position = ToAbsolutePosition(text, request.Line, request.Column);
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
                Range = BuildRange(definitionText, sourceLocation.SourceSpan)
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
                project = workspace.OpenProjectAsync(normalized).Result;

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

        private static Document CreateAdhocDocument(string documentPath, string documentText)
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
            project = project.AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
            project = project.AddMetadataReference(MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location));
            project = project.AddMetadataReference(MetadataReference.CreateFromFile(typeof(Uri).Assembly.Location));
            var text = SourceText.From(documentText ?? ReadAllTextSafe(documentPath), Encoding.UTF8);
            var documentId = DocumentId.CreateNewId(project.Id);
            var documentInfo = DocumentInfo.Create(
                documentId,
                Path.GetFileName(documentPath ?? "Document.cs"),
                loader: TextLoader.From(TextAndVersion.Create(text, VersionStamp.Create(), documentPath ?? "Document.cs")),
                filePath: documentPath ?? "Document.cs");
            return workspace.AddDocument(documentInfo);
        }

        private static LanguageServiceDiagnostic[] CollectDiagnostics(Document document)
        {
            var syntaxTree = document.GetSyntaxTreeAsync().Result;
            if (syntaxTree == null)
            {
                return new LanguageServiceDiagnostic[0];
            }

            var diagnostics = new List<Diagnostic>();
            diagnostics.AddRange(syntaxTree.GetDiagnostics());

            var compilation = document.Project.GetCompilationAsync().Result;
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

        private static LanguageServiceClassifiedSpan[] CollectClassifications(Document document, SourceText text)
        {
            var spans = Classifier.GetClassifiedSpansAsync(document, new TextSpan(0, text.Length)).Result;
            return spans.Select(span =>
            {
                var linePosition = text.Lines.GetLinePosition(span.TextSpan.Start);
                return new LanguageServiceClassifiedSpan
                {
                    Classification = span.ClassificationType ?? string.Empty,
                    Start = span.TextSpan.Start,
                    Length = span.TextSpan.Length,
                    Line = linePosition.Line + 1,
                    Column = linePosition.Character + 1
                };
            }).ToArray();
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
            var symbol = SymbolFinder.FindSymbolAtPositionAsync(document, position).Result;
            if (symbol != null)
            {
                return symbol;
            }

            var semanticModel = document.GetSemanticModelAsync().Result;
            var root = document.GetSyntaxRootAsync().Result;
            if (semanticModel == null || root == null)
            {
                return null;
            }

            var token = root.FindToken(position);
            if (token.Parent == null)
            {
                return null;
            }

            return semanticModel.GetSymbolInfo(token.Parent).Symbol ?? semanticModel.GetDeclaredSymbol(token.Parent);
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
