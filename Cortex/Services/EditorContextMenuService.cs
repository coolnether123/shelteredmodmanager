using System;
using System.Collections.Generic;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;

namespace Cortex.Services
{
    internal sealed class EditorContextMenuService
    {
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

            var menus = contributionRegistry.GetMenus();
            var commandContext = BuildCommandContext(state, target);
            var previousGroup = string.Empty;
            for (var i = 0; i < menus.Count; i++)
            {
                var menu = menus[i];
                if (menu == null ||
                    menu.Location != MenuProjectionLocation.ContextMenu ||
                    !MatchesContext(menu.ContextId, target.ContextId))
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
