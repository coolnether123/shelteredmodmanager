using System;
using System.Collections.Generic;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Services.Harmony.Inspection;

namespace Cortex.Modules.Harmony
{
    internal static class HarmonyExplorerFilterContributions
    {
        public static void EnsureRegistered(IContributionRegistry contributionRegistry, IHarmonyFeatureServices services)
        {
            if (contributionRegistry == null || services == null)
            {
                return;
            }

            contributionRegistry.RegisterExplorerFilter(new ExplorerFilterContribution
            {
                FilterId = ExplorerFilterWellKnownIds.HarmonyPatched,
                DisplayName = "Harmony Patched",
                Description = "Show only decompiler nodes patched by the selected workspace. Falls back to all runtime patches when no project is selected.",
                Scope = ExplorerFilterScope.Decompiler,
                SortOrder = 100,
                CreateMatcher = delegate(ExplorerFilterRuntimeContext context)
                {
                    return BuildPatchedMatcher(services, context);
                }
            });
        }

        private static ExplorerNodeMatcher BuildPatchedMatcher(IHarmonyFeatureServices services, ExplorerFilterRuntimeContext context)
        {
            if (services == null ||
                services.State == null ||
                services.HarmonyPatchInspectionService == null ||
                context == null ||
                context.Scope != ExplorerFilterScope.Decompiler)
            {
                return null;
            }

            EnsureSnapshotLoaded(services);

            var snapshotMethods = services.State.Harmony != null
                ? services.State.Harmony.SnapshotMethods ?? new HarmonyMethodPatchSummary[0]
                : new HarmonyMethodPatchSummary[0];
            return BuildPatchedNodeMatcher(snapshotMethods, context);
        }

        internal static ExplorerNodeMatcher BuildPatchedNodeMatcher(HarmonyMethodPatchSummary[] snapshotMethods)
        {
            return BuildPatchedNodeMatcher(snapshotMethods, null);
        }

        internal static ExplorerNodeMatcher BuildPatchedNodeMatcher(HarmonyMethodPatchSummary[] snapshotMethods, ExplorerFilterRuntimeContext context)
        {
            var assemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var namespaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var types = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var members = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            snapshotMethods = snapshotMethods ?? new HarmonyMethodPatchSummary[0];
            for (var i = 0; i < snapshotMethods.Length; i++)
            {
                var summary = snapshotMethods[i];
                if (summary == null ||
                    !summary.IsPatched ||
                    string.IsNullOrEmpty(summary.AssemblyPath) ||
                    !ShouldIncludeSummary(summary, context))
                {
                    continue;
                }

                assemblies.Add(summary.AssemblyPath);

                var normalizedTypeName = NormalizeTypeName(summary.DeclaringType);
                if (!string.IsNullOrEmpty(normalizedTypeName))
                {
                    types.Add(BuildScopedKey(summary.AssemblyPath, normalizedTypeName));
                    AddNamespacePrefixes(namespaces, summary.AssemblyPath, normalizedTypeName);
                }

                if (summary.Target != null && summary.Target.MetadataToken > 0)
                {
                    members.Add(BuildMemberKey(summary.AssemblyPath, summary.Target.MetadataToken));
                }
            }

            return delegate(WorkspaceTreeNode node)
            {
                return MatchesNode(node, assemblies, namespaces, types, members);
            };
        }

        private static bool ShouldIncludeSummary(HarmonyMethodPatchSummary summary, ExplorerFilterRuntimeContext context)
        {
            if (summary == null)
            {
                return false;
            }

            if (context == null || !context.RestrictToSelectedProject || context.SelectedProject == null)
            {
                return true;
            }

            return HarmonyPatchOwnerAssociationMatcher.SummaryMatchesSelectedProject(summary, context.SelectedProject);
        }

        private static void EnsureSnapshotLoaded(IHarmonyFeatureServices services)
        {
            if (services == null || services.State == null || services.State.Harmony == null || services.HarmonyPatchInspectionService == null)
            {
                return;
            }

            if (services.State.Harmony.RefreshRequested || services.State.Harmony.SnapshotUtc == DateTime.MinValue)
            {
                services.HarmonyPatchInspectionService.RefreshSnapshot(
                    services.State,
                    services.LoadedModCatalog,
                    services.ProjectCatalog);
            }
        }

        private static bool MatchesNode(
            WorkspaceTreeNode node,
            HashSet<string> assemblies,
            HashSet<string> namespaces,
            HashSet<string> types,
            HashSet<string> members)
        {
            if (node == null)
            {
                return false;
            }

            switch (node.NodeKind)
            {
                case WorkspaceTreeNodeKind.DecompilerRoot:
                    return assemblies.Count > 0;
                case WorkspaceTreeNodeKind.Assembly:
                    return assemblies.Contains(node.AssemblyPath ?? string.Empty);
                case WorkspaceTreeNodeKind.Folder:
                    return namespaces.Contains(BuildScopedKey(node.AssemblyPath, node.RelativePath));
                case WorkspaceTreeNodeKind.Type:
                    return types.Contains(BuildScopedKey(node.AssemblyPath, NormalizeTypeName(node.TypeName)));
                case WorkspaceTreeNodeKind.Member:
                    return members.Contains(BuildMemberKey(node.AssemblyPath, node.MetadataToken));
                default:
                    return false;
            }
        }

        private static void AddNamespacePrefixes(HashSet<string> namespaces, string assemblyPath, string declaringTypeName)
        {
            if (namespaces == null || string.IsNullOrEmpty(assemblyPath) || string.IsNullOrEmpty(declaringTypeName))
            {
                return;
            }

            var namespacePath = GetNamespacePath(declaringTypeName);
            if (string.IsNullOrEmpty(namespacePath))
            {
                return;
            }

            var segments = namespacePath.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            var current = string.Empty;
            for (var i = 0; i < segments.Length; i++)
            {
                current = string.IsNullOrEmpty(current)
                    ? segments[i]
                    : current + "." + segments[i];
                namespaces.Add(BuildScopedKey(assemblyPath, current));
            }
        }

        private static string GetNamespacePath(string declaringTypeName)
        {
            if (string.IsNullOrEmpty(declaringTypeName))
            {
                return string.Empty;
            }

            var lastDot = declaringTypeName.LastIndexOf('.');
            return lastDot > 0
                ? declaringTypeName.Substring(0, lastDot)
                : string.Empty;
        }

        private static string NormalizeTypeName(string typeName)
        {
            return string.IsNullOrEmpty(typeName)
                ? string.Empty
                : typeName.Replace('+', '.');
        }

        private static string BuildScopedKey(string assemblyPath, string name)
        {
            return (assemblyPath ?? string.Empty) + "|" + (name ?? string.Empty);
        }

        private static string BuildMemberKey(string assemblyPath, int metadataToken)
        {
            return (assemblyPath ?? string.Empty) + "|" + metadataToken.ToString();
        }

    }
}
