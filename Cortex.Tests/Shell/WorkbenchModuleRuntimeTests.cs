using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.Modules.Shared;
using Cortex.Plugins.Abstractions;
using Cortex.Presentation.Abstractions;
using Cortex.Presentation.Models;
using Cortex.Services.Editor.Commands;
using Cortex.Services.Editor.Context;
using Cortex.Services.Navigation;
using Cortex.Services.Semantics.Context;
using Cortex.Shell;
using Cortex.Tests.Testing;
using Xunit;

namespace Cortex.Tests.Shell
{
    public sealed class WorkbenchModuleRuntimeTests
    {
        [Fact]
        public void PersistentState_PersistsAcrossSessionRestore()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var persistencePath = Path.Combine(Path.GetTempPath(), "cortex-module-state-" + Guid.NewGuid().ToString("N") + ".json");
                try
                {
                    var descriptor = new WorkbenchModuleDescriptor("module.alpha", "container.alpha", typeof(RecordingModule));
                    var initialState = new CortexShellState();
                    var initialRuntime = CreateModuleRuntime(initialState, descriptor);

                    initialRuntime.State.Persistent.SetValue("selection", "method:42");
                    CreateSessionCoordinator(initialState, persistencePath).PersistSession();

                    var restoredState = new CortexShellState();
                    CreateSessionCoordinator(restoredState, persistencePath).RestoreSession();
                    var restoredRuntime = CreateModuleRuntime(restoredState, descriptor);

                    Assert.True(restoredRuntime.State.Persistent.Contains("selection"));
                    Assert.Equal("method:42", restoredRuntime.State.Persistent.GetValue("selection", string.Empty));
                }
                finally
                {
                    if (File.Exists(persistencePath))
                    {
                        File.Delete(persistencePath);
                    }
                }
            });
        }

        [Fact]
        public void WorkflowState_DoesNotPersistAcrossSessionRestore()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var persistencePath = Path.Combine(Path.GetTempPath(), "cortex-workflow-state-" + Guid.NewGuid().ToString("N") + ".json");
                try
                {
                    var descriptor = new WorkbenchModuleDescriptor("module.alpha", "container.alpha", typeof(RecordingModule));
                    var initialState = new CortexShellState();
                    var initialRuntime = CreateModuleRuntime(initialState, descriptor);

                    initialRuntime.State.Workflow.Set("flow", new WorkflowProbe("active"));
                    CreateSessionCoordinator(initialState, persistencePath).PersistSession();

                    var restoredState = new CortexShellState();
                    CreateSessionCoordinator(restoredState, persistencePath).RestoreSession();
                    var restoredRuntime = CreateModuleRuntime(restoredState, descriptor);

                    Assert.Null(restoredRuntime.State.Workflow.Get<WorkflowProbe>("flow"));
                }
                finally
                {
                    if (File.Exists(persistencePath))
                    {
                        File.Delete(persistencePath);
                    }
                }
            });
        }

        [Fact]
        public void ContextState_ClearsWhenDocumentCloses()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var descriptor = new WorkbenchModuleDescriptor("module.alpha", "container.alpha", typeof(RecordingModule));
                var state = new CortexShellState();
                var runtime = CreateModuleRuntime(state, descriptor);
                var documentPath = Path.Combine(Path.GetTempPath(), "context-state-test.cs");
                var session = new DocumentSession
                {
                    FilePath = documentPath,
                    Kind = DocumentKind.SourceCode
                };

                state.Documents.OpenDocuments.Add(session);
                state.Documents.ActiveDocument = session;
                state.Documents.ActiveDocumentPath = documentPath;

                var context = new EditorContextSnapshot
                {
                    DocumentPath = documentPath,
                    SurfaceId = "surface-alpha",
                    PaneId = "main",
                    SurfaceKind = EditorSurfaceKind.Source
                };

                var documentScope = runtime.Editor.CreateDocumentScope(context);
                var editorScope = runtime.Editor.CreateEditorScope(context);

                runtime.State.Contexts.Set(documentScope, "value", new ContextProbe("document"));
                runtime.State.Contexts.Set(editorScope, "value", new ContextProbe("editor"));

                Assert.Equal("document", runtime.State.Contexts.Get<ContextProbe>(documentScope, "value").Value);
                Assert.Equal("editor", runtime.State.Contexts.Get<ContextProbe>(editorScope, "value").Value);

                CortexModuleUtil.CloseDocument(state, documentPath);

                Assert.Null(runtime.State.Contexts.Get<ContextProbe>(documentScope, "value"));
                Assert.Null(runtime.State.Contexts.Get<ContextProbe>(editorScope, "value"));
            });
        }

        [Fact]
        public void ModuleComposition_ExposesTypedRuntimeCapabilities()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var state = new CortexShellState();
                var commandRegistry = new CommandRegistry();
                var contributionRegistry = new ContributionRegistry();
                var workbenchRuntime = new TestWorkbenchRuntime(commandRegistry, contributionRegistry);
                var contribution = new RecordingModuleContribution("module.alpha", "container.alpha");
                var registry = new CortexShellModuleContributionRegistry();
                registry.Register(contribution);

                commandRegistry.Register(new CommandDefinition
                {
                    CommandId = "module.test",
                    DisplayName = "Module Test",
                    SortOrder = 1
                });

                var executed = false;
                commandRegistry.RegisterHandler(
                    "module.test",
                    delegate(CommandExecutionContext context) { executed = context != null; },
                    delegate(CommandExecutionContext context) { return true; });

                var composition = new CortexShellModuleCompositionService(
                    registry,
                    CreateRuntimeFactory(state, workbenchRuntime));
                var module = composition.GetOrCreate("container.alpha");
                var runtime = composition.GetRuntime("container.alpha");

                Assert.NotNull(module);
                Assert.NotNull(runtime);
                Assert.Same(runtime, contribution.CapturedRuntime);
                Assert.NotNull(runtime.Lifecycle);
                Assert.NotNull(runtime.Commands);
                Assert.NotNull(runtime.Navigation);
                Assert.NotNull(runtime.Projects);
                Assert.NotNull(runtime.Editor);
                Assert.NotNull(runtime.State);

                Assert.True(runtime.Commands.Execute("module.test", null));
                Assert.True(executed);

                runtime.Lifecycle.RequestContainer("container.beta", WorkbenchHostLocation.SecondarySideHost);
                Assert.Equal("container.beta", state.Workbench.RequestedContainerId);

                module.Render(new WorkbenchModuleRenderContext("container.alpha", new WorkbenchPresentationSnapshot(), null, runtime), false);
                Assert.Same(runtime, contribution.Module.RenderRuntime);
            });
        }

        [Fact]
        public void MultipleModules_UseGenericStateStorageWithoutHostSpecificFields()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var state = new CortexShellState();
                var workbenchRuntime = new TestWorkbenchRuntime(new CommandRegistry(), new ContributionRegistry());
                var alpha = CreateModuleRuntime(state, new WorkbenchModuleDescriptor("alpha.module", "container.alpha", typeof(RecordingModule)), workbenchRuntime);
                var beta = CreateModuleRuntime(state, new WorkbenchModuleDescriptor("beta.module", "container.beta", typeof(RecordingModule)), workbenchRuntime);

                alpha.State.Workflow.Set("shared", new WorkflowProbe("alpha"));
                beta.State.Workflow.Set("shared", new WorkflowProbe("beta"));
                alpha.State.Persistent.SetValue("shared", "alpha");
                beta.State.Persistent.SetValue("shared", "beta");

                Assert.Equal("alpha", alpha.State.Workflow.Get<WorkflowProbe>("shared").Value);
                Assert.Equal("beta", beta.State.Workflow.Get<WorkflowProbe>("shared").Value);
                Assert.Equal("alpha", alpha.State.Persistent.GetValue("shared", string.Empty));
                Assert.Equal("beta", beta.State.Persistent.GetValue("shared", string.Empty));

                var fields = typeof(CortexShellState).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.Contains(fields, field => string.Equals(field.Name, "Modules", StringComparison.Ordinal));
                Assert.DoesNotContain(fields, field => field.Name.IndexOf("alpha", StringComparison.OrdinalIgnoreCase) >= 0);
                Assert.DoesNotContain(fields, field => field.Name.IndexOf("beta", StringComparison.OrdinalIgnoreCase) >= 0);
            });
        }

        [Fact]
        public void ModuleOwnedState_IsStoredInsideModuleBucketsInsteadOfShellFeatureFields()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var state = new CortexShellState();
                var runtime = CreateModuleRuntime(state, new WorkbenchModuleDescriptor("module.alpha", "container.alpha", typeof(RecordingModule)));
                var context = new EditorContextSnapshot
                {
                    DocumentPath = @"D:\Mods\Alpha\Patch.cs",
                    SurfaceId = "surface.alpha",
                    PaneId = "main",
                    SurfaceKind = EditorSurfaceKind.Source
                };

                runtime.State.Persistent.SetValue("persistent.key", "persisted");
                runtime.State.Workflow.Set("workflow.key", new WorkflowProbe("workflow"));
                runtime.State.Contexts.Set(runtime.Editor.CreateDocumentScope(context), "document.key", new ContextProbe("context"));

                var modulesField = typeof(CortexModuleRuntimeState).GetField("_modules", BindingFlags.Instance | BindingFlags.NonPublic);
                var modules = modulesField != null ? modulesField.GetValue(state.Modules) : null;

                Assert.NotNull(modules);
                Assert.Equal(1, CountDictionaryEntries(modules));

                var bucket = GetSingleDictionaryValue(modules);
                Assert.NotNull(bucket);
                Assert.Equal("persisted", ReadDictionaryValue<string>(bucket, "PersistentValues", "persistent.key"));
                Assert.Equal("workflow", ReadDictionaryValue<WorkflowProbe>(bucket, "WorkflowValues", "workflow.key").Value);

                var contextScopes = ReadDictionary(bucket, "ContextScopes");
                Assert.NotNull(contextScopes);
                Assert.Equal(1, CountDictionaryEntries(contextScopes));

                var shellFields = typeof(CortexShellState).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.DoesNotContain(shellFields, field => string.Equals(field.Name, "PersistentValues", StringComparison.Ordinal));
                Assert.DoesNotContain(shellFields, field => string.Equals(field.Name, "WorkflowValues", StringComparison.Ordinal));
                Assert.DoesNotContain(shellFields, field => string.Equals(field.Name, "ContextScopes", StringComparison.Ordinal));
            });
        }

        [Fact]
        public void PluginContributor_RegistersDedicatedWindowCommandsActionsAndExplorerFilters()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var state = new CortexShellState();
                state.Workbench.FocusedContainerId = "editor";
                var commandRegistry = new CommandRegistry();
                var contributionRegistry = new ContributionRegistry();
                var moduleRegistry = new CortexShellModuleContributionRegistry();
                var extensionRegistry = new WorkbenchExtensionRegistry();
                var runtimeAccess = new TestWorkbenchRuntimeAccess();
                var descriptor = new WorkbenchModuleDescriptor("module.review", "container.review", typeof(RecordingModule));
                runtimeAccess.Register(CreateModuleRuntime(state, descriptor));

                var contributor = new ReviewWorkbenchContributor();
                contributor.Register(new WorkbenchPluginContext(
                    commandRegistry,
                    contributionRegistry,
                    moduleRegistry,
                    extensionRegistry,
                    runtimeAccess));

                Assert.Contains(contributionRegistry.GetViewContainers(), container => string.Equals(container.ContainerId, "container.review", StringComparison.Ordinal));
                Assert.Contains(contributionRegistry.GetViews("container.review"), view => string.Equals(view.ViewId, "container.review.main", StringComparison.Ordinal));
                Assert.Contains(contributionRegistry.GetIcons(), icon => string.Equals(icon.IconId, "container.review", StringComparison.Ordinal));
                Assert.NotNull(moduleRegistry.FindContribution("container.review"));

                var filter = Assert.Single(contributionRegistry.GetExplorerFilters().Where(filterContribution =>
                    string.Equals(filterContribution.FilterId, "cortex.explorer.filter.review.documents", StringComparison.Ordinal)));
                var matcher = filter.CreateMatcher(new ExplorerFilterRuntimeContext
                {
                    Scope = ExplorerFilterScope.Workspace,
                    ActiveDocumentPath = @"D:\Mods\Alpha\Note.review.cs"
                });
                Assert.NotNull(matcher);
                Assert.True(matcher(new WorkspaceTreeNode
                {
                    NodeKind = WorkspaceTreeNodeKind.File,
                    FullPath = @"D:\Mods\Alpha\Note.review.cs"
                }));
                Assert.False(matcher(new WorkspaceTreeNode
                {
                    NodeKind = WorkspaceTreeNodeKind.File,
                    FullPath = @"D:\Mods\Alpha\Main.cs"
                }));

                var target = new EditorCommandTarget
                {
                    ContextId = EditorContextIds.Document,
                    DocumentPath = @"D:\Mods\Alpha\Note.review.cs"
                };
                var actions = new EditorContextActionResolverService().ResolveActions(
                    state,
                    commandRegistry,
                    contributionRegistry,
                    target,
                    EditorContextActionPlacement.ContextMenu);
                var action = Assert.Single(actions.Where(candidate => string.Equals(candidate.ActionId, "review.context.add-note", StringComparison.Ordinal)));
                Assert.True(action.Enabled);
                Assert.Equal("Add Review Note", action.Title);

                Assert.True(commandRegistry.Execute("review.note.add", new EditorCommandContextFactory().Build(state, target)));
                Assert.Equal(1, contributor.AddNoteInvocationCount);

                Assert.True(commandRegistry.Execute("review.window.open", new CommandExecutionContext()));
                Assert.Equal("container.review", state.Workbench.RequestedContainerId);
                Assert.Equal("Opened Review Notes.", runtimeAccess.GetStatusMessage());
            });
        }

        private static IWorkbenchModuleRuntime CreateModuleRuntime(CortexShellState state, WorkbenchModuleDescriptor descriptor)
        {
            return CreateModuleRuntime(state, descriptor, new TestWorkbenchRuntime(new CommandRegistry(), new ContributionRegistry()));
        }

        private static IWorkbenchModuleRuntime CreateModuleRuntime(CortexShellState state, WorkbenchModuleDescriptor descriptor, TestWorkbenchRuntime workbenchRuntime)
        {
            return CreateRuntimeFactory(state, workbenchRuntime).Create(descriptor);
        }

        private static WorkbenchModuleRuntimeFactory CreateRuntimeFactory(CortexShellState state, TestWorkbenchRuntime workbenchRuntime)
        {
            var services = new ShellServiceMap
            {
                ProjectCatalog = new InMemoryProjectCatalog(
                    new[]
                    {
                        new CortexProjectDefinition
                        {
                            ModId = "module.alpha",
                            SourceRootPath = @"D:\Mods\Alpha",
                            ProjectFilePath = @"D:\Mods\Alpha\Alpha.csproj"
                        }
                    }),
                LoadedModCatalog = new InMemoryLoadedModCatalog(
                    new List<LoadedModInfo>
                    {
                        new LoadedModInfo
                        {
                            ModId = "module.alpha",
                            DisplayName = "Alpha",
                            RootPath = @"D:\Mods\Alpha"
                        }
                    }),
                NavigationService = new FakeNavigationService(),
                EditorContextService = new EditorContextService(
                    new EditorService(),
                    new EditorCommandContextFactory(),
                    new EditorSymbolInteractionService())
            };

            return new WorkbenchModuleRuntimeFactory(state, services, delegate { return workbenchRuntime; });
        }

        private static ShellSessionCoordinator CreateSessionCoordinator(CortexShellState state, string persistencePath)
        {
            return new ShellSessionCoordinator(
                state,
                null,
                delegate { return null; },
                delegate { return null; },
                delegate { return null; },
                delegate { return new JsonWorkbenchPersistenceService(persistencePath); },
                delegate { return null; });
        }

        private static object ReadDictionary(object target, string fieldName)
        {
            var field = target != null
                ? target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                : null;
            return field != null ? field.GetValue(target) : null;
        }

        private static TValue ReadDictionaryValue<TValue>(object target, string fieldName, string key) where TValue : class
        {
            var dictionary = ReadDictionary(target, fieldName);
            object value;
            return TryGetDictionaryValue(dictionary, key, out value)
                ? value as TValue
                : null;
        }

        private static int CountDictionaryEntries(object dictionary)
        {
            var enumerable = dictionary as IEnumerable;
            if (enumerable == null)
            {
                return 0;
            }

            var count = 0;
            foreach (var entry in enumerable)
            {
                if (entry != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static object GetSingleDictionaryValue(object dictionary)
        {
            var enumerable = dictionary as IEnumerable;
            if (enumerable == null)
            {
                return null;
            }

            foreach (var entry in enumerable)
            {
                return GetDictionaryEntryValue(entry);
            }

            return null;
        }

        private static bool TryGetDictionaryValue(object dictionary, string key, out object value)
        {
            value = null;
            if (dictionary == null)
            {
                return false;
            }

            var nonGeneric = dictionary as IDictionary;
            if (nonGeneric != null)
            {
                if (!nonGeneric.Contains(key))
                {
                    return false;
                }

                value = nonGeneric[key];
                return true;
            }

            var containsKey = dictionary.GetType().GetMethod("ContainsKey", new[] { typeof(string) });
            if (containsKey != null)
            {
                var contains = containsKey.Invoke(dictionary, new object[] { key });
                if (!(contains is bool) || !((bool)contains))
                {
                    return false;
                }
            }

            var indexer = dictionary.GetType().GetProperty("Item", new[] { typeof(string) });
            if (indexer == null)
            {
                return false;
            }

            value = indexer.GetValue(dictionary, new object[] { key });
            return true;
        }

        private static object GetDictionaryEntryValue(object entry)
        {
            if (entry == null)
            {
                return null;
            }

            if (entry is DictionaryEntry)
            {
                return ((DictionaryEntry)entry).Value;
            }

            var property = entry.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);
            return property != null ? property.GetValue(entry, null) : null;
        }

        private sealed class RecordingModuleContribution : IWorkbenchModuleContribution
        {
            public RecordingModuleContribution(string moduleId, string containerId)
            {
                Descriptor = new WorkbenchModuleDescriptor(moduleId, containerId, typeof(RecordingModule));
            }

            public WorkbenchModuleDescriptor Descriptor { get; private set; }

            public IWorkbenchModuleRuntime CapturedRuntime { get; private set; }

            public RecordingModule Module { get; private set; }

            public IWorkbenchModule CreateModule(IWorkbenchModuleRuntime runtime)
            {
                CapturedRuntime = runtime;
                Module = new RecordingModule();
                return Module;
            }
        }

        private sealed class ReviewWorkbenchContributor : IWorkbenchPluginContributor
        {
            public int AddNoteInvocationCount;

            public string PluginId
            {
                get { return "module.review"; }
            }

            public string DisplayName
            {
                get { return "Review Notes"; }
            }

            public void Register(WorkbenchPluginContext context)
            {
                context.RegisterViewContainer(
                    "container.review",
                    "Review Notes",
                    WorkbenchHostLocation.SecondarySideHost,
                    25,
                    true,
                    ModuleActivationKind.OnCommand,
                    "review.window.open",
                    "container.review");
                context.RegisterView(
                    "container.review.main",
                    "container.review",
                    "Review Notes",
                    "container.review.main",
                    0,
                    true);
                context.RegisterIcon(new IconContribution
                {
                    IconId = "container.review",
                    Alias = "RV"
                });
                context.RegisterModule(new RecordingModuleContribution("module.review", "container.review"));

                context.RegisterCommand(
                    "review.window.open",
                    "Open Review Notes",
                    "Review",
                    "Open the review notes workbench window.",
                    string.Empty,
                    10,
                    true,
                    true);
                context.RegisterCommandHandler(
                    "review.window.open",
                    delegate(CommandExecutionContext executionContext)
                    {
                        var moduleRuntime = context.Runtime != null ? context.Runtime.Modules.GetByContainer("container.review") : null;
                        if (moduleRuntime != null)
                        {
                            moduleRuntime.Lifecycle.RequestContainer("container.review", WorkbenchHostLocation.SecondarySideHost);
                        }

                        if (context.Runtime != null)
                        {
                            context.Runtime.Feedback.SetStatusMessage("Opened Review Notes.");
                        }
                    },
                    delegate(CommandExecutionContext executionContext)
                    {
                        return context.Runtime != null && context.Runtime.Modules.GetByContainer("container.review") != null;
                    });

                context.RegisterCommand(
                    "review.note.add",
                    "Add Review Note",
                    "Review",
                    "Capture a review note for the current document.",
                    string.Empty,
                    20,
                    true,
                    false);
                context.RegisterCommandHandler(
                    "review.note.add",
                    delegate(CommandExecutionContext executionContext)
                    {
                        if (executionContext != null && executionContext.Parameter is EditorCommandTarget)
                        {
                            AddNoteInvocationCount++;
                        }
                    },
                    delegate(CommandExecutionContext executionContext)
                    {
                        return executionContext != null && executionContext.Parameter is EditorCommandTarget;
                    });

                context.RegisterEditorContextAction(
                    "review.context.add-note",
                    "review.note.add",
                    EditorContextIds.Document,
                    "review",
                    10,
                    EditorContextActionPlacement.ContextMenu | EditorContextActionPlacement.ActionBar,
                    string.Empty,
                    true,
                    false,
                    "Add Review Note",
                    "Capture a review note for the current document.");

                context.ContributionRegistry.RegisterExplorerFilter(new ExplorerFilterContribution
                {
                    FilterId = "cortex.explorer.filter.review.documents",
                    DisplayName = "Review Documents",
                    Description = "Show only files that hold review notes.",
                    Scope = ExplorerFilterScope.Workspace,
                    SortOrder = 30,
                    CreateMatcher = delegate(ExplorerFilterRuntimeContext runtimeContext)
                    {
                        return delegate(WorkspaceTreeNode node)
                        {
                            return node != null &&
                                node.NodeKind == WorkspaceTreeNodeKind.File &&
                                !string.IsNullOrEmpty(node.FullPath) &&
                                node.FullPath.EndsWith(".review.cs", StringComparison.OrdinalIgnoreCase);
                        };
                    }
                });
            }
        }

        private sealed class RecordingModule : IWorkbenchModule
        {
            public IWorkbenchModuleRuntime RenderRuntime { get; private set; }

            public string GetUnavailableMessage()
            {
                return string.Empty;
            }

            public void Render(WorkbenchModuleRenderContext context, bool detachedWindow)
            {
                RenderRuntime = context != null ? context.Runtime : null;
            }
        }

        private sealed class WorkflowProbe
        {
            public WorkflowProbe(string value)
            {
                Value = value;
            }

            public string Value { get; private set; }
        }

        private sealed class ContextProbe
        {
            public ContextProbe(string value)
            {
                Value = value;
            }

            public string Value { get; private set; }
        }

        private sealed class InMemoryProjectCatalog : IProjectCatalog
        {
            private readonly Dictionary<string, CortexProjectDefinition> _projects = new Dictionary<string, CortexProjectDefinition>(StringComparer.OrdinalIgnoreCase);

            public InMemoryProjectCatalog(IEnumerable<CortexProjectDefinition> definitions)
            {
                if (definitions == null)
                {
                    return;
                }

                foreach (var definition in definitions)
                {
                    if (definition != null && !string.IsNullOrEmpty(definition.ModId))
                    {
                        _projects[definition.ModId] = definition;
                    }
                }
            }

            public IList<CortexProjectDefinition> GetProjects()
            {
                return new List<CortexProjectDefinition>(_projects.Values);
            }

            public CortexProjectDefinition GetProject(string modId)
            {
                CortexProjectDefinition definition;
                return !string.IsNullOrEmpty(modId) && _projects.TryGetValue(modId, out definition)
                    ? definition
                    : null;
            }

            public void Upsert(CortexProjectDefinition definition)
            {
                if (definition != null && !string.IsNullOrEmpty(definition.ModId))
                {
                    _projects[definition.ModId] = definition;
                }
            }

            public void Remove(string modId)
            {
                if (!string.IsNullOrEmpty(modId))
                {
                    _projects.Remove(modId);
                }
            }
        }

        private sealed class FakeNavigationService : ICortexNavigationService
        {
            public DocumentSession OpenDocument(CortexShellState state, string filePath, int highlightedLine, string successStatusMessage, string failureStatusMessage)
            {
                return string.IsNullOrEmpty(filePath) ? null : new DocumentSession { FilePath = filePath, HighlightedLine = highlightedLine };
            }

            public void PreloadDocument(CortexShellState state, string filePath)
            {
            }

            public void PreloadHoverResponseTarget(CortexShellState state, LanguageService.Protocol.LanguageServiceHoverResponse response)
            {
            }

            public void PreloadHoverDisplayPartTarget(CortexShellState state, LanguageService.Protocol.LanguageServiceHoverDisplayPart part)
            {
            }

            public void PreloadHoverNavigationTarget(CortexShellState state, EditorHoverNavigationTarget target)
            {
            }

            public DecompilerResponse RequestDecompilerSource(CortexShellState state, string assemblyPath, int metadataToken, DecompilerEntityKind entityKind, bool ignoreCache)
            {
                return new DecompilerResponse
                {
                    SourceText = string.Empty
                };
            }

            public bool OpenDecompilerResult(CortexShellState state, DecompilerResponse response, string successStatusMessage, string failureStatusMessage)
            {
                return response != null;
            }

            public bool OpenDecompilerResult(CortexShellState state, DecompilerResponse response, int highlightedLine, string successStatusMessage, string failureStatusMessage)
            {
                return response != null;
            }

            public bool DecompileAndOpen(CortexShellState state, string assemblyPath, int metadataToken, DecompilerEntityKind entityKind, bool ignoreCache, string successStatusMessage, string failureStatusMessage)
            {
                return true;
            }

            public bool OpenDecompilerMethodTarget(CortexShellState state, string assemblyPath, int methodMetadataToken, string metadataName, string containingTypeName, string symbolKind, bool ignoreCache, string successStatusMessage, string failureStatusMessage)
            {
                return true;
            }

            public DecompilerResponse RequestDecompilerMethodView(CortexShellState state, string assemblyPath, int methodMetadataToken, string metadataName, string containingTypeName, string symbolKind, bool ignoreCache, out int highlightedLine)
            {
                highlightedLine = 1;
                return new DecompilerResponse
                {
                    SourceText = string.Empty
                };
            }

            public SourceNavigationTarget ResolveRuntimeTarget(RuntimeLogEntry entry, int frameIndex, CortexShellState state)
            {
                return new SourceNavigationTarget
                {
                    Success = true,
                    FilePath = string.Empty
                };
            }

            public bool OpenRuntimeTarget(CortexShellState state, SourceNavigationTarget target, string successStatusMessage, string failureStatusMessage)
            {
                return target != null && target.Success;
            }

            public bool OpenHoverDisplayPart(CortexShellState state, LanguageService.Protocol.LanguageServiceHoverDisplayPart part, string successStatusMessage, string failureStatusMessage)
            {
                return true;
            }

            public bool OpenHoverNavigationTarget(CortexShellState state, EditorHoverNavigationTarget target, string successStatusMessage, string failureStatusMessage)
            {
                return target != null;
            }

            public bool OpenLanguageSymbolTarget(CortexShellState state, string symbolDisplay, string symbolKind, string metadataName, string containingTypeName, string containingAssemblyName, string documentationCommentId, string definitionDocumentPath, LanguageService.Protocol.LanguageServiceRange definitionRange, string successStatusMessage, string failureStatusMessage)
            {
                return true;
            }
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
    }
}
