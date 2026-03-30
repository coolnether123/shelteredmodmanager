using System;
using System.Collections.Generic;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;

namespace Cortex.Services.Explorer
{
    /// <summary>
    /// Builds the explorer-facing filter plan from shared shell state and module
    /// contributions so explorer UI code stays agnostic of feature-specific rules.
    /// </summary>
    internal sealed class ExplorerFilterPlanBuilder
    {
        public ExplorerFilterPlan Build(IContributionRegistry contributionRegistry, CortexShellState state, ExplorerFilterScope scope)
        {
            return new ExplorerFilterPlan(
                contributionRegistry != null ? contributionRegistry.GetExplorerFilters() : new List<ExplorerFilterContribution>(),
                state != null && state.Explorer != null ? state.Explorer.FilterText : string.Empty,
                state != null && state.Explorer != null ? state.Explorer.ActiveFilterIds : null,
                new ExplorerFilterRuntimeContext
                {
                    Scope = scope,
                    ActiveDocumentPath = state != null && state.Documents != null ? state.Documents.ActiveDocumentPath ?? string.Empty : string.Empty,
                    HoveredDefinitionDocumentPath = state != null && state.EditorContext != null ? state.EditorContext.HoveredDefinitionDocumentPath ?? string.Empty : string.Empty,
                    SelectedProject = state != null ? state.SelectedProject : null,
                    Settings = state != null ? state.Settings : null,
                    RestrictToSelectedProject = state != null &&
                        state.Explorer != null &&
                        state.Explorer.ScopeMode == CortexExplorerScopeMode.CurrentMod
                });
        }
    }

    internal sealed class ExplorerFilterPlan
    {
        private readonly string _filterText;
        private readonly HashSet<string> _activeFilterIds;
        private readonly Dictionary<string, ExplorerNodeMatcher> _availableMatchers;
        private readonly List<ExplorerNodeMatcher> _activeMatchers;
        private readonly List<ExplorerFilterContribution> _contributions;

        public ExplorerFilterPlan(
            IList<ExplorerFilterContribution> contributions,
            string filterText,
            ICollection<string> activeFilterIds,
            ExplorerFilterRuntimeContext runtimeContext)
        {
            _filterText = filterText ?? string.Empty;
            _activeFilterIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _availableMatchers = new Dictionary<string, ExplorerNodeMatcher>(StringComparer.OrdinalIgnoreCase);
            _activeMatchers = new List<ExplorerNodeMatcher>();
            _contributions = new List<ExplorerFilterContribution>();

            if (activeFilterIds != null)
            {
                foreach (var filterId in activeFilterIds)
                {
                    if (!string.IsNullOrEmpty(filterId))
                    {
                        _activeFilterIds.Add(filterId);
                    }
                }
            }

            if (contributions == null)
            {
                return;
            }

            for (var i = 0; i < contributions.Count; i++)
            {
                var contribution = contributions[i];
                if (contribution == null ||
                    string.IsNullOrEmpty(contribution.FilterId) ||
                    !AppliesToScope(contribution.Scope, runtimeContext != null ? runtimeContext.Scope : ExplorerFilterScope.All))
                {
                    continue;
                }

                _contributions.Add(contribution);

                ExplorerNodeMatcher matcher = null;
                if (contribution.CreateMatcher != null)
                {
                    matcher = contribution.CreateMatcher(runtimeContext);
                }

                if (matcher != null)
                {
                    _availableMatchers[contribution.FilterId] = matcher;
                    if (_activeFilterIds.Contains(contribution.FilterId))
                    {
                        _activeMatchers.Add(matcher);
                    }
                }
            }
        }

        public IList<ExplorerFilterContribution> Contributions
        {
            get { return _contributions; }
        }

        public bool HasAnyFilter
        {
            get { return !string.IsNullOrEmpty(_filterText) || _activeMatchers.Count > 0; }
        }

        public bool IsFilterAvailable(string filterId)
        {
            return !string.IsNullOrEmpty(filterId) && _availableMatchers.ContainsKey(filterId);
        }

        public bool IsFilterActive(string filterId)
        {
            return !string.IsNullOrEmpty(filterId) && _activeFilterIds.Contains(filterId);
        }

        public bool Matches(WorkspaceTreeNode node)
        {
            return MatchesNodeOrDescendant(node);
        }

        private bool MatchesNodeOrDescendant(WorkspaceTreeNode node)
        {
            if (node == null)
            {
                return false;
            }

            if (MatchesNode(node))
            {
                return true;
            }

            if (!node.HasChildren || !node.ChildrenLoaded)
            {
                return false;
            }

            for (var i = 0; i < node.Children.Count; i++)
            {
                if (MatchesNodeOrDescendant(node.Children[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private bool MatchesNode(WorkspaceTreeNode node)
        {
            if (node == null || !MatchesText(node))
            {
                return false;
            }

            for (var i = 0; i < _activeMatchers.Count; i++)
            {
                if (!_activeMatchers[i](node))
                {
                    return false;
                }
            }

            return true;
        }

        private bool MatchesText(WorkspaceTreeNode node)
        {
            if (string.IsNullOrEmpty(_filterText))
            {
                return true;
            }

            var filter = _filterText;
            return Contains(node.Name, filter) ||
                Contains(node.RelativePath, filter) ||
                Contains(node.AssemblyPath, filter) ||
                Contains(node.TypeName, filter);
        }

        private static bool Contains(string value, string filter)
        {
            return !string.IsNullOrEmpty(value) &&
                !string.IsNullOrEmpty(filter) &&
                value.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool AppliesToScope(ExplorerFilterScope contributionScope, ExplorerFilterScope requestedScope)
        {
            return contributionScope == ExplorerFilterScope.All ||
                requestedScope == ExplorerFilterScope.All ||
                contributionScope == requestedScope;
        }
    }
}
