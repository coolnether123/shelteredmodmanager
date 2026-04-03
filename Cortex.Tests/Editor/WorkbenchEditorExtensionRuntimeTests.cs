using System;
using System.Collections.Generic;
using System.Linq;
using Cortex;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.LanguageService.Protocol;
using Cortex.Modules.Editor;
using Cortex.Plugins.Abstractions;
using Cortex.Presentation.Abstractions;
using Cortex.Presentation.Models;
using Cortex.Services.Inspector;
using Cortex.Services.Inspector.Lifecycle;
using Cortex.Services.Navigation;
using Cortex.Services.Semantics.Context;
using Cortex.Shell;
using Cortex.Tests.Testing;
using Xunit;

namespace Cortex.Tests.Editor
{
    public sealed class WorkbenchEditorExtensionRuntimeTests
    {
        [Fact]
        public void InspectorContributions_AreOrderedFilteredAndInvoked_UsingGenericSections()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var state = new CortexShellState();
                state.Editor.MethodInspector.IsVisible = true;
                state.Editor.MethodInspector.Title = "Calculate";
                state.Editor.MethodInspector.ContextKey = "ctx.review";
                state.Editor.MethodInspector.SectionExpansionStates["review.notes"] = false;

                var session = new DocumentSession
                {
                    FilePath = @"D:\Workspace\Calculator.cs",
                    Kind = DocumentKind.SourceCode,
                    Text = "namespace Sample\r\n{\r\n    class Calculator\r\n    {\r\n        int Calculate() { return 1; }\r\n    }\r\n}"
                };
                var target = new EditorCommandTarget
                {
                    ContextKey = "ctx.review",
                    ContextId = EditorContextIds.Symbol,
                    SymbolText = "Calculate",
                    MetadataName = "Calculate",
                    SymbolKind = "Method",
                    DocumentPath = session.FilePath,
                    DefinitionDocumentPath = session.FilePath,
                    DefinitionLine = 5,
                    DefinitionColumn = 9,
                    Line = 5,
                    Column = 9,
                    ContainingTypeName = "Calculator",
                    ContainingAssemblyName = "Sample.Assembly",
                    QualifiedSymbolDisplay = "Sample.Calculator.Calculate()"
                };
                var editorContext = CreateEditorContext(target, session.FilePath, "surface.review", "ctx.review");
                var contextService = new ContributionContextService(editorContext, target);
                var navigationService = new StubNavigationService();
                var runtimeAccess = new TestWorkbenchRuntimeAccess();
                runtimeAccess.Register(CreateModuleRuntime(
                    state,
                    new WorkbenchModuleDescriptor("module.review", "container.review", typeof(PlaceholderModule)),
                    contextService,
                    navigationService));
                var extensionRegistry = new WorkbenchExtensionRegistry();
                extensionRegistry.RegisterMethodInspectorSection(new WorkbenchMethodInspectorSectionContribution
                {
                    ContributionId = "review.hidden",
                    SortOrder = 240,
                    CanDisplay = delegate(WorkbenchMethodInspectorContext context) { return false; },
                    BuildSection = delegate(WorkbenchMethodInspectorContext context)
                    {
                        return new MethodInspectorSectionViewModel
                        {
                            Id = "review.hidden",
                            Title = "Hidden"
                        };
                    }
                });
                extensionRegistry.RegisterMethodInspectorSection(new WorkbenchMethodInspectorSectionContribution
                {
                    ContributionId = "review.notes",
                    SortOrder = 250,
                    DefaultExpanded = true,
                    BuildSection = delegate(WorkbenchMethodInspectorContext context)
                    {
                        return new MethodInspectorSectionViewModel
                        {
                            Id = "review.notes",
                            Title = "Review Notes",
                            Elements = new MethodInspectorElementViewModel[]
                            {
                                new MethodInspectorTextViewModel
                                {
                                    Label = "Focus",
                                    Value = context.Target != null ? context.Target.SymbolText : string.Empty
                                }
                            }
                        };
                    },
                    TryHandleAction = delegate(WorkbenchMethodInspectorActionContext context)
                    {
                        if (string.Equals(context.ActionId, "review.notes.open-source", StringComparison.Ordinal))
                        {
                            var module = context.Runtime.Modules.Get("module.review");
                            if (module != null && context.Session != null)
                            {
                                module.Navigation.OpenDocument(context.Session.FilePath, 23);
                            }

                            return new WorkbenchMethodInspectorActionResult
                            {
                                Handled = true
                            };
                        }

                        return string.Equals(context.ActionId, "review.notes.approve", StringComparison.Ordinal)
                            ? new WorkbenchMethodInspectorActionResult
                            {
                                Handled = true,
                                CloseInspector = true
                            }
                            : new WorkbenchMethodInspectorActionResult();
                    }
                });
                extensionRegistry.RegisterMethodInspectorSection(new WorkbenchMethodInspectorSectionContribution
                {
                    ContributionId = "review.bookmarks",
                    SortOrder = 260,
                    DefaultExpanded = false,
                    BuildSection = delegate(WorkbenchMethodInspectorContext context)
                    {
                        return new MethodInspectorSectionViewModel
                        {
                            Id = "review.bookmarks",
                            Title = "Review Bookmarks",
                            Elements = new MethodInspectorElementViewModel[]
                            {
                                new MethodInspectorTextViewModel
                                {
                                    Label = string.Empty,
                                    Value = "Bookmark the current method for later verification."
                                }
                            }
                        };
                    }
                });

                var runtime = new EditorContributionRuntime(extensionRegistry, runtimeAccess, contextService);
                var prepared = runtime.PrepareInspector(state, session);

                Assert.NotNull(prepared);
                Assert.Equal(
                    new[] { "Structure", "Relationships", "Review Notes", "Review Bookmarks", "Source Context" },
                    prepared.ViewModel.Sections.Select(section => section.Title).ToArray());
                Assert.DoesNotContain(prepared.ViewModel.Sections, section => string.Equals(section.Id, "review.hidden", StringComparison.Ordinal));
                Assert.False(prepared.ViewModel.Sections.Single(section => string.Equals(section.Id, "review.notes", StringComparison.Ordinal)).Expanded);
                Assert.False(prepared.ViewModel.Sections.Single(section => string.Equals(section.Id, "review.bookmarks", StringComparison.Ordinal)).Expanded);

                new EditorMethodInspectorService(contextService).ToggleSection(state, "review.notes");
                prepared = runtime.PrepareInspector(state, session);

                Assert.True(prepared.ViewModel.Sections.Single(section => string.Equals(section.Id, "review.notes", StringComparison.Ordinal)).Expanded);

                var result = runtime.HandleInspectorAction("review.notes.approve", prepared);

                Assert.True(result.Handled);
                Assert.True(result.CloseInspector);

                result = runtime.HandleInspectorAction("review.notes.open-source", prepared);

                Assert.True(result.Handled);
                Assert.Equal(session.FilePath, navigationService.LastOpenedDocumentPath);
                Assert.Equal(23, navigationService.LastOpenedLine);
            });
        }

        [Fact]
        public void EditorWorkflowsAndAdornments_UseModuleStateAndEditorScope()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var state = new CortexShellState();
                var session = new DocumentSession
                {
                    FilePath = @"D:\Workspace\Calculator.cs",
                    Kind = DocumentKind.SourceCode,
                    Text = "class Calculator { int Calculate() { return 1; } }"
                };
                var target = new EditorCommandTarget
                {
                    ContextKey = "ctx.review",
                    ContextId = EditorContextIds.Symbol,
                    SymbolText = "Calculate",
                    MetadataName = "Calculate",
                    SymbolKind = "Method",
                    DocumentPath = session.FilePath,
                    DefinitionDocumentPath = session.FilePath,
                    DefinitionLine = 1,
                    DefinitionColumn = 24,
                    Line = 1,
                    Column = 24,
                    ContainingTypeName = "Calculator",
                    ContainingAssemblyName = "Sample.Assembly",
                    QualifiedSymbolDisplay = "Sample.Calculator.Calculate()"
                };
                var editorContext = CreateEditorContext(target, session.FilePath, "surface.review", "ctx.review");
                var contextService = new ContributionContextService(editorContext, target);
                var moduleRuntime = CreateModuleRuntime(
                    state,
                    new WorkbenchModuleDescriptor("module.review", "container.review", typeof(PlaceholderModule)),
                    contextService,
                    new StubNavigationService());
                var runtimeAccess = new TestWorkbenchRuntimeAccess();
                runtimeAccess.Register(moduleRuntime);
                moduleRuntime.State.Workflow.Set("anchor.selection", new ReviewAnchorWorkflowState { IsPicking = true });

                var extensionRegistry = new WorkbenchExtensionRegistry();
                extensionRegistry.RegisterEditorAdornment(new WorkbenchEditorAdornmentContribution
                {
                    ContributionId = "review.anchor.prompt",
                    SortOrder = 100,
                    BuildAdornments = delegate(WorkbenchEditorAdornmentContext context)
                    {
                        var module = context.Runtime.Modules.Get("module.review");
                        var workflow = module != null ? module.State.Workflow.Get<ReviewAnchorWorkflowState>("anchor.selection") : null;
                        return workflow != null && workflow.IsPicking
                            ? new[]
                            {
                                new WorkbenchEditorAdornment
                                {
                                    AdornmentId = "review.anchor.prompt",
                                    Label = "Pick Anchor",
                                    Placement = WorkbenchEditorAdornmentPlacement.TopRight,
                                    SortOrder = 100,
                                    Enabled = true
                                }
                            }
                            : new WorkbenchEditorAdornment[0];
                    }
                });
                extensionRegistry.RegisterEditorAdornment(new WorkbenchEditorAdornmentContribution
                {
                    ContributionId = "review.symbol.badge",
                    SortOrder = 200,
                    BuildAdornments = delegate(WorkbenchEditorAdornmentContext context)
                    {
                        return new[]
                        {
                            new WorkbenchEditorAdornment
                            {
                                AdornmentId = "review.symbol.badge",
                                Label = context.Target != null ? context.Target.SymbolText : string.Empty,
                                Placement = WorkbenchEditorAdornmentPlacement.TopRight,
                                SortOrder = 200,
                                Enabled = false
                            }
                        };
                    }
                });
                extensionRegistry.RegisterEditorWorkflow(new WorkbenchEditorWorkflowContribution
                {
                    ContributionId = "review.anchor.workflow",
                    SortOrder = 100,
                    IsActive = delegate(WorkbenchEditorWorkflowContext context)
                    {
                        var module = context.Runtime.Modules.Get("module.review");
                        var workflow = module != null ? module.State.Workflow.Get<ReviewAnchorWorkflowState>("anchor.selection") : null;
                        return workflow != null && workflow.IsPicking;
                    },
                    Synchronize = delegate(WorkbenchEditorWorkflowContext context)
                    {
                        context.Runtime.Feedback.SetStatusMessage("Select a review anchor.");
                    },
                    TryHandlePointer = delegate(WorkbenchEditorPointerContext context)
                    {
                        var module = context.Runtime.Modules.Get("module.review");
                        var scope = module != null ? module.Editor.CreateEditorScope(context.EditorContext) : null;
                        if (module != null)
                        {
                            module.State.Contexts.Set(scope, "selected.anchor", new ReviewAnchorSelection(context.LineNumber, context.AbsolutePosition));
                        }

                        return new WorkbenchEditorWorkflowResult
                        {
                            Handled = true,
                            ConsumeInput = true
                        };
                    },
                    TryHandleKeyboard = delegate(WorkbenchEditorKeyboardContext context)
                    {
                        if (context.Key != WorkbenchEditorInteractionKey.Escape)
                        {
                            return new WorkbenchEditorWorkflowResult();
                        }

                        var module = context.Runtime.Modules.Get("module.review");
                        if (module != null)
                        {
                            module.State.Workflow.Remove("anchor.selection");
                        }

                        return new WorkbenchEditorWorkflowResult
                        {
                            Handled = true,
                            ConsumeInput = true
                        };
                    }
                });

                var runtime = new EditorContributionRuntime(extensionRegistry, runtimeAccess, contextService);

                var adornments = runtime.BuildAdornments(state, session, editorContext.SurfaceId);
                Assert.Equal(new[] { "Pick Anchor", "Calculate" }, adornments.Select(adornment => adornment.Label).ToArray());
                Assert.True(adornments[0].Enabled);
                Assert.False(adornments[1].Enabled);

                runtime.Synchronize(state, session, editorContext.SurfaceId, true);
                Assert.Equal("Select a review anchor.", runtimeAccess.GetStatusMessage());

                var pointerResult = runtime.HandlePointer(state, session, editorContext.SurfaceId, true, 12, 144);
                Assert.True(pointerResult.Handled);
                Assert.True(pointerResult.ConsumeInput);

                var scopeState = moduleRuntime.State.Contexts.Get<ReviewAnchorSelection>(
                    moduleRuntime.Editor.CreateEditorScope(editorContext),
                    "selected.anchor");
                Assert.NotNull(scopeState);
                Assert.Equal(12, scopeState.LineNumber);
                Assert.Equal(144, scopeState.AbsolutePosition);

                var keyboardResult = runtime.HandleKeyboard(
                    state,
                    session,
                    editorContext.SurfaceId,
                    true,
                    WorkbenchEditorInteractionKey.Escape,
                    false,
                    false,
                    false);
                Assert.True(keyboardResult.Handled);
                Assert.True(keyboardResult.ConsumeInput);
                Assert.Null(moduleRuntime.State.Workflow.Get<ReviewAnchorWorkflowState>("anchor.selection"));
                Assert.False(runtime.HandlePointer(state, session, editorContext.SurfaceId, true, 14, 188).Handled);
            });
        }

        private static EditorContextSnapshot CreateEditorContext(EditorCommandTarget target, string documentPath, string surfaceId, string contextKey)
        {
            return new EditorContextSnapshot
            {
                ContextKey = contextKey,
                SurfaceId = surfaceId,
                PaneId = "main",
                SurfaceKind = EditorSurfaceKind.Source,
                DocumentPath = documentPath,
                DocumentKind = DocumentKind.SourceCode,
                Target = target
            };
        }

        private static IWorkbenchModuleRuntime CreateModuleRuntime(
            CortexShellState state,
            WorkbenchModuleDescriptor descriptor,
            IEditorContextService editorContextService,
            StubNavigationService navigationService)
        {
            var services = new ShellServiceMap(
                projectCatalog: new TestProjectCatalog(),
                loadedModCatalog: new TestLoadedModCatalog(),
                navigationService: navigationService,
                editorContextService: editorContextService);

            return new WorkbenchModuleRuntimeFactory(
                state,
                services,
                delegate { return new TestWorkbenchRuntime(new CommandRegistry(), new ContributionRegistry()); })
                .Create(descriptor);
        }

        private sealed class ContributionContextService : IEditorContextService
        {
            private readonly EditorContextSnapshot _context;
            private readonly EditorCommandInvocation _invocation;
            private readonly EditorCommandTarget _target;

            public ContributionContextService(EditorContextSnapshot context, EditorCommandTarget target)
            {
                _context = context;
                _target = target;
                _invocation = target != null
                    ? new EditorCommandInvocation
                    {
                        ActiveContainerId = "editor",
                        ActiveDocumentId = context != null ? context.DocumentPath : string.Empty,
                        FocusedRegionId = "editor",
                        Target = target
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
            public EditorCommandTarget ResolveTarget(CortexShellState state, string contextKey) { return _target != null ? _target.Clone() : null; }
            public EditorCommandInvocation ResolveInvocation(CortexShellState state, string contextKey) { return _invocation != null ? new EditorCommandInvocation { ActiveContainerId = _invocation.ActiveContainerId, ActiveDocumentId = _invocation.ActiveDocumentId, FocusedRegionId = _invocation.FocusedRegionId, Target = _invocation.Target != null ? _invocation.Target.Clone() : null } : null; }
            public LanguageServiceHoverResponse ResolveHoverResponse(CortexShellState state, string contextKey, string hoverKey) { return null; }
            public LanguageServiceHoverResponse ResolveHoverResponse(CortexShellState state, string hoverKey) { return null; }
            public EditorResolvedHoverContent ResolveHoverContent(CortexShellState state, string contextKey, string hoverKey) { return null; }
            public EditorResolvedHoverContent ResolveHoverContent(CortexShellState state, string hoverKey) { return null; }
            public string ApplyHoverResponse(CortexShellState state, string contextKey, string hoverKey, LanguageServiceHoverResponse response) { return string.Empty; }
            public void ApplyHoverContent(CortexShellState state, string contextKey, string hoverKey, EditorResolvedHoverContent content) { }
            public void ApplySymbolContext(CortexShellState state, string contextKey, LanguageServiceSymbolContextResponse response) { }
            public void ClearHoverResponse(CortexShellState state, string contextKey) { }
            public void PublishHoveredContext(CortexShellState state, string contextKey, string definitionDocumentPath) { }
            public void ClearHoveredContext(CortexShellState state) { }
            public string BuildContextKey(string surfaceId, string documentPath, int documentVersion, int caretIndex, int selectionStart, int selectionEnd, int targetStart, int targetLength, string symbolText) { return string.Empty; }
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
        }

        private sealed class StubNavigationService : ICortexNavigationService
        {
            public string LastOpenedDocumentPath = string.Empty;
            public int LastOpenedLine = -1;

            public DocumentSession OpenDocument(CortexShellState state, string filePath, int highlightedLine, string successStatusMessage, string failureStatusMessage)
            {
                LastOpenedDocumentPath = filePath ?? string.Empty;
                LastOpenedLine = highlightedLine;
                return new DocumentSession { FilePath = filePath, HighlightedLine = highlightedLine };
            }

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

        private sealed class PlaceholderModule
        {
        }

        private sealed class ReviewAnchorWorkflowState
        {
            public bool IsPicking;
        }

        private sealed class ReviewAnchorSelection
        {
            public ReviewAnchorSelection(int lineNumber, int absolutePosition)
            {
                LineNumber = lineNumber;
                AbsolutePosition = absolutePosition;
            }

            public int LineNumber { get; private set; }

            public int AbsolutePosition { get; private set; }
        }
    }
}
