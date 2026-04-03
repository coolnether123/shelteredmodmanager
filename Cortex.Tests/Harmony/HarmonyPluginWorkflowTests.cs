using System;
using System.IO;
using System.Reflection;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using Cortex.Plugin.Harmony;
using Cortex.Plugin.Harmony.Services.Generation;
using Cortex.Plugins.Abstractions;
using Cortex.Presentation.Abstractions;
using Cortex.Presentation.Models;
using Cortex.Services.Navigation;
using Cortex.Services.Semantics.Context;
using Cortex.Shell;
using Cortex.Tests.Testing;
using Xunit;

namespace Cortex.Tests.Harmony
{
    public sealed class HarmonyPluginWorkflowTests
    {
        [Fact]
        public void ViewPatches_LoadsPluginOwnedSummaryState()
        {
            EnsureHarmonyAssemblyLoaded();
            var sourcePath = CreateTempSourceFile(BuildHarmonySourcePatchDocument());
            try
            {
                var state = new CortexShellState();
                var runtime = CreateModuleRuntime(state);
                var stateStore = new HarmonyModuleStateStore();
                var controller = new HarmonyWorkflowController(stateStore);
                var method = typeof(DateTime).GetMethod("ToBinary", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);

                string statusMessage;
                var handled = controller.ViewPatches(runtime, CreateSourcePatchTarget(sourcePath), false, out statusMessage);

                Assert.True(handled, statusMessage ?? string.Empty);
                Assert.NotNull(stateStore.GetWorkflow(runtime).ActiveSummary);
                Assert.NotNull(stateStore.GetWorkflow(runtime).ActiveInspectionRequest);
                Assert.Equal(method.MetadataToken, stateStore.GetWorkflow(runtime).ActiveInspectionRequest.MetadataToken);
                Assert.False(string.IsNullOrEmpty(stateStore.GetWorkflow(runtime).ActiveSummaryKey));
                Assert.Contains("System.DateTime.ToBinary", stateStore.GetWorkflow(runtime).ActiveSymbolDisplay);
                Assert.False(string.IsNullOrEmpty(statusMessage));
            }
            finally
            {
                DeleteIfExists(sourcePath);
            }
        }

        [Fact]
        public void PrepareGeneration_PopulatesPluginWorkflowState()
        {
            EnsureHarmonyAssemblyLoaded();
            var sourcePath = CreateTempSourceFile(BuildHarmonySourcePatchDocument());
            try
            {
                var state = new CortexShellState();
                var runtime = CreateModuleRuntime(state);
                var stateStore = new HarmonyModuleStateStore();
                var controller = new HarmonyWorkflowController(stateStore);

                string statusMessage;
                var handled = controller.PrepareGeneration(
                    runtime,
                    CreateSourcePatchTarget(sourcePath),
                    HarmonyPatchGenerationKind.Prefix,
                    out statusMessage);

                var workflow = stateStore.GetWorkflow(runtime);
                Assert.True(handled, statusMessage ?? string.Empty);
                Assert.NotNull(workflow.GenerationRequest);
                Assert.NotNull(workflow.GenerationPreview);
                Assert.True(workflow.IsInsertionSelectionActive);
                Assert.NotEmpty(workflow.InsertionTargets);
                Assert.Equal(sourcePath, workflow.GenerationRequest.DestinationFilePath);
                Assert.Equal(HarmonyPatchGenerationKind.Prefix, workflow.GenerationRequest.GenerationKind);
                Assert.Contains("Preview ready", statusMessage);
            }
            finally
            {
                DeleteIfExists(sourcePath);
            }
        }

        [Fact]
        public void BuildPreview_SelectedContext_InsertsAfterContainingType()
        {
            var sourcePath = CreateTempSourceFile(
@"namespace Demo
{
    internal class Holder
    {
        private void Existing()
        {
        }
    }
}");
            try
            {
                var service = new HarmonyPatchInsertionService(new HarmonyModuleStateStore());
                var request = new HarmonyPatchGenerationRequest
                {
                    DestinationFilePath = sourcePath,
                    InsertionAnchorKind = HarmonyPatchInsertionAnchorKind.SelectedContext,
                    InsertionLine = 5,
                    InsertionAbsolutePosition = File.ReadAllText(sourcePath).IndexOf("Existing", StringComparison.Ordinal)
                };

                var preview = service.BuildPreview(
                    null,
                    request,
                    new HarmonyPatchGenerationPreview
                    {
                        SnippetText = "internal static class GeneratedPatch\r\n{\r\n}\r\n",
                        CanApply = true
                    });

                Assert.True(preview.CanApply);
                Assert.Contains("after class Holder", preview.InsertionContextLabel);
                Assert.Contains("GeneratedPatch", preview.PreviewText);
            }
            finally
            {
                DeleteIfExists(sourcePath);
            }
        }

        [Fact]
        public void ExplorerMatcher_RespectsSelectedProjectRestriction()
        {
            var state = new CortexShellState();
            var runtime = CreateModuleRuntime(state);
            var stateStore = new HarmonyModuleStateStore();
            var controller = new HarmonyWorkflowController(stateStore);
            var workflow = stateStore.GetWorkflow(runtime);
            var assemblyPath = @"C:\Game\Managed\Assembly-CSharp.dll";
            workflow.RefreshRequested = false;
            workflow.SnapshotUtc = DateTime.UtcNow;
            workflow.SnapshotMethods = new[]
            {
                new HarmonyMethodPatchSummary
                {
                    AssemblyPath = assemblyPath,
                    DeclaringType = "Game.UI.BasePanel",
                    MethodName = "Initialise",
                    IsPatched = true,
                    Target = new HarmonyPatchNavigationTarget
                    {
                        AssemblyPath = assemblyPath,
                        MetadataToken = 101,
                        DeclaringTypeName = "Game.UI.BasePanel",
                        MethodName = "Initialise"
                    },
                    Entries = new[]
                    {
                        new HarmonyPatchEntry
                        {
                            OwnerAssociation = new HarmonyPatchOwnerAssociation
                            {
                                ProjectModId = "coolnether123.sheltereddisplayfixes",
                                ProjectSourceRootPath = @"D:\Projects\Sheltered Modding\Sheltered Display Fixes",
                                HasMatch = true
                            }
                        }
                    }
                },
                new HarmonyMethodPatchSummary
                {
                    AssemblyPath = assemblyPath,
                    DeclaringType = "Game.UI.OtherPanel",
                    MethodName = "Initialise",
                    IsPatched = true,
                    Target = new HarmonyPatchNavigationTarget
                    {
                        AssemblyPath = assemblyPath,
                        MetadataToken = 202,
                        DeclaringTypeName = "Game.UI.OtherPanel",
                        MethodName = "Initialise"
                    },
                    Entries = new[]
                    {
                        new HarmonyPatchEntry
                        {
                            OwnerAssociation = new HarmonyPatchOwnerAssociation
                            {
                                ProjectModId = "modapi.core",
                                ProjectSourceRootPath = @"D:\Projects\Other\ModAPI",
                                HasMatch = true
                            }
                        }
                    }
                }
            };

            var matcher = controller.BuildExplorerMatcher(
                runtime,
                new ExplorerFilterRuntimeContext
                {
                    Scope = ExplorerFilterScope.Decompiler,
                    RestrictToSelectedProject = true,
                    SelectedProject = new CortexProjectDefinition
                    {
                        ModId = "coolnether123.sheltereddisplayfixes",
                        SourceRootPath = @"D:\Projects\Sheltered Modding\Sheltered Display Fixes"
                    }
                });

            Assert.NotNull(matcher);
            Assert.True(matcher(new WorkspaceTreeNode
            {
                NodeKind = WorkspaceTreeNodeKind.Type,
                AssemblyPath = assemblyPath,
                TypeName = "Game.UI.BasePanel"
            }));
            Assert.False(matcher(new WorkspaceTreeNode
            {
                NodeKind = WorkspaceTreeNodeKind.Type,
                AssemblyPath = assemblyPath,
                TypeName = "Game.UI.OtherPanel"
            }));
            Assert.False(matcher(new WorkspaceTreeNode
            {
                NodeKind = WorkspaceTreeNodeKind.Member,
                AssemblyPath = assemblyPath,
                TypeName = "Game.UI.OtherPanel",
                MetadataToken = 202,
                Name = "Initialise()"
            }));
        }

        private static IWorkbenchModuleRuntime CreateModuleRuntime(CortexShellState state)
        {
            var services = new ShellServiceMap(
                projectCatalog: new TestProjectCatalog(),
                loadedModCatalog: new TestLoadedModCatalog(),
                navigationService: new StubNavigationService(),
                editorContextService: new TestEditorContextService(null));

            return new WorkbenchModuleRuntimeFactory(
                state,
                services,
                delegate { return new StubWorkbenchRuntime(); })
                .Create(new WorkbenchModuleDescriptor(HarmonyPluginIds.ModuleId, HarmonyPluginIds.ContainerId, typeof(object)));
        }

        private static EditorCommandTarget CreateSourcePatchTarget(string documentPath)
        {
            var sourceText = !string.IsNullOrEmpty(documentPath) && File.Exists(documentPath)
                ? File.ReadAllText(documentPath)
                : string.Empty;
            var symbolText = "Prefix";
            return new EditorCommandTarget
            {
                ContextId = EditorContextIds.Symbol,
                SymbolKind = "Method",
                SymbolText = symbolText,
                MetadataName = symbolText,
                ContainingTypeName = "Demo.PatchHost",
                ContainingAssemblyName = string.Empty,
                QualifiedSymbolDisplay = "Demo.PatchHost.Prefix()",
                DocumentationCommentId = string.Empty,
                DocumentPath = documentPath,
                DefinitionDocumentPath = documentPath,
                DefinitionLine = 1,
                DefinitionColumn = 1,
                Line = 1,
                Column = 1,
                AbsolutePosition = !string.IsNullOrEmpty(symbolText)
                    ? Math.Max(0, sourceText.IndexOf(symbolText, StringComparison.Ordinal))
                    : 0
            };
        }

        private static string BuildHarmonySourcePatchDocument()
        {
            return
@"using HarmonyLib;
namespace Demo
{
    [HarmonyPatch(typeof(System.DateTime), ""ToBinary"")]
    internal static class PatchHost
    {
        private static void Prefix()
        {
        }
    }
}";
        }

        private static void EnsureHarmonyAssemblyLoaded()
        {
            var harmonyPath = FindWorkspaceFile(@"libs\0Harmony.dll");
            if (string.IsNullOrEmpty(harmonyPath))
            {
                harmonyPath = FindWorkspaceFile(@"mods\0Harmony\Assemblies\0Harmony.dll");
            }

            if (string.IsNullOrEmpty(harmonyPath) || !File.Exists(harmonyPath))
            {
                throw new FileNotFoundException("0Harmony.dll was not found for the Harmony plugin tests.", harmonyPath ?? string.Empty);
            }

            var probePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? string.Empty, "0Harmony.dll");
            if (!File.Exists(probePath))
            {
                File.Copy(harmonyPath, probePath, true);
            }

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                try
                {
                    var location = Path.GetFullPath(assemblies[i].Location);
                    if (string.Equals(location, harmonyPath, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(location, Path.GetFullPath(probePath), StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }
                }
                catch
                {
                }
            }

            Assembly.LoadFrom(probePath);
        }

        private static string FindWorkspaceFile(string relativePath)
        {
            var roots = new[]
            {
                AppDomain.CurrentDomain.BaseDirectory ?? string.Empty,
                Directory.GetCurrentDirectory()
            };

            for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                var current = roots[rootIndex];
                while (!string.IsNullOrEmpty(current))
                {
                    try
                    {
                        var candidate = Path.Combine(current, relativePath);
                        if (File.Exists(candidate))
                        {
                            return Path.GetFullPath(candidate);
                        }

                        var parent = Directory.GetParent(current);
                        current = parent != null ? parent.FullName : string.Empty;
                    }
                    catch
                    {
                        break;
                    }
                }
            }

            return string.Empty;
        }

        private static string CreateTempSourceFile(string content)
        {
            var filePath = Path.Combine(Path.GetTempPath(), "cortex-harmony-plugin-" + Guid.NewGuid().ToString("N") + ".cs");
            File.WriteAllText(filePath, content ?? string.Empty);
            return filePath;
        }

        private static void DeleteIfExists(string filePath)
        {
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }

        private sealed class StubWorkbenchRuntime : IWorkbenchRuntime
        {
            public StubWorkbenchRuntime()
            {
                CommandRegistry = new Cortex.Core.Services.CommandRegistry();
                ContributionRegistry = new Cortex.Core.Services.ContributionRegistry();
                WorkbenchState = new WorkbenchState();
                LayoutState = new LayoutState();
                StatusState = new StatusState();
                ThemeState = new ThemeState();
                FocusState = new FocusState();
            }

            public ICommandRegistry CommandRegistry { get; private set; }

            public IContributionRegistry ContributionRegistry { get; private set; }

            public WorkbenchState WorkbenchState { get; private set; }

            public LayoutState LayoutState { get; private set; }

            public StatusState StatusState { get; private set; }

            public ThemeState ThemeState { get; private set; }

            public FocusState FocusState { get; private set; }
        }

        private sealed class StubNavigationService : ICortexNavigationService
        {
            public DocumentSession OpenDocument(CortexShellState state, string filePath, int highlightedLine, string successStatusMessage, string failureStatusMessage) { return new DocumentSession { FilePath = filePath, HighlightedLine = highlightedLine }; }
            public void PreloadDocument(CortexShellState state, string filePath) { }
            public void PreloadHoverResponseTarget(CortexShellState state, LanguageServiceHoverResponse response) { }
            public void PreloadHoverDisplayPartTarget(CortexShellState state, LanguageServiceHoverDisplayPart part) { }
            public void PreloadHoverNavigationTarget(CortexShellState state, EditorHoverNavigationTarget target) { }
            public DecompilerResponse RequestDecompilerSource(CortexShellState state, string assemblyPath, int metadataToken, DecompilerEntityKind entityKind, bool ignoreCache) { return new DecompilerResponse(); }
            public bool OpenDecompilerResult(CortexShellState state, DecompilerResponse response, string successStatusMessage, string failureStatusMessage) { return true; }
            public bool OpenDecompilerResult(CortexShellState state, DecompilerResponse response, int highlightedLine, string successStatusMessage, string failureStatusMessage) { return true; }
            public bool DecompileAndOpen(CortexShellState state, string assemblyPath, int metadataToken, DecompilerEntityKind entityKind, bool ignoreCache, string successStatusMessage, string failureStatusMessage) { return true; }
            public bool OpenDecompilerMethodTarget(CortexShellState state, string assemblyPath, int methodMetadataToken, string metadataName, string containingTypeName, string symbolKind, bool ignoreCache, string successStatusMessage, string failureStatusMessage) { return true; }
            public DecompilerResponse RequestDecompilerMethodView(CortexShellState state, string assemblyPath, int methodMetadataToken, string metadataName, string containingTypeName, string symbolKind, bool ignoreCache, out int highlightedLine) { highlightedLine = 1; return new DecompilerResponse(); }
            public SourceNavigationTarget ResolveRuntimeTarget(RuntimeLogEntry entry, int frameIndex, CortexShellState state) { return new SourceNavigationTarget { Success = true }; }
            public bool OpenRuntimeTarget(CortexShellState state, SourceNavigationTarget target, string successStatusMessage, string failureStatusMessage) { return true; }
            public bool OpenHoverDisplayPart(CortexShellState state, LanguageServiceHoverDisplayPart part, string successStatusMessage, string failureStatusMessage) { return true; }
            public bool OpenHoverNavigationTarget(CortexShellState state, EditorHoverNavigationTarget target, string successStatusMessage, string failureStatusMessage) { return true; }
            public bool OpenLanguageSymbolTarget(CortexShellState state, string symbolDisplay, string symbolKind, string metadataName, string containingTypeName, string containingAssemblyName, string documentationCommentId, string definitionDocumentPath, LanguageServiceRange definitionRange, string successStatusMessage, string failureStatusMessage) { return true; }
        }
    }
}
