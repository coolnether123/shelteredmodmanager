using System;
using System.Collections.Generic;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Services.Editor.Context;

namespace Cortex.Services.Editor.Commands
{
    internal sealed class EditorContextActionResolverService
    {
        private readonly EditorCommandContextFactory _contextFactory = new EditorCommandContextFactory();
        private readonly EditorCommandAvailabilityService _availabilityService = new EditorCommandAvailabilityService();

        public IList<EditorResolvedContextAction> ResolveActions(
            CortexShellState state,
            ICommandRegistry commandRegistry,
            IContributionRegistry contributionRegistry,
            EditorCommandTarget target,
            EditorContextActionPlacement placement)
        {
            return ResolveActions(
                state,
                commandRegistry,
                contributionRegistry,
                _contextFactory.CreateForTarget(state, target),
                placement);
        }

        public IList<EditorResolvedContextAction> ResolveActions(
            CortexShellState state,
            ICommandRegistry commandRegistry,
            IContributionRegistry contributionRegistry,
            EditorCommandInvocation invocation,
            EditorContextActionPlacement placement)
        {
            var results = new List<EditorResolvedContextAction>();
            var target = invocation != null ? invocation.Target : null;
            if (commandRegistry == null || contributionRegistry == null || target == null)
            {
                return results;
            }

            var commandContext = _contextFactory.Build(invocation);
            var contributions = contributionRegistry.GetEditorContextActions();
            for (var i = 0; i < contributions.Count; i++)
            {
                var contribution = contributions[i];
                if (!IsIncluded(contribution, placement) || !MatchesContext(contribution, target))
                {
                    continue;
                }

                var definition = commandRegistry.Get(contribution.CommandId);
                if (definition == null)
                {
                    continue;
                }

                var availability = _availabilityService.GetAvailability(contribution.CommandId, state, target);
                if (availability != null && !availability.Visible)
                {
                    continue;
                }

                var disabledReason = availability != null ? availability.DisabledReason ?? string.Empty : string.Empty;
                var available = availability != null && availability.Enabled;
                var enabled = available && commandRegistry.CanExecute(contribution.CommandId, commandContext);
                if (!enabled && string.IsNullOrEmpty(disabledReason))
                {
                    disabledReason = "This action is not available for the current context.";
                }

                if (!enabled && !contribution.ShowWhenDisabled)
                {
                    continue;
                }

                results.Add(new EditorResolvedContextAction
                {
                    ActionId = !string.IsNullOrEmpty(contribution.ActionId) ? contribution.ActionId : contribution.CommandId,
                    CommandId = contribution.CommandId,
                    ContextId = contribution.ContextId,
                    Group = contribution.Group ?? string.Empty,
                    Title = !string.IsNullOrEmpty(contribution.Title) ? contribution.Title : definition.DisplayName ?? contribution.CommandId,
                    Description = availability != null && !string.IsNullOrEmpty(availability.Description)
                        ? availability.Description
                        : !string.IsNullOrEmpty(contribution.Description)
                            ? contribution.Description
                            : definition.Description ?? string.Empty,
                    ShortcutText = definition.DefaultGesture ?? string.Empty,
                    RequiredCapability = contribution.RequiredCapability ?? string.Empty,
                    DisabledReason = enabled ? string.Empty : disabledReason,
                    SortOrder = contribution.SortOrder,
                    Placements = contribution.Placements,
                    Enabled = enabled
                });
            }

            results.Sort(CompareActions);
            return results;
        }

        private static bool IsIncluded(EditorContextActionContribution contribution, EditorContextActionPlacement placement)
        {
            return contribution != null && (contribution.Placements & placement) == placement;
        }

        private static bool MatchesContext(EditorContextActionContribution contribution, EditorCommandTarget target)
        {
            if (contribution == null || target == null)
            {
                return false;
            }

            if (string.IsNullOrEmpty(target.SymbolText) && !contribution.IncludeWhenNoSymbol)
            {
                return false;
            }

            if (string.IsNullOrEmpty(contribution.ContextId))
            {
                return true;
            }

            if (string.Equals(contribution.ContextId, EditorContextIds.Document, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.Equals(contribution.ContextId, target.ContextId, StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareActions(EditorResolvedContextAction left, EditorResolvedContextAction right)
        {
            var groupOrder = string.Compare(left.Group, right.Group, StringComparison.OrdinalIgnoreCase);
            if (groupOrder != 0)
            {
                return groupOrder;
            }

            var sortOrder = left.SortOrder.CompareTo(right.SortOrder);
            if (sortOrder != 0)
            {
                return sortOrder;
            }

            return string.Compare(left.Title, right.Title, StringComparison.OrdinalIgnoreCase);
        }
    }
}
