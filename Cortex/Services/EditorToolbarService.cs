using System;
using System.Collections.Generic;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;

namespace Cortex.Services
{
    /// <summary>
    /// Builds an ordered, filterable list of toolbar items from the active
    /// contribution registry.  Each item mirrors its corresponding context-menu
    /// command so the toolbar always stays in sync with the right-click menu.
    ///
    /// Design principles
    /// -----------------
    /// • Single Responsibility – responsible only for building the item list;
    ///   execution is delegated to <see cref="EditorContextMenuService"/>.
    /// • Open / Closed – new commands appear automatically once they are
    ///   registered with the correct <see cref="MenuProjectionLocation"/>;
    ///   no code in this class needs to change.
    /// • Liskov / Interface Segregation – depends on the narrow
    ///   <see cref="IContributionRegistry"/> and <see cref="ICommandRegistry"/>
    ///   interfaces rather than concrete types.
    /// • Dependency Inversion – callers inject registries; this service owns
    ///   no singleton state beyond the reusable scratch list.
    /// </summary>
    internal sealed class EditorToolbarService
    {
        // Scratch list reused each frame to avoid per-frame allocations.
        private readonly List<EditorToolbarItem> _scratch = new List<EditorToolbarItem>();

        /// <summary>
        /// Returns an ordered list of toolbar items for the given target context.
        /// Items are first sorted by <see cref="MenuContribution.SortOrder"/> and
        /// then grouped by <see cref="MenuContribution.Group"/> so callers can
        /// insert visual separators between groups.
        ///
        /// Only contributions registered against
        /// <see cref="MenuProjectionLocation.ContextMenu"/> or
        /// <see cref="MenuProjectionLocation.Toolbar"/> are included.
        /// </summary>
        public IList<EditorToolbarItem> BuildItems(
            ICommandRegistry commandRegistry,
            IContributionRegistry contributionRegistry,
            EditorCommandTarget target)
        {
            _scratch.Clear();

            if (commandRegistry == null || contributionRegistry == null || target == null)
            {
                return _scratch;
            }

            var menus = EditorContextMenuService.GetEffectiveMenuContributions(contributionRegistry, target.ContextId, true);
            var commandContext = BuildCommandContext(target);

            for (var i = 0; i < menus.Count; i++)
            {
                var menu = menus[i];
                if (!IsToolbarEligible(menu))
                {
                    continue;
                }

                if (!MatchesContext(menu.ContextId, target.ContextId))
                {
                    continue;
                }

                var definition = commandRegistry.Get(menu.CommandId);
                if (definition == null)
                {
                    continue;
                }

                _scratch.Add(new EditorToolbarItem
                {
                    CommandId  = menu.CommandId,
                    Label      = definition.DisplayName ?? menu.CommandId,
                    ToolTip    = definition.DefaultGesture ?? string.Empty,
                    Group      = menu.Group ?? string.Empty,
                    SortOrder  = menu.SortOrder,
                    Enabled    = commandRegistry.CanExecute(menu.CommandId, commandContext)
                });
            }

            _scratch.Sort(CompareItems);
            InsertGroupSeparators(_scratch);
            return _scratch;
        }

        // ------------------------------------------------------------------ //
        //  Helpers
        // ------------------------------------------------------------------ //

        private static bool IsToolbarEligible(MenuContribution menu)
        {
            if (menu == null)
            {
                return false;
            }

            return menu.Location == MenuProjectionLocation.ContextMenu ||
                   menu.Location == MenuProjectionLocation.Toolbar;
        }

        private static bool MatchesContext(string contributionContextId, string requestedContextId)
        {
            if (string.IsNullOrEmpty(contributionContextId))
            {
                return true;
            }

            return string.Equals(contributionContextId, requestedContextId, StringComparison.OrdinalIgnoreCase);
        }

        private static CommandExecutionContext BuildCommandContext(EditorCommandTarget target)
        {
            return new CommandExecutionContext
            {
                ActiveDocumentId = target.DocumentPath ?? string.Empty,
                Parameter        = target
            };
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

        /// <summary>
        /// Inserts sentinel separator items between adjacent items whose
        /// <see cref="EditorToolbarItem.Group"/> values differ.
        /// Modifies <paramref name="items"/> in-place.
        /// </summary>
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
    /// A single item in the editor toolbar.
    /// Mirrors <see cref="EditorContextMenuItem"/> but carries extra ordering
    /// metadata so the toolbar can remain independently sortable in future.
    /// </summary>
    internal sealed class EditorToolbarItem
    {
        public string CommandId  = string.Empty;
        public string Label      = string.Empty;
        /// <summary>Keyboard shortcut hint shown in the tooltip.</summary>
        public string ToolTip    = string.Empty;
        public string Group      = string.Empty;
        public int    SortOrder;
        public bool   Enabled    = true;
        public bool   IsSeparator;

        /// <summary>Factory helper for group-boundary sentinels.</summary>
        public static EditorToolbarItem CreateSeparator()
        {
            return new EditorToolbarItem { IsSeparator = true, Enabled = false };
        }
    }
}
