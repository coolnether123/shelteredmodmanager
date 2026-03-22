using System;
using System.Collections.Generic;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;

namespace Cortex.Services
{
    internal sealed class EditorContextMenuService
    {
        private static readonly string[] SuppressedLegacyCommandIds = new[]
        {
            "cortex.editor.copySymbol",
            "cortex.editor.copyHoverInfo"
        };

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

            var menus = GetEffectiveMenuContributions(contributionRegistry, target.ContextId, false);
            var commandContext = BuildCommandContext(state, target);
            var previousGroup = string.Empty;
            for (var i = 0; i < menus.Count; i++)
            {
                var menu = menus[i];
                if (menu == null)
                {
                    continue;
                }

                var definition = commandRegistry.Get(menu.CommandId);
                if (definition == null)
                {
                    continue;
                }

                var group = menu.Group ?? string.Empty;
                if (items.Count > 0 && !string.Equals(previousGroup, group, StringComparison.OrdinalIgnoreCase))
                {
                    items.Add(EditorContextMenuItem.CreateSeparator());
                }

                items.Add(new EditorContextMenuItem
                {
                    CommandId = menu.CommandId,
                    Label = definition.DisplayName ?? menu.CommandId,
                    ShortcutText = definition.DefaultGesture ?? string.Empty,
                    Enabled = commandRegistry.CanExecute(menu.CommandId, commandContext)
                });
                previousGroup = group;
            }

            return items;
        }

        internal static IList<MenuContribution> GetEffectiveMenuContributions(
            IContributionRegistry contributionRegistry,
            string requestedContextId,
            bool includeToolbarMenus)
        {
            var results = new List<MenuContribution>();
            if (contributionRegistry == null)
            {
                return results;
            }

            var menus = contributionRegistry.GetMenus();
            var selectedByCommandId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < menus.Count; i++)
            {
                var menu = menus[i];
                if (!IsEligibleEditorMenu(menu, requestedContextId, includeToolbarMenus) || IsSuppressedLegacyMenu(menu))
                {
                    continue;
                }

                int existingIndex;
                if (selectedByCommandId.TryGetValue(menu.CommandId, out existingIndex))
                {
                    results[existingIndex] = null;
                }

                selectedByCommandId[menu.CommandId] = results.Count;
                results.Add(menu);
            }

            var compacted = new List<MenuContribution>(results.Count);
            for (var i = 0; i < results.Count; i++)
            {
                if (results[i] != null)
                {
                    compacted.Add(results[i]);
                }
            }

            return compacted;
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

            return commandRegistry.Execute(commandId, BuildCommandContext(state, target));
        }

        private static CommandExecutionContext BuildCommandContext(CortexShellState state, EditorCommandTarget target)
        {
            return new CommandExecutionContext
            {
                ActiveContainerId = state != null ? state.Workbench.FocusedContainerId : string.Empty,
                ActiveDocumentId = state != null ? state.Documents.ActiveDocumentPath : string.Empty,
                FocusedRegionId = state != null ? state.Workbench.FocusedContainerId : string.Empty,
                Parameter = target
            };
        }

        private static bool MatchesContext(string contributionContextId, string requestedContextId)
        {
            if (string.IsNullOrEmpty(contributionContextId))
            {
                return true;
            }

            return string.Equals(contributionContextId, requestedContextId, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsEligibleEditorMenu(MenuContribution menu, string requestedContextId, bool includeToolbarMenus)
        {
            return menu != null &&
                (menu.Location == MenuProjectionLocation.ContextMenu ||
                    (includeToolbarMenus && menu.Location == MenuProjectionLocation.Toolbar)) &&
                MatchesContext(menu.ContextId, requestedContextId);
        }

        private static bool IsSuppressedLegacyMenu(MenuContribution menu)
        {
            if (menu == null || string.IsNullOrEmpty(menu.CommandId))
            {
                return false;
            }

            for (var i = 0; i < SuppressedLegacyCommandIds.Length; i++)
            {
                if (string.Equals(menu.CommandId, SuppressedLegacyCommandIds[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
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
