using System;
using System.Collections.Generic;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;

namespace Cortex.Services
{
    internal sealed class EditorContextMenuService
    {
        private readonly EditorContextActionResolverService _resolverService = new EditorContextActionResolverService();
        private readonly EditorCommandContextFactory _contextFactory = new EditorCommandContextFactory();

        public IList<EditorContextMenuItem> BuildItems(
            CortexShellState state,
            ICommandRegistry commandRegistry,
            IContributionRegistry contributionRegistry,
            EditorCommandTarget target)
        {
            var items = new List<EditorContextMenuItem>();
            if (commandRegistry == null || contributionRegistry == null || target == null)
            {
                return items;
            }

            var harmonyCommandsConsidered = 0;
            var harmonyCommandsVisible = 0;
            var harmonyActionsResolved = 0;

            var resolvedItems = new List<ResolvedMenuItem>();
            var actions = _resolverService.ResolveActions(
                state,
                commandRegistry,
                contributionRegistry,
                target,
                EditorContextActionPlacement.ContextMenu);
            for (var i = 0; i < actions.Count; i++)
            {
                var action = actions[i];
                if (action == null)
                {
                    continue;
                }

                if (IsHarmonyCommand(action.CommandId))
                {
                    harmonyActionsResolved++;
                    MMLog.WriteInfo("[Cortex.Harmony] Editor action resolved for context menu. Command='" +
                        (action.CommandId ?? string.Empty) +
                        "', Enabled=" + action.Enabled +
                        ", Symbol='" + (target.SymbolText ?? string.Empty) +
                        "', Document='" + (target.DocumentPath ?? string.Empty) + "'.");
                }

                resolvedItems.Add(new ResolvedMenuItem
                {
                    CommandId = action.CommandId,
                    Label = action.Title ?? action.CommandId,
                    ShortcutText = action.ShortcutText ?? string.Empty,
                    Enabled = action.Enabled,
                    Group = action.Group ?? string.Empty,
                    SortOrder = action.SortOrder
                });
            }

            var commandContext = _contextFactory.Build(state, target);
            var menus = contributionRegistry.GetMenus();
            for (var i = 0; i < menus.Count; i++)
            {
                var menu = menus[i];
                if (menu == null ||
                    menu.Location != MenuProjectionLocation.ContextMenu ||
                    !MatchesContext(menu.ContextId, target))
                {
                    continue;
                }

                var definition = commandRegistry.Get(menu.CommandId);
                if (definition == null)
                {
                    continue;
                }

                var isHarmony = IsHarmonyCommand(menu.CommandId);
                if (isHarmony)
                {
                    harmonyCommandsConsidered++;
                }

                var enabled = commandRegistry.CanExecute(menu.CommandId, commandContext);
                if (!enabled && !menu.ShowWhenDisabled)
                {
                    if (isHarmony)
                    {
                        MMLog.WriteInfo("[Cortex.Harmony] Context menu command hidden. Command='" +
                            (menu.CommandId ?? string.Empty) +
                            "', Enabled=False, ShowWhenDisabled=False, Symbol='" +
                            (target.SymbolText ?? string.Empty) +
                            "', Document='" + (target.DocumentPath ?? string.Empty) + "'.");
                    }
                    continue;
                }

                if (isHarmony)
                {
                    harmonyCommandsVisible++;
                    MMLog.WriteInfo("[Cortex.Harmony] Context menu command visible. Command='" +
                        (menu.CommandId ?? string.Empty) +
                        "', Enabled=" + enabled +
                        ", Symbol='" + (target.SymbolText ?? string.Empty) +
                        "', Document='" + (target.DocumentPath ?? string.Empty) + "'.");
                }

                resolvedItems.Add(new ResolvedMenuItem
                {
                    CommandId = menu.CommandId,
                    Label = !string.IsNullOrEmpty(definition.DisplayName) ? definition.DisplayName : menu.CommandId,
                    ShortcutText = definition.DefaultGesture ?? string.Empty,
                    Enabled = enabled,
                    Group = menu.Group ?? string.Empty,
                    SortOrder = menu.SortOrder
                });
            }

            resolvedItems.Sort(CompareItems);
            var previousGroup = string.Empty;
            for (var i = 0; i < resolvedItems.Count; i++)
            {
                var item = resolvedItems[i];
                if (item == null)
                {
                    continue;
                }

                if (items.Count > 0 && !string.Equals(previousGroup, item.Group ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                {
                    items.Add(EditorContextMenuItem.CreateSeparator());
                }

                items.Add(new EditorContextMenuItem
                {
                    CommandId = item.CommandId,
                    Label = item.Label,
                    ShortcutText = item.ShortcutText,
                    Enabled = item.Enabled
                });
                previousGroup = item.Group ?? string.Empty;
            }

            if (harmonyCommandsConsidered > 0 || harmonyActionsResolved > 0)
            {
                MMLog.WriteInfo("[Cortex.Harmony] Context menu build summary. Symbol='" +
                    (target.SymbolText ?? string.Empty) +
                    "', Document='" + (target.DocumentPath ?? string.Empty) +
                    "', Position=" + target.AbsolutePosition +
                    ", HarmonyActionsResolved=" + harmonyActionsResolved +
                    ", HarmonyCommandsConsidered=" + harmonyCommandsConsidered +
                    ", HarmonyCommandsVisible=" + harmonyCommandsVisible +
                    ", TotalMenuItems=" + items.Count + ".");
            }

            return items;
        }

        public bool Execute(
            CortexShellState state,
            ICommandRegistry commandRegistry,
            EditorCommandTarget target,
            string commandId)
        {
            if (commandRegistry == null || target == null || string.IsNullOrEmpty(commandId))
            {
                return false;
            }

            return commandRegistry.Execute(commandId, _contextFactory.Build(state, target));
        }

        private static bool MatchesContext(string contextId, EditorCommandTarget target)
        {
            if (target == null)
            {
                return false;
            }

            if (string.IsNullOrEmpty(contextId) || string.Equals(contextId, EditorContextIds.Document, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.Equals(contextId, target.ContextId, StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareItems(ResolvedMenuItem left, ResolvedMenuItem right)
        {
            var groupOrder = string.Compare(left != null ? left.Group : string.Empty, right != null ? right.Group : string.Empty, StringComparison.OrdinalIgnoreCase);
            if (groupOrder != 0)
            {
                return groupOrder;
            }

            var sortOrder = (left != null ? left.SortOrder : 0).CompareTo(right != null ? right.SortOrder : 0);
            if (sortOrder != 0)
            {
                return sortOrder;
            }

            return string.Compare(left != null ? left.Label : string.Empty, right != null ? right.Label : string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsHarmonyCommand(string commandId)
        {
            return !string.IsNullOrEmpty(commandId) &&
                commandId.StartsWith("cortex.harmony.", StringComparison.OrdinalIgnoreCase);
        }
    }

    internal sealed class ResolvedMenuItem
    {
        public string CommandId;
        public string Label;
        public string ShortcutText;
        public bool Enabled;
        public string Group;
        public int SortOrder;
    }

    internal sealed class EditorContextMenuItem
    {
        public string CommandId;
        public string Label;
        public string ShortcutText;
        public bool Enabled;
        public bool IsSeparator;

        public static EditorContextMenuItem CreateSeparator()
        {
            return new EditorContextMenuItem
            {
                IsSeparator = true,
                Enabled = false
            };
        }
    }
}
