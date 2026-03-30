using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.Modules.Editor;
using Cortex.Plugin.Harmony;
using Cortex.Plugin.Harmony.Runtime;
using Cortex.Plugin.Harmony.Services;
using Cortex.Plugin.Harmony.Services.Editor;
using Cortex.Plugin.Harmony.Services.Generation;
using Cortex.Plugin.Harmony.Services.Presentation;
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
    public sealed class HarmonyPluginRegistrationTests
    {
        [Fact]
        public void Contributor_RegistersHarmonyThroughPublicPluginContracts()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var commandRegistry = new CommandRegistry();
                var contributionRegistry = new ContributionRegistry();
                var moduleRegistry = new CortexShellModuleContributionRegistry();
                var extensionRegistry = new WorkbenchExtensionRegistry();
                var runtimeAccess = new TestWorkbenchRuntimeAccess();

                new HarmonyPluginContributor().Register(new WorkbenchPluginContext(
                    commandRegistry,
                    contributionRegistry,
                    moduleRegistry,
                    extensionRegistry,
                    runtimeAccess));

                Assert.Contains(contributionRegistry.GetViewContainers(), contribution => string.Equals(contribution.ContainerId, HarmonyPluginIds.ContainerId, StringComparison.Ordinal));
                Assert.Contains(contributionRegistry.GetViews(HarmonyPluginIds.ContainerId), contribution => string.Equals(contribution.ViewId, HarmonyPluginIds.ViewId, StringComparison.Ordinal));
                Assert.Contains(contributionRegistry.GetIcons(), contribution => string.Equals(contribution.IconId, HarmonyPluginIds.ContainerId, StringComparison.Ordinal));
                Assert.NotNull(moduleRegistry.FindContribution(HarmonyPluginIds.ContainerId));

                Assert.NotNull(commandRegistry.Get(HarmonyPluginIds.OpenWindowCommandId));
                Assert.NotNull(commandRegistry.Get(HarmonyPluginIds.ViewPatchesCommandId));
                Assert.NotNull(commandRegistry.Get(HarmonyPluginIds.GeneratePrefixCommandId));
                Assert.NotNull(commandRegistry.Get(HarmonyPluginIds.GeneratePostfixCommandId));
                Assert.NotNull(commandRegistry.Get(HarmonyPluginIds.RefreshCommandId));
                Assert.NotNull(commandRegistry.Get(HarmonyPluginIds.CopySummaryCommandId));

                Assert.Contains(contributionRegistry.GetEditorContextActions(), contribution => string.Equals(contribution.CommandId, HarmonyPluginIds.GeneratePrefixCommandId, StringComparison.Ordinal));
                Assert.Contains(contributionRegistry.GetExplorerFilters(), contribution => string.Equals(contribution.FilterId, HarmonyPluginIds.ExplorerFilterId, StringComparison.Ordinal));
                Assert.Contains(extensionRegistry.GetMethodInspectorSections(), contribution => string.Equals(contribution.ContributionId, HarmonyPluginIds.InspectorSectionId, StringComparison.Ordinal));
                Assert.Contains(extensionRegistry.GetEditorAdornments(), contribution => string.Equals(contribution.ContributionId, HarmonyPluginIds.EditorAdornmentId, StringComparison.Ordinal));
                Assert.Contains(extensionRegistry.GetEditorWorkflows(), contribution => string.Equals(contribution.ContributionId, HarmonyPluginIds.EditorWorkflowId, StringComparison.Ordinal));
            });
        }

        [Fact]
        public void CommandsAndEditorWorkflow_UseSeparatedModuleStateScopes()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                EnsureHarmonyAssemblyLoaded();
                var state = new CortexShellState();
                state.Editor.MethodInspector.IsVisible = true;
                state.Editor.MethodInspector.ContextKey = "ctx.harmony";
                var commandRegistry = new CommandRegistry();
                var contributionRegistry = new ContributionRegistry();
                var moduleRegistry = new CortexShellModuleContributionRegistry();
                var extensionRegistry = new WorkbenchExtensionRegistry();
                var sourcePath = Path.Combine(Path.GetTempPath(), "cortex-harmony-registration-" + Guid.NewGuid().ToString("N") + ".cs");
                try
                {
                    File.WriteAllText(sourcePath, BuildHarmonySourcePatchDocument());
                    var target = CreateSourcePatchTarget(sourcePath, "ctx.harmony", "surface.harmony");
                    var editorContext = new EditorContextSnapshot
                    {
                        ContextKey = target.ContextKey,
                        SurfaceId = target.SurfaceId,
                        PaneId = "main",
                        SurfaceKind = EditorSurfaceKind.Source,
                        DocumentPath = target.DocumentPath,
                        DocumentKind = DocumentKind.SourceCode,
                        Target = target
                    };
                    var contextService = new TestEditorContextService(editorContext);
                    var runtimeAccess = new TestWorkbenchRuntimeAccess();
                    var moduleRuntime = CreateModuleRuntime(state, contextService);
                    runtimeAccess.Register(moduleRuntime);

                    new HarmonyPluginContributor().Register(new WorkbenchPluginContext(
                        commandRegistry,
                        contributionRegistry,
                        moduleRegistry,
                        extensionRegistry,
                        runtimeAccess));

                    Assert.True(commandRegistry.Execute(HarmonyPluginIds.ViewPatchesCommandId, new CommandExecutionContext { Parameter = target }));

                    var stateStore = new HarmonyModuleStateStore();
                    var persistent = stateStore.ReadPersistent(moduleRuntime);
                    var workflow = stateStore.GetWorkflow(moduleRuntime);
                    var documentState = stateStore.GetDocument(moduleRuntime, editorContext, false);

                    Assert.False(string.IsNullOrEmpty(persistent.LastInspectedSymbol), runtimeAccess.GetStatusMessage() ?? string.Empty);
                    Assert.Equal(target.QualifiedSymbolDisplay, persistent.LastInspectedSymbol);
                    Assert.Equal(target.DocumentPath, persistent.LastDocumentPath);
                    Assert.Equal(target.QualifiedSymbolDisplay, workflow.ActiveSymbolDisplay);
                    Assert.Equal(target.ContainingTypeName, workflow.ActiveContainingTypeName);
                    Assert.NotNull(documentState);
                    Assert.Equal(target.QualifiedSymbolDisplay, documentState.LastInspectedSymbol);

                    Assert.True(commandRegistry.Execute(HarmonyPluginIds.GeneratePrefixCommandId, new CommandExecutionContext { Parameter = target }));
                    Assert.Equal("Prefix", stateStore.ReadPersistent(moduleRuntime).PreferredGenerationKind);
                    Assert.True(stateStore.GetWorkflow(moduleRuntime).IsInsertionSelectionActive);

                    var editorRuntime = new EditorContributionRuntime(extensionRegistry, runtimeAccess, contextService);
                    var prepared = editorRuntime.PrepareInspector(state, new DocumentSession
                    {
                        FilePath = target.DocumentPath,
                        Kind = DocumentKind.SourceCode,
                        Text = "class Calculator { int Calculate() { return 1; } }"
                    });
                    Assert.NotNull(prepared);
                    Assert.Contains(prepared.ViewModel.Sections, section => string.Equals(section.Id, HarmonyPluginIds.InspectorSectionId, StringComparison.Ordinal));

                    var adornments = editorRuntime.BuildAdornments(state, new DocumentSession { FilePath = target.DocumentPath, Kind = DocumentKind.SourceCode }, target.SurfaceId);
                    Assert.Contains(adornments, adornment => string.Equals(adornment.AdornmentId, HarmonyPluginIds.EditorAdornmentId, StringComparison.Ordinal));
                    Assert.Contains(adornments, adornment => string.Equals(adornment.Label, "Pick Insert", StringComparison.Ordinal));

                    editorRuntime.Synchronize(state, new DocumentSession { FilePath = target.DocumentPath, Kind = DocumentKind.SourceCode }, target.SurfaceId, true);
                    Assert.Equal("Click a writable editor line to place the Harmony patch. Press Escape to cancel.", runtimeAccess.GetStatusMessage());

                    var pointerResult = editorRuntime.HandlePointer(
                        state,
                        new DocumentSession { FilePath = target.DocumentPath, Kind = DocumentKind.SourceCode },
                        target.SurfaceId,
                        true,
                        12,
                        144);
                    Assert.True(pointerResult.Handled);
                    Assert.True(pointerResult.ConsumeInput);

                    var editorState = stateStore.GetEditor(moduleRuntime, editorContext, false);
                    Assert.NotNull(editorState);
                    Assert.Equal(12, editorState.SelectedLineNumber);
                    Assert.Equal(144, editorState.SelectedAbsolutePosition);
                    Assert.False(stateStore.GetWorkflow(moduleRuntime).IsInsertionSelectionActive);
                }
                finally
                {
                    if (File.Exists(sourcePath))
                    {
                        File.Delete(sourcePath);
                    }
                }
            });
        }

        [Fact]
        public void ModuleAndCommands_AreDisabled_When0HarmonyIsUnavailable()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var state = new CortexShellState();
                var commandRegistry = new CommandRegistry();
                var contributionRegistry = new ContributionRegistry();
                var moduleRegistry = new CortexShellModuleContributionRegistry();
                var extensionRegistry = new WorkbenchExtensionRegistry();
                var runtimeAccess = new TestWorkbenchRuntimeAccess();
                var moduleRuntime = CreateModuleRuntime(state, new TestEditorContextService(null));
                runtimeAccess.Register(moduleRuntime);

                var stateStore = new HarmonyModuleStateStore();
                var workflowController = CreateWorkflowController(stateStore, delegate { return null; });
                var contributor = new WorkbenchPluginContext(
                    commandRegistry,
                    contributionRegistry,
                    moduleRegistry,
                    extensionRegistry,
                    runtimeAccess);

                new HarmonyCommandRegistrar(stateStore, workflowController).Register(contributor);
                var module = new HarmonyModule(stateStore, workflowController);

                Assert.Contains("0Harmony", module.GetUnavailableMessage());
                Assert.False(commandRegistry.CanExecute(HarmonyPluginIds.OpenWindowCommandId, new CommandExecutionContext()));
                Assert.False(commandRegistry.CanExecute(
                    HarmonyPluginIds.ViewPatchesCommandId,
                    new CommandExecutionContext
                    {
                        Parameter = new EditorCommandTarget
                        {
                            ContextId = EditorContextIds.Symbol,
                            SymbolText = "Prefix"
                        }
                    }));
            });
        }

        private static IWorkbenchModuleRuntime CreateModuleRuntime(CortexShellState state, IEditorContextService editorContextService)
        {
            var services = new ShellServiceMap
            {
                ProjectCatalog = new TestProjectCatalog(),
                LoadedModCatalog = new TestLoadedModCatalog(),
                NavigationService = new TestNavigationService(),
                EditorContextService = editorContextService
            };

            return new WorkbenchModuleRuntimeFactory(
                state,
                services,
                delegate { return new TestWorkbenchRuntime(new CommandRegistry(), new ContributionRegistry()); })
                .Create(new WorkbenchModuleDescriptor(HarmonyPluginIds.ModuleId, HarmonyPluginIds.ContainerId, typeof(object)));
        }

        private static HarmonyWorkflowController CreateWorkflowController(HarmonyModuleStateStore stateStore, Func<Assembly> harmonyAssemblyResolver)
        {
            return new HarmonyWorkflowController(
                stateStore,
                new HarmonyMethodResolver(),
                new HarmonyRuntimeInspectionService(harmonyAssemblyResolver),
                new HarmonyPatchDisplayService(),
                new HarmonyPatchTemplateService(),
                new HarmonyPatchInsertionService(stateStore),
                new HarmonyTemplateNavigationService(stateStore),
                new HarmonyMethodInspectorNavigationActionFactory());
        }

        private static EditorCommandTarget CreateSourcePatchTarget(string documentPath, string contextKey, string surfaceId)
        {
            var sourceText = !string.IsNullOrEmpty(documentPath) && File.Exists(documentPath)
                ? File.ReadAllText(documentPath)
                : string.Empty;
            var symbolText = "Prefix";
            return new EditorCommandTarget
            {
                ContextKey = contextKey,
                SurfaceId = surfaceId,
                ContextId = EditorContextIds.Symbol,
                DocumentPath = documentPath,
                DefinitionDocumentPath = documentPath,
                DefinitionLine = 1,
                DefinitionColumn = 1,
                Line = 1,
                Column = 1,
                AbsolutePosition = !string.IsNullOrEmpty(symbolText)
                    ? Math.Max(0, sourceText.IndexOf(symbolText, StringComparison.Ordinal))
                    : 0,
                SymbolKind = "Method",
                SymbolText = symbolText,
                MetadataName = symbolText,
                QualifiedSymbolDisplay = "Demo.PatchHost.Prefix()",
                ContainingTypeName = "Demo.PatchHost",
                ContainingAssemblyName = string.Empty,
                DocumentationCommentId = string.Empty
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

        private sealed class TestWorkbenchRuntimeAccess : IWorkbenchRuntimeAccess, IWorkbenchModuleRuntimeResolver, IWorkbenchFeedbackRuntime
        {
            private readonly Dictionary<string, IWorkbenchModuleRuntime> _modulesById = new Dictionary<string, IWorkbenchModuleRuntime>(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, IWorkbenchModuleRuntime> _modulesByContainer = new Dictionary<string, IWorkbenchModuleRuntime>(StringComparer.OrdinalIgnoreCase);
            private string _statusMessage = string.Empty;

            public IWorkbenchModuleRuntimeResolver Modules
            {
                get { return this; }
            }

            public IWorkbenchFeedbackRuntime Feedback
            {
                get { return this; }
            }

            public void Register(IWorkbenchModuleRuntime runtime)
            {
                if (runtime == null || runtime.Lifecycle == null)
                {
                    return;
                }

                _modulesById[runtime.Lifecycle.ModuleId] = runtime;
                _modulesByContainer[runtime.Lifecycle.ContainerId] = runtime;
            }

            public IWorkbenchModuleRuntime Get(string moduleId)
            {
                IWorkbenchModuleRuntime runtime;
                return !string.IsNullOrEmpty(moduleId) && _modulesById.TryGetValue(moduleId, out runtime)
                    ? runtime
                    : null;
            }

            public IWorkbenchModuleRuntime GetByContainer(string containerId)
            {
                IWorkbenchModuleRuntime runtime;
                return !string.IsNullOrEmpty(containerId) && _modulesByContainer.TryGetValue(containerId, out runtime)
                    ? runtime
                    : null;
            }

            public string GetStatusMessage()
            {
                return _statusMessage;
            }

            public void SetStatusMessage(string message)
            {
                _statusMessage = message ?? string.Empty;
            }
        }

        private sealed class TestWorkbenchRuntime : IWorkbenchRuntime
        {
            public TestWorkbenchRuntime(ICommandRegistry commandRegistry, IContributionRegistry contributionRegistry)
            {
                CommandRegistry = commandRegistry;
                ContributionRegistry = contributionRegistry;
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

            public WorkbenchPresentationSnapshot CreateSnapshot()
            {
                return new WorkbenchPresentationSnapshot();
            }
        }

        private sealed class TestEditorContextService : IEditorContextService
        {
            private readonly EditorContextSnapshot _context;
            private readonly EditorCommandInvocation _invocation;

            public TestEditorContextService(EditorContextSnapshot context)
            {
                _context = context;
                _invocation = context != null && context.Target != null
                    ? new EditorCommandInvocation
                    {
                        ActiveContainerId = CortexWorkbenchIds.EditorContainer,
                        ActiveDocumentId = context.DocumentPath,
                        FocusedRegionId = context.SurfaceId,
                        Target = context.Target.Clone()
                    }
                    : null;
            }

            public string BuildSurfaceId(string documentPath, EditorSurfaceKind surfaceKind, string paneId) { return string.Empty; }
            public EditorContextSnapshot PublishDocumentContext(CortexShellState state, DocumentSession session, string surfaceId, string paneId, EditorSurfaceKind surfaceKind, bool editingEnabled, int absolutePosition) { return _context != null ? _context.Clone() : null; }
            public EditorContextSnapshot PublishInvocationContext(CortexShellState state, DocumentSession session, string surfaceId, string paneId, EditorSurfaceKind surfaceKind, EditorCommandInvocation invocation, bool setActive) { return _context != null ? _context.Clone() : null; }
            public EditorContextSnapshot PublishTargetContext(CortexShellState state, DocumentSession session, string surfaceId, string paneId, EditorSurfaceKind surfaceKind, EditorCommandTarget target, bool setActive) { return _context != null ? _context.Clone() : null; }
            public EditorContextSnapshot GetActiveContext(CortexShellState state) { return _context != null ? _context.Clone() : null; }
            public EditorContextSnapshot GetHoveredContext(CortexShellState state) { return null; }
            public EditorContextSnapshot GetContext(CortexShellState state, string contextKey) { return _context != null ? _context.Clone() : null; }
            public EditorContextSnapshot GetSurfaceContext(CortexShellState state, string surfaceId) { return _context != null ? _context.Clone() : null; }
            public EditorCommandTarget ResolveTarget(CortexShellState state, string contextKey) { return _context != null && _context.Target != null ? _context.Target.Clone() : null; }
            public EditorCommandInvocation ResolveInvocation(CortexShellState state, string contextKey)
            {
                if (_invocation == null)
                {
                    return null;
                }

                return new EditorCommandInvocation
                {
                    ActiveContainerId = _invocation.ActiveContainerId,
                    ActiveDocumentId = _invocation.ActiveDocumentId,
                    FocusedRegionId = _invocation.FocusedRegionId,
                    Target = _invocation.Target != null ? _invocation.Target.Clone() : null
                };
            }
            public Cortex.LanguageService.Protocol.LanguageServiceHoverResponse ResolveHoverResponse(CortexShellState state, string contextKey, string hoverKey) { return null; }
            public Cortex.LanguageService.Protocol.LanguageServiceHoverResponse ResolveHoverResponse(CortexShellState state, string hoverKey) { return null; }
            public EditorResolvedHoverContent ResolveHoverContent(CortexShellState state, string contextKey, string hoverKey) { return null; }
            public EditorResolvedHoverContent ResolveHoverContent(CortexShellState state, string hoverKey) { return null; }
            public string ApplyHoverResponse(CortexShellState state, string contextKey, string hoverKey, Cortex.LanguageService.Protocol.LanguageServiceHoverResponse response) { return string.Empty; }
            public void ApplyHoverContent(CortexShellState state, string contextKey, string hoverKey, EditorResolvedHoverContent content) { }
            public void ApplySymbolContext(CortexShellState state, string contextKey, Cortex.LanguageService.Protocol.LanguageServiceSymbolContextResponse response) { }
            public void ClearHoverResponse(CortexShellState state, string contextKey) { }
            public void PublishHoveredContext(CortexShellState state, string contextKey, string definitionDocumentPath) { }
            public void ClearHoveredContext(CortexShellState state) { }
            public string BuildContextKey(string surfaceId, string documentPath, int documentVersion, int caretIndex, int selectionStart, int selectionEnd, int targetStart, int targetLength, string symbolText) { return string.Empty; }
        }

        private sealed class TestProjectCatalog : IProjectCatalog
        {
            public IList<CortexProjectDefinition> GetProjects() { return new List<CortexProjectDefinition>(); }
            public CortexProjectDefinition GetProject(string modId) { return null; }
            public void Upsert(CortexProjectDefinition definition) { }
            public void Remove(string modId) { }
        }

        private sealed class TestLoadedModCatalog : ILoadedModCatalog
        {
            public IList<LoadedModInfo> GetLoadedMods() { return new List<LoadedModInfo>(); }
            public LoadedModInfo GetMod(string modId) { return null; }
        }

        private sealed class TestNavigationService : ICortexNavigationService
        {
            public DocumentSession OpenDocument(CortexShellState state, string filePath, int highlightedLine, string successStatusMessage, string failureStatusMessage) { return new DocumentSession { FilePath = filePath, HighlightedLine = highlightedLine }; }
            public void PreloadDocument(CortexShellState state, string filePath) { }
            public void PreloadHoverResponseTarget(CortexShellState state, Cortex.LanguageService.Protocol.LanguageServiceHoverResponse response) { }
            public void PreloadHoverDisplayPartTarget(CortexShellState state, Cortex.LanguageService.Protocol.LanguageServiceHoverDisplayPart part) { }
            public void PreloadHoverNavigationTarget(CortexShellState state, EditorHoverNavigationTarget target) { }
            public DecompilerResponse RequestDecompilerSource(CortexShellState state, string assemblyPath, int metadataToken, DecompilerEntityKind entityKind, bool ignoreCache) { return new DecompilerResponse(); }
            public bool OpenDecompilerResult(CortexShellState state, DecompilerResponse response, string successStatusMessage, string failureStatusMessage) { return true; }
            public bool OpenDecompilerResult(CortexShellState state, DecompilerResponse response, int highlightedLine, string successStatusMessage, string failureStatusMessage) { return true; }
            public bool DecompileAndOpen(CortexShellState state, string assemblyPath, int metadataToken, DecompilerEntityKind entityKind, bool ignoreCache, string successStatusMessage, string failureStatusMessage) { return true; }
            public bool OpenDecompilerMethodTarget(CortexShellState state, string assemblyPath, int methodMetadataToken, string metadataName, string containingTypeName, string symbolKind, bool ignoreCache, string successStatusMessage, string failureStatusMessage) { return true; }
            public DecompilerResponse RequestDecompilerMethodView(CortexShellState state, string assemblyPath, int methodMetadataToken, string metadataName, string containingTypeName, string symbolKind, bool ignoreCache, out int highlightedLine) { highlightedLine = 1; return new DecompilerResponse(); }
            public SourceNavigationTarget ResolveRuntimeTarget(RuntimeLogEntry entry, int frameIndex, CortexShellState state) { return new SourceNavigationTarget { Success = true }; }
            public bool OpenRuntimeTarget(CortexShellState state, SourceNavigationTarget target, string successStatusMessage, string failureStatusMessage) { return true; }
            public bool OpenHoverDisplayPart(CortexShellState state, Cortex.LanguageService.Protocol.LanguageServiceHoverDisplayPart part, string successStatusMessage, string failureStatusMessage) { return true; }
            public bool OpenHoverNavigationTarget(CortexShellState state, EditorHoverNavigationTarget target, string successStatusMessage, string failureStatusMessage) { return true; }
            public bool OpenLanguageSymbolTarget(CortexShellState state, string symbolDisplay, string symbolKind, string metadataName, string containingTypeName, string containingAssemblyName, string documentationCommentId, string definitionDocumentPath, Cortex.LanguageService.Protocol.LanguageServiceRange definitionRange, string successStatusMessage, string failureStatusMessage) { return true; }
        }
    }
}
