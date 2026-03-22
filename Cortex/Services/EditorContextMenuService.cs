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

            var actions = _resolverService.ResolveActions(
                state,
                commandRegistry,
                contributionRegistry,
                target,
                EditorContextActionPlacement.ContextMenu);
            var previousGroup = string.Empty;
            for (var i = 0; i < actions.Count; i++)
            {
                var action = actions[i];
                if (action == null)
                {
                    continue;
                }

                var group = action.Group ?? string.Empty;
                if (items.Count > 0 && !string.Equals(previousGroup, group, StringComparison.OrdinalIgnoreCase))
                {
                    items.Add(EditorContextMenuItem.CreateSeparator());
                }

                items.Add(new EditorContextMenuItem
                {
                    CommandId = action.CommandId,
                    Label = action.Title ?? action.CommandId,
                    ShortcutText = action.ShortcutText ?? string.Empty,
                    Enabled = action.Enabled
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

            return commandRegistry.Execute(commandId, _contextFactory.Build(state, target));
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
