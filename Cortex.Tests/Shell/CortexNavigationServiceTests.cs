using System;
using System.IO;
using System.Reflection;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.Modules.Shared;
using Cortex.Services;
using Cortex.Tests.Testing;
using Xunit;

namespace Cortex.Tests.Shell
{
    public sealed class CortexNavigationServiceTests
    {
        private static void NavigationTargetExample()
        {
        }

        [Fact]
        public void OpenDocument_ReusesExistingSession_AndMovesCaretToRequestedLine()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var tempRoot = Path.Combine(Path.GetTempPath(), "CortexNavigationTests", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempRoot);
                var filePath = Path.Combine(tempRoot, "NavigationTarget.cs");
                File.WriteAllText(filePath, "line1\r\nline2\r\nline3\r\nline4");

                try
                {
                    var state = new CortexShellState();
                    var documentService = new FileDocumentService();
                    var editorService = new EditorService();

                    var opened = CortexModuleUtil.OpenDocument(documentService, state, filePath, 1, DocumentKind.SourceCode);
                    Assert.NotNull(opened);

                    editorService.SetCaret(opened, opened.Text.Length, false, false);
                    var moved = CortexModuleUtil.OpenDocument(documentService, state, filePath, 3, DocumentKind.SourceCode);

                    Assert.Same(opened, moved);
                    Assert.Equal(3, moved.HighlightedLine);
                    Assert.True(moved.EditorState.ScrollToCaretPending);

                    var caret = editorService.GetCaretPosition(moved, moved.EditorState.CaretIndex);
                    Assert.Equal(2, caret.Line);
                    Assert.Equal(0, caret.Column);
                }
                finally
                {
                    Directory.Delete(tempRoot, true);
                }
            });
        }

        [Fact]
        public void OpenLanguageSymbolTarget_ForMetadataMethod_OpensDeclaringTypeDecompilerDocument()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var tempRoot = Path.Combine(Path.GetTempPath(), "CortexNavigationTests", Guid.NewGuid().ToString("N"));
                var cacheRoot = Path.Combine(tempRoot, "cortex_cache", "Assembly-CSharp");
                Directory.CreateDirectory(cacheRoot);

                try
                {
                    var state = new CortexShellState();
                    var documentService = new FileDocumentService();
                    var sourceReferenceService = new RecordingSourceReferenceService(cacheRoot, BuildDeclaringTypeSource());
                    var navigationService = new CortexNavigationService(
                        documentService,
                        sourceReferenceService,
                        new TestRuntimeSourceNavigationService());

                    var targetMethod = typeof(CortexNavigationServiceTests).GetMethod("NavigationTargetExample", BindingFlags.Static | BindingFlags.NonPublic);
                    Assert.NotNull(targetMethod);

                    var opened = navigationService.OpenLanguageSymbolTarget(
                        state,
                        "NavigationTargetExample",
                        "Method",
                        targetMethod.Name,
                        typeof(CortexNavigationServiceTests).FullName,
                        typeof(CortexNavigationServiceTests).Assembly.GetName().Name,
                        BuildMethodDocumentationId(targetMethod),
                        string.Empty,
                        null,
                        "Opened definition.",
                        "Open failed.");

                    Assert.True(opened);
                    Assert.NotNull(sourceReferenceService.LastRequest);
                    Assert.Equal(DecompilerEntityKind.Type, sourceReferenceService.LastRequest.EntityKind);
                    Assert.Equal(typeof(CortexNavigationServiceTests).MetadataToken, sourceReferenceService.LastRequest.MetadataToken);
                    Assert.NotNull(state.Documents.ActiveDocument);
                    Assert.Equal(sourceReferenceService.LastResponse.CachePath, state.Documents.ActiveDocument.FilePath);
                    Assert.Equal(9, state.Documents.ActiveDocument.HighlightedLine);
                }
                finally
                {
                    Directory.Delete(tempRoot, true);
                }
            });
        }

        [Fact]
        public void DecompileAndOpen_ForMethodEntity_UsesDeclaringTypeDecompilerDocument()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var tempRoot = Path.Combine(Path.GetTempPath(), "CortexNavigationTests", Guid.NewGuid().ToString("N"));
                var cacheRoot = Path.Combine(tempRoot, "cortex_cache", "Assembly-CSharp");
                Directory.CreateDirectory(cacheRoot);

                try
                {
                    var state = new CortexShellState();
                    var documentService = new FileDocumentService();
                    var sourceReferenceService = new RecordingSourceReferenceService(cacheRoot, BuildDeclaringTypeSource());
                    var navigationService = new CortexNavigationService(
                        documentService,
                        sourceReferenceService,
                        new TestRuntimeSourceNavigationService());

                    var targetMethod = typeof(CortexNavigationServiceTests).GetMethod("NavigationTargetExample", BindingFlags.Static | BindingFlags.NonPublic);
                    Assert.NotNull(targetMethod);

                    var opened = navigationService.DecompileAndOpen(
                        state,
                        targetMethod.Module.Assembly.Location,
                        targetMethod.MetadataToken,
                        DecompilerEntityKind.Method,
                        false,
                        "Opened method.",
                        "Open failed.");

                    Assert.True(opened);
                    Assert.NotNull(sourceReferenceService.LastRequest);
                    Assert.Equal(DecompilerEntityKind.Type, sourceReferenceService.LastRequest.EntityKind);
                    Assert.Equal(typeof(CortexNavigationServiceTests).MetadataToken, sourceReferenceService.LastRequest.MetadataToken);
                    Assert.NotNull(state.Documents.ActiveDocument);
                    Assert.Equal(sourceReferenceService.LastResponse.CachePath, state.Documents.ActiveDocument.FilePath);
                    Assert.Equal(9, state.Documents.ActiveDocument.HighlightedLine);
                }
                finally
                {
                    Directory.Delete(tempRoot, true);
                }
            });
        }

        [Fact]
        public void OpenLanguageSymbolTarget_PrefersHookedSourceDocument_OverDecompiler()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var tempRoot = Path.Combine(Path.GetTempPath(), "CortexNavigationTests", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempRoot);
                var sourceFilePath = Path.Combine(tempRoot, "CortexNavigationServiceTests.cs");
                File.WriteAllText(sourceFilePath, BuildDeclaringTypeSource());

                try
                {
                    var state = new CortexShellState
                    {
                        SelectedProject = new CortexProjectDefinition
                        {
                            ModId = "TestMod",
                            SourceRootPath = tempRoot,
                            ProjectFilePath = string.Empty
                        },
                        Settings = new CortexSettings()
                    };
                    var documentService = new FileDocumentService();
                    var sourceReferenceService = new RecordingSourceReferenceService(Path.Combine(tempRoot, "cortex_cache"), BuildDeclaringTypeSource());
                    var navigationService = new CortexNavigationService(
                        documentService,
                        sourceReferenceService,
                        new TestRuntimeSourceNavigationService());

                    var targetMethod = typeof(CortexNavigationServiceTests).GetMethod("NavigationTargetExample", BindingFlags.Static | BindingFlags.NonPublic);
                    Assert.NotNull(targetMethod);

                    var opened = navigationService.OpenLanguageSymbolTarget(
                        state,
                        "NavigationTargetExample",
                        "Method",
                        targetMethod.Name,
                        typeof(CortexNavigationServiceTests).FullName,
                        typeof(CortexNavigationServiceTests).Assembly.GetName().Name,
                        BuildMethodDocumentationId(targetMethod),
                        string.Empty,
                        null,
                        "Opened definition.",
                        "Open failed.");

                    Assert.True(opened);
                    Assert.Null(sourceReferenceService.LastRequest);
                    Assert.NotNull(state.Documents.ActiveDocument);
                    Assert.Equal(sourceFilePath, state.Documents.ActiveDocument.FilePath);
                    Assert.Equal(9, state.Documents.ActiveDocument.HighlightedLine);
                }
                finally
                {
                    Directory.Delete(tempRoot, true);
                }
            });
        }

        [Fact]
        public void OpenLanguageSymbolTarget_IgnoresDecompilerDefinitionPath_WhenHookedSourceExists()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var tempRoot = Path.Combine(Path.GetTempPath(), "CortexNavigationTests", Guid.NewGuid().ToString("N"));
                var cacheRoot = Path.Combine(tempRoot, "cortex_cache", "Assembly-CSharp");
                Directory.CreateDirectory(tempRoot);
                Directory.CreateDirectory(cacheRoot);
                var sourceFilePath = Path.Combine(tempRoot, "CortexNavigationServiceTests.cs");
                var decompiledFilePath = Path.Combine(cacheRoot, "CortexNavigationServiceTests.NavigationTargetExample_0x00000001.cs");
                File.WriteAllText(sourceFilePath, BuildDeclaringTypeSource());
                File.WriteAllText(decompiledFilePath, "// decompiled placeholder");

                try
                {
                    var state = new CortexShellState
                    {
                        SelectedProject = new CortexProjectDefinition
                        {
                            ModId = "TestMod",
                            SourceRootPath = tempRoot,
                            ProjectFilePath = string.Empty
                        },
                        Settings = new CortexSettings()
                    };
                    var documentService = new FileDocumentService();
                    var sourceReferenceService = new RecordingSourceReferenceService(cacheRoot, BuildDeclaringTypeSource());
                    var navigationService = new CortexNavigationService(
                        documentService,
                        sourceReferenceService,
                        new TestRuntimeSourceNavigationService());

                    var targetMethod = typeof(CortexNavigationServiceTests).GetMethod("NavigationTargetExample", BindingFlags.Static | BindingFlags.NonPublic);
                    Assert.NotNull(targetMethod);

                    var opened = navigationService.OpenLanguageSymbolTarget(
                        state,
                        "NavigationTargetExample",
                        "Method",
                        targetMethod.Name,
                        typeof(CortexNavigationServiceTests).FullName,
                        typeof(CortexNavigationServiceTests).Assembly.GetName().Name,
                        BuildMethodDocumentationId(targetMethod),
                        decompiledFilePath,
                        null,
                        "Opened definition.",
                        "Open failed.");

                    Assert.True(opened);
                    Assert.Null(sourceReferenceService.LastRequest);
                    Assert.NotNull(state.Documents.ActiveDocument);
                    Assert.Equal(sourceFilePath, state.Documents.ActiveDocument.FilePath);
                    Assert.Equal(9, state.Documents.ActiveDocument.HighlightedLine);
                }
                finally
                {
                    Directory.Delete(tempRoot, true);
                }
            });
        }

        [Fact]
        public void OpenLanguageSymbolTarget_RecomputesLine_WhenRemappingDecompilerDefinitionToHookedSource()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var tempRoot = Path.Combine(Path.GetTempPath(), "CortexNavigationTests", Guid.NewGuid().ToString("N"));
                var cacheRoot = Path.Combine(tempRoot, "cortex_cache", "Assembly-CSharp");
                Directory.CreateDirectory(tempRoot);
                Directory.CreateDirectory(cacheRoot);
                var sourceFilePath = Path.Combine(tempRoot, "CortexNavigationServiceTests.cs");
                var decompiledFilePath = Path.Combine(cacheRoot, "CortexNavigationServiceTests.NavigationTargetExample_0x00000001.cs");
                File.WriteAllText(sourceFilePath, BuildDeclaringTypeSource());
                File.WriteAllText(decompiledFilePath, "// decompiled placeholder");

                try
                {
                    var state = new CortexShellState
                    {
                        SelectedProject = new CortexProjectDefinition
                        {
                            ModId = "TestMod",
                            SourceRootPath = tempRoot,
                            ProjectFilePath = string.Empty
                        },
                        Settings = new CortexSettings()
                    };
                    var documentService = new FileDocumentService();
                    var sourceReferenceService = new RecordingSourceReferenceService(cacheRoot, BuildDeclaringTypeSource());
                    var navigationService = new CortexNavigationService(
                        documentService,
                        sourceReferenceService,
                        new TestRuntimeSourceNavigationService());

                    var targetMethod = typeof(CortexNavigationServiceTests).GetMethod("NavigationTargetExample", BindingFlags.Static | BindingFlags.NonPublic);
                    Assert.NotNull(targetMethod);

                    var opened = navigationService.OpenLanguageSymbolTarget(
                        state,
                        "NavigationTargetExample",
                        "Method",
                        targetMethod.Name,
                        typeof(CortexNavigationServiceTests).FullName,
                        typeof(CortexNavigationServiceTests).Assembly.GetName().Name,
                        BuildMethodDocumentationId(targetMethod),
                        decompiledFilePath,
                        new Cortex.LanguageService.Protocol.LanguageServiceRange
                        {
                            StartLine = 42
                        },
                        "Opened definition.",
                        "Open failed.");

                    Assert.True(opened);
                    Assert.NotNull(state.Documents.ActiveDocument);
                    Assert.Equal(sourceFilePath, state.Documents.ActiveDocument.FilePath);
                    Assert.Equal(9, state.Documents.ActiveDocument.HighlightedLine);
                }
                finally
                {
                    Directory.Delete(tempRoot, true);
                }
            });
        }

        [Fact]
        public void DecompileAndOpen_ForTypeEntity_PrefersHookedSourceDocument_OverDecompiler()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var tempRoot = Path.Combine(Path.GetTempPath(), "CortexNavigationTests", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempRoot);
                var sourceFilePath = Path.Combine(tempRoot, "CortexNavigationServiceTests.cs");
                File.WriteAllText(sourceFilePath, BuildDeclaringTypeSource());

                try
                {
                    var state = new CortexShellState
                    {
                        SelectedProject = new CortexProjectDefinition
                        {
                            ModId = "TestMod",
                            SourceRootPath = tempRoot,
                            ProjectFilePath = string.Empty
                        },
                        Settings = new CortexSettings()
                    };
                    var documentService = new FileDocumentService();
                    var sourceReferenceService = new RecordingSourceReferenceService(Path.Combine(tempRoot, "cortex_cache"), BuildDeclaringTypeSource());
                    var navigationService = new CortexNavigationService(
                        documentService,
                        sourceReferenceService,
                        new TestRuntimeSourceNavigationService());

                    var opened = navigationService.DecompileAndOpen(
                        state,
                        typeof(CortexNavigationServiceTests).Assembly.Location,
                        typeof(CortexNavigationServiceTests).MetadataToken,
                        DecompilerEntityKind.Type,
                        false,
                        "Opened type.",
                        "Open failed.");

                    Assert.True(opened);
                    Assert.Null(sourceReferenceService.LastRequest);
                    Assert.NotNull(state.Documents.ActiveDocument);
                    Assert.Equal(sourceFilePath, state.Documents.ActiveDocument.FilePath);
                    Assert.Equal(3, state.Documents.ActiveDocument.HighlightedLine);
                }
                finally
                {
                    Directory.Delete(tempRoot, true);
                }
            });
        }

        private static string BuildDeclaringTypeSource()
        {
            return "namespace Cortex.Tests.Shell\r\n" +
                "{\r\n" +
                "    public sealed class CortexNavigationServiceTests\r\n" +
                "    {\r\n" +
                "        private static void OtherMethod()\r\n" +
                "        {\r\n" +
                "        }\r\n" +
                "\r\n" +
                "        private static void NavigationTargetExample()\r\n" +
                "        {\r\n" +
                "        }\r\n" +
                "    }\r\n" +
                "}\r\n";
        }

        private static string BuildMethodDocumentationId(MethodBase method)
        {
            return "M:" + method.DeclaringType.FullName.Replace('+', '.') + "." + method.Name;
        }

        private sealed class RecordingSourceReferenceService : ISourceReferenceService
        {
            private readonly string _cacheRoot;
            private readonly string _sourceText;

            public RecordingSourceReferenceService(string cacheRoot, string sourceText)
            {
                _cacheRoot = cacheRoot;
                _sourceText = sourceText;
            }

            public DecompilerRequest LastRequest { get; private set; }
            public DecompilerResponse LastResponse { get; private set; }

            public DecompilerResponse GetSource(DecompilerRequest request)
            {
                LastRequest = request;

                var fileName = request.EntityKind == DecompilerEntityKind.Type
                    ? "CortexNavigationServiceTests.cs"
                    : "CortexNavigationServiceTests.NavigationTargetExample.cs";
                var cachePath = Path.Combine(_cacheRoot, fileName);
                File.WriteAllText(cachePath, _sourceText);

                LastResponse = new DecompilerResponse
                {
                    CachePath = cachePath,
                    SourceText = _sourceText,
                    StatusMessage = "Generated decompiled source."
                };
                return LastResponse;
            }

            public int MapSourceLineToOffset(string mapText, int sourceLine)
            {
                return -1;
            }

            public int MapOffsetToSourceLine(string mapText, int ilOffset)
            {
                return -1;
            }
        }
    }
}
