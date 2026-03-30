using System;
using System.Collections.Generic;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Services.Editor.Context;

namespace Cortex.Services.Editor.Commands
{
    /// <summary>
    /// Builds toolbar items from resolved editor-context actions so the action
    /// bar stays aligned with the right-click menu and quick-actions picker.
    /// </summary>
    internal sealed class EditorToolbarService
    {
        private readonly List<EditorToolbarItem> _scratch = new List<EditorToolbarItem>();
        private readonly EditorCommandContextFactory _contextFactory = new EditorCommandContextFactory();
        private readonly EditorContextActionResolverService _resolverService = new EditorContextActionResolverService();

        /// <summary>
        /// Returns an ordered list of toolbar items for the given target context.
        /// Items are grouped and sorted from the action-bar placement results.
        /// </summary>
        public IList<EditorToolbarItem> BuildItems(
            CortexShellState state,
            ICommandRegistry commandRegistry,
            IContributionRegistry contributionRegistry,
            EditorCommandTarget target)
        {
            return BuildItems(
                state,
                commandRegistry,
                contributionRegistry,
                _contextFactory.CreateForTarget(state, target));
        }

        public IList<EditorToolbarItem> BuildItems(
            CortexShellState state,
            ICommandRegistry commandRegistry,
            IContributionRegistry contributionRegistry,
            EditorCommandInvocation invocation)
        {
            _scratch.Clear();
            var target = invocation != null ? invocation.Target : null;

            if (commandRegistry == null || contributionRegistry == null || target == null)
            {
                return _scratch;
            }

            var actions = _resolverService.ResolveActions(
                state,
                commandRegistry,
                contributionRegistry,
                invocation,
                EditorContextActionPlacement.ActionBar);
            for (var i = 0; i < actions.Count; i++)
            {
                var action = actions[i];
                if (action == null)
                {
                    continue;
                }

                _scratch.Add(new EditorToolbarItem
                {
                    CommandId = action.CommandId,
                    Label = action.Title ?? action.CommandId,
                    ToolTip = action.ShortcutText ?? string.Empty,
                    Group = action.Group ?? string.Empty,
                    SortOrder = action.SortOrder,
                    Enabled = action.Enabled
                });
            }

            _scratch.Sort(CompareItems);
            InsertGroupSeparators(_scratch);
            return _scratch;
        }

        private static int CompareItems(EditorToolbarItem a, EditorToolbarItem b)
        {
            var groupCmp = string.Compare(a.Group, b.Group, StringComparison.OrdinalIgnoreCase);
            if (groupCmp != 0)
            {
                return groupCmp;
            }

            return a.SortOrder.CompareTo(b.SortOrder);
        }

        private static void InsertGroupSeparators(List<EditorToolbarItem> items)
        {
            for (var i = items.Count - 1; i > 0; i--)
            {
                if (!string.Equals(items[i].Group, items[i - 1].Group, StringComparison.OrdinalIgnoreCase))
                {
                    items.Insert(i, EditorToolbarItem.CreateSeparator());
                }
            }
        }
    }

    /// <summary>
    /// Toolbar projection of a resolved editor action.
    /// </summary>
    internal sealed class EditorToolbarItem
    {
        public string CommandId = string.Empty;
        public string Label = string.Empty;
        public string ToolTip = string.Empty;
        public string Group = string.Empty;
        public int SortOrder;
        public bool Enabled = true;
        public bool IsSeparator;

        public static EditorToolbarItem CreateSeparator()
        {
            return new EditorToolbarItem
            {
                IsSeparator = true,
                Enabled = false
            };
        }
    }
}
