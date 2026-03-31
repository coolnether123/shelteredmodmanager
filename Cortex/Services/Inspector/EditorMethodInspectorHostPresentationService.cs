using System;
using System.Collections.Generic;
using Cortex.Core.Models;
using Cortex.Plugins.Abstractions;
using Cortex.Presentation.Models;
using Cortex.Services.Inspector.Actions;
using Cortex.Services.Inspector.Identity;
using Cortex.Services.Inspector.Lifecycle;
using Cortex.Services.Inspector.Relationships;
using Cortex.Services.Semantics.Context;

namespace Cortex.Services.Inspector
{
    internal sealed class EditorMethodInspectorHostPresentationService
    {
        private const int StructureSortOrder = 100;
        private const int RelationshipsSortOrder = 200;
        private const int SourceSortOrder = 400;

        private readonly IEditorContextService _contextService;
        private readonly EditorMethodInspectorService _inspectorService;
        private readonly EditorMethodRelationshipsContextService _relationshipsContextService;
        private readonly IEditorMethodTargetContextEnricher _targetContextEnricher;
        private readonly EditorMethodInspectorHostViewComposer _viewComposer;

        public EditorMethodInspectorHostPresentationService(IEditorContextService contextService)
            : this(
                contextService,
                new EditorMethodRelationshipsContextService(),
                new EditorMethodTargetContextEnricher(contextService),
                new EditorMethodInspectorHostViewComposer(new EditorMethodInspectorNavigationActionFactory()))
        {
        }

        internal EditorMethodInspectorHostPresentationService(
            IEditorContextService contextService,
            EditorMethodRelationshipsContextService relationshipsContextService,
            IEditorMethodTargetContextEnricher targetContextEnricher,
            EditorMethodInspectorHostViewComposer viewComposer)
        {
            _contextService = contextService;
            _inspectorService = new EditorMethodInspectorService(contextService);
            _relationshipsContextService = relationshipsContextService;
            _targetContextEnricher = targetContextEnricher;
            _viewComposer = viewComposer;
        }

        public EditorMethodInspectorPreparedView Prepare(
            CortexShellState state,
            DocumentSession session,
            IWorkbenchExtensionRegistry extensionRegistry,
            IWorkbenchRuntimeAccess runtimeAccess)
        {
            var inspector = state != null && state.Editor != null ? state.Editor.MethodInspector : null;
            var editorContext = _contextService != null
                ? _contextService.GetContext(state, inspector != null ? inspector.ContextKey : string.Empty)
                : null;
            var invocation = _contextService != null
                ? _contextService.ResolveInvocation(state, inspector != null ? inspector.ContextKey : string.Empty)
                : null;
            var target = invocation != null ? invocation.Target : null;
            if (target == null)
            {
                _inspectorService.Close(state);
                return null;
            }

            _targetContextEnricher.EnsureSymbolContextRequest(state, target);
            _targetContextEnricher.Enrich(target, session, state);

            if (inspector != null && inspector.RelationshipsExpanded)
            {
                _inspectorService.EnsureRelationshipsRequest(state);
            }

            var relationshipsContext = _relationshipsContextService.BuildContext(inspector, target);
            var augmentationContext = new WorkbenchMethodInspectorContext(
                session,
                editorContext,
                invocation,
                BuildRelationshipsSnapshot(relationshipsContext),
                runtimeAccess);
            ApplyRelationshipAugmentations(relationshipsContext, augmentationContext, extensionRegistry);

            var relationshipsSnapshot = BuildRelationshipsSnapshot(relationshipsContext);
            var contributionContext = new WorkbenchMethodInspectorContext(
                session,
                editorContext,
                invocation,
                relationshipsSnapshot,
                runtimeAccess);

            ApplyRelationshipActions(relationshipsContext, contributionContext, extensionRegistry);

            var contributedSections = BuildContributedSections(inspector, contributionContext, extensionRegistry);
            var contributedModels = new MethodInspectorSectionViewModel[contributedSections.Count];
            for (var i = 0; i < contributedSections.Count; i++)
            {
                contributedModels[i] = contributedSections[i].Section;
            }

            var viewModel = _viewComposer.Compose(inspector, session, target, relationshipsContext, contributedModels);
            if (viewModel == null)
            {
                return null;
            }

            viewModel.Sections = OrderSections(viewModel.Sections, contributedSections);
            return new EditorMethodInspectorPreparedView
            {
                Inspector = inspector,
                EditorContext = editorContext,
                Invocation = invocation,
                Relationships = relationshipsSnapshot,
                Session = session,
                Target = target,
                ViewModel = viewModel
            };
        }

        private static void ApplyRelationshipAugmentations(
            EditorMethodRelationshipsContext relationshipsContext,
            WorkbenchMethodInspectorContext contributionContext,
            IWorkbenchExtensionRegistry extensionRegistry)
        {
            if (relationshipsContext == null)
            {
                return;
            }

            var contributions = extensionRegistry != null
                ? extensionRegistry.GetMethodRelationshipAugmentations()
                : new List<WorkbenchMethodRelationshipAugmentationContribution>();
            if (contributions == null || contributions.Count == 0)
            {
                return;
            }

            var incoming = new List<EditorMethodRelationshipItem>(relationshipsContext.IncomingCalls ?? new EditorMethodRelationshipItem[0]);
            var outgoing = new List<EditorMethodRelationshipItem>(relationshipsContext.OutgoingCalls ?? new EditorMethodRelationshipItem[0]);

            for (var i = 0; i < contributions.Count; i++)
            {
                var contribution = contributions[i];
                if (contribution == null)
                {
                    continue;
                }

                AppendRelationshipAugmentations(
                    incoming,
                    contribution.BuildIncomingRelationships != null
                        ? contribution.BuildIncomingRelationships(contributionContext)
                        : null);
                AppendRelationshipAugmentations(
                    outgoing,
                    contribution.BuildOutgoingRelationships != null
                        ? contribution.BuildOutgoingRelationships(contributionContext)
                        : null);
            }

            relationshipsContext.IncomingCalls = incoming.ToArray();
            relationshipsContext.OutgoingCalls = outgoing.ToArray();
            relationshipsContext.IncomingCallCount = relationshipsContext.IncomingCalls.Length;
            relationshipsContext.OutgoingCallCount = relationshipsContext.OutgoingCalls.Length;
        }

        private static void ApplyRelationshipActions(
            EditorMethodRelationshipsContext relationshipsContext,
            WorkbenchMethodInspectorContext contributionContext,
            IWorkbenchExtensionRegistry extensionRegistry)
        {
            if (relationshipsContext == null)
            {
                return;
            }

            var contributions = extensionRegistry != null
                ? extensionRegistry.GetMethodRelationshipActions()
                : new List<WorkbenchMethodRelationshipActionContribution>();
            ApplyRelationshipActions(relationshipsContext.IncomingCalls, contributionContext, contributions);
            ApplyRelationshipActions(relationshipsContext.OutgoingCalls, contributionContext, contributions);
        }

        private static void ApplyRelationshipActions(
            EditorMethodRelationshipItem[] items,
            WorkbenchMethodInspectorContext contributionContext,
            IList<WorkbenchMethodRelationshipActionContribution> contributions)
        {
            if (items == null || items.Length == 0)
            {
                return;
            }

            for (var i = 0; i < items.Length; i++)
            {
                var item = items[i];
                if (item == null)
                {
                    continue;
                }

                item.Actions = BuildRelationshipActions(item, contributionContext, contributions);
            }
        }

        private static MethodInspectorActionViewModel[] BuildRelationshipActions(
            EditorMethodRelationshipItem item,
            WorkbenchMethodInspectorContext contributionContext,
            IList<WorkbenchMethodRelationshipActionContribution> contributions)
        {
            if (item == null)
            {
                return new MethodInspectorActionViewModel[0];
            }

            var context = new WorkbenchMethodRelationshipActionContext(
                new WorkbenchMethodRelationship
                {
                    Title = item.Title ?? string.Empty,
                    Detail = item.Detail ?? string.Empty,
                    SymbolKind = item.SymbolKind ?? string.Empty,
                    MetadataName = item.MetadataName ?? string.Empty,
                    ContainingTypeName = item.ContainingTypeName ?? string.Empty,
                    ContainingAssemblyName = item.ContainingAssemblyName ?? string.Empty,
                    DocumentationCommentId = item.DocumentationCommentId ?? string.Empty,
                    DefinitionDocumentPath = item.DefinitionDocumentPath ?? string.Empty,
                    DefinitionRange = item.DefinitionRange,
                    Relationship = item.Relationship ?? string.Empty,
                    CallCount = item.CallCount
                },
                contributionContext != null ? contributionContext.Session : null,
                contributionContext != null ? contributionContext.EditorContext : null,
                contributionContext != null ? contributionContext.Invocation : null,
                contributionContext != null ? contributionContext.Relationships : null,
                contributionContext != null ? contributionContext.Runtime : null);

            if (contributions == null)
            {
                return new MethodInspectorActionViewModel[0];
            }

            for (var i = 0; i < contributions.Count; i++)
            {
                var contribution = contributions[i];
                if (contribution == null || contribution.BuildActions == null)
                {
                    continue;
                }

                var actions = contribution.BuildActions(context);
                if (actions != null && actions.Length > 0)
                {
                    return actions;
                }
            }

            return new MethodInspectorActionViewModel[0];
        }

        private static void AppendRelationshipAugmentations(
            List<EditorMethodRelationshipItem> items,
            WorkbenchMethodRelationship[] augmentations)
        {
            if (items == null || augmentations == null || augmentations.Length == 0)
            {
                return;
            }

            for (var i = 0; i < augmentations.Length; i++)
            {
                var augmentation = augmentations[i];
                if (augmentation == null)
                {
                    continue;
                }

                EditorMethodRelationshipSet.AddDistinct(items, new EditorMethodRelationshipItem
                {
                    Title = augmentation.Title ?? string.Empty,
                    Detail = augmentation.Detail ?? string.Empty,
                    SymbolKind = augmentation.SymbolKind ?? string.Empty,
                    MetadataName = augmentation.MetadataName ?? string.Empty,
                    ContainingTypeName = augmentation.ContainingTypeName ?? string.Empty,
                    ContainingAssemblyName = augmentation.ContainingAssemblyName ?? string.Empty,
                    DocumentationCommentId = augmentation.DocumentationCommentId ?? string.Empty,
                    DefinitionDocumentPath = augmentation.DefinitionDocumentPath ?? string.Empty,
                    DefinitionRange = augmentation.DefinitionRange,
                    Relationship = augmentation.Relationship ?? string.Empty,
                    CallCount = augmentation.CallCount > 0 ? augmentation.CallCount : 1
                });
            }
        }

        private static List<OrderedSectionEntry> BuildContributedSections(
            CortexMethodInspectorState inspector,
            WorkbenchMethodInspectorContext context,
            IWorkbenchExtensionRegistry extensionRegistry)
        {
            var contributions = extensionRegistry != null
                ? extensionRegistry.GetMethodInspectorSections()
                : new List<WorkbenchMethodInspectorSectionContribution>();
            var results = new List<OrderedSectionEntry>();
            for (var i = 0; i < contributions.Count; i++)
            {
                var contribution = contributions[i];
                if (contribution == null)
                {
                    continue;
                }

                if (contribution.CanDisplay != null && !contribution.CanDisplay(context))
                {
                    continue;
                }

                var section = contribution.BuildSection != null
                    ? contribution.BuildSection(context)
                    : null;
                if (section == null)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(section.Id))
                {
                    section.Id = contribution.ContributionId ?? string.Empty;
                }

                section.Expanded = EditorMethodInspectorService.ResolveSectionExpansion(
                    inspector,
                    section.Id,
                    contribution.DefaultExpanded);
                results.Add(new OrderedSectionEntry
                {
                    Section = section,
                    SortOrder = contribution.SortOrder
                });
            }

            return results;
        }

        private static MethodInspectorSectionViewModel[] OrderSections(
            MethodInspectorSectionViewModel[] sections,
            IList<OrderedSectionEntry> contributedSections)
        {
            var contributedSortOrders = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (contributedSections != null)
            {
                for (var i = 0; i < contributedSections.Count; i++)
                {
                    var section = contributedSections[i] != null ? contributedSections[i].Section : null;
                    if (section != null && !string.IsNullOrEmpty(section.Id))
                    {
                        contributedSortOrders[section.Id] = contributedSections[i].SortOrder;
                    }
                }
            }

            var entries = new List<OrderedSectionEntry>();
            var safeSections = sections ?? new MethodInspectorSectionViewModel[0];
            for (var i = 0; i < safeSections.Length; i++)
            {
                var section = safeSections[i];
                if (section == null)
                {
                    continue;
                }

                entries.Add(new OrderedSectionEntry
                {
                    Section = section,
                    SortOrder = ResolveSortOrder(section.Id, contributedSortOrders)
                });
            }

            entries.Sort(delegate(OrderedSectionEntry left, OrderedSectionEntry right)
            {
                var order = left.SortOrder.CompareTo(right.SortOrder);
                return order != 0
                    ? order
                    : string.Compare(left.Section.Id, right.Section.Id, StringComparison.OrdinalIgnoreCase);
            });

            var ordered = new MethodInspectorSectionViewModel[entries.Count];
            for (var i = 0; i < entries.Count; i++)
            {
                ordered[i] = entries[i].Section;
            }

            return ordered;
        }

        private static int ResolveSortOrder(string sectionId, IDictionary<string, int> contributedSortOrders)
        {
            if (!string.IsNullOrEmpty(sectionId))
            {
                if (string.Equals(sectionId, "structure", StringComparison.OrdinalIgnoreCase))
                {
                    return StructureSortOrder;
                }

                if (string.Equals(sectionId, "relationships", StringComparison.OrdinalIgnoreCase))
                {
                    return RelationshipsSortOrder;
                }

                if (string.Equals(sectionId, "source", StringComparison.OrdinalIgnoreCase))
                {
                    return SourceSortOrder;
                }

                int contributionSortOrder;
                if (contributedSortOrders != null &&
                    contributedSortOrders.TryGetValue(sectionId, out contributionSortOrder))
                {
                    return contributionSortOrder;
                }
            }

            return SourceSortOrder - 1;
        }

        private static WorkbenchMethodRelationshipsSnapshot BuildRelationshipsSnapshot(EditorMethodRelationshipsContext context)
        {
            var snapshot = new WorkbenchMethodRelationshipsSnapshot();
            if (context == null)
            {
                return snapshot;
            }

            snapshot.IsExpanded = context.IsExpanded;
            snapshot.IsLoading = context.IsLoading;
            snapshot.HasResponse = context.HasResponse;
            snapshot.StatusMessage = context.StatusMessage ?? string.Empty;
            snapshot.IncomingCallCount = context.IncomingCallCount;
            snapshot.OutgoingCallCount = context.OutgoingCallCount;
            snapshot.IncomingCalls = CloneRelationships(context.IncomingCalls);
            snapshot.OutgoingCalls = CloneRelationships(context.OutgoingCalls);
            return snapshot;
        }

        private static WorkbenchMethodRelationship[] CloneRelationships(EditorMethodRelationshipItem[] items)
        {
            var safeItems = items ?? new EditorMethodRelationshipItem[0];
            var results = new List<WorkbenchMethodRelationship>();
            for (var i = 0; i < safeItems.Length; i++)
            {
                var item = safeItems[i];
                if (item == null)
                {
                    continue;
                }

                results.Add(new WorkbenchMethodRelationship
                {
                    Title = item.Title ?? string.Empty,
                    Detail = item.Detail ?? string.Empty,
                    SymbolKind = item.SymbolKind ?? string.Empty,
                    MetadataName = item.MetadataName ?? string.Empty,
                    ContainingTypeName = item.ContainingTypeName ?? string.Empty,
                    ContainingAssemblyName = item.ContainingAssemblyName ?? string.Empty,
                    DocumentationCommentId = item.DocumentationCommentId ?? string.Empty,
                    DefinitionDocumentPath = item.DefinitionDocumentPath ?? string.Empty,
                    DefinitionRange = item.DefinitionRange,
                    Relationship = item.Relationship ?? string.Empty,
                    CallCount = item.CallCount
                });
            }

            return results.ToArray();
        }

        private sealed class OrderedSectionEntry
        {
            public MethodInspectorSectionViewModel Section;
            public int SortOrder;
        }
    }
}
