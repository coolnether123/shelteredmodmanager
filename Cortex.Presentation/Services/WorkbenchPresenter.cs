using System.Collections.Generic;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Presentation.Abstractions;
using Cortex.Presentation.Models;

namespace Cortex.Presentation.Services
{
    public sealed class WorkbenchPresenter : IWorkbenchPresenter
    {
        public WorkbenchPresentationSnapshot BuildSnapshot(
            WorkbenchState workbenchState,
            LayoutState layoutState,
            StatusState statusState,
            ThemeState themeState,
            FocusState focusState,
            ICommandRegistry commandRegistry,
            IContributionRegistry contributionRegistry)
        {
            var snapshot = new WorkbenchPresentationSnapshot();
            if (workbenchState != null)
            {
                snapshot.ActiveContainerId = workbenchState.ActiveContainerId ?? string.Empty;
            }

            if (focusState != null)
            {
                snapshot.FocusedRegionId = focusState.FocusedRegionId ?? string.Empty;
            }

            if (themeState != null)
            {
                snapshot.ActiveThemeId = themeState.ThemeId ?? "cortex.vs-dark";
            }

            PopulateThemeTokens(snapshot, contributionRegistry);
            PopulateToolRail(snapshot, workbenchState, contributionRegistry);
            PopulateMenus(snapshot, commandRegistry, workbenchState, focusState, contributionRegistry);
            PopulateStatus(snapshot, contributionRegistry);
            PopulateCatalog(snapshot, contributionRegistry);
            return snapshot;
        }

        private static void PopulateToolRail(
            WorkbenchPresentationSnapshot snapshot,
            WorkbenchState workbenchState,
            IContributionRegistry contributionRegistry)
        {
            if (snapshot == null || contributionRegistry == null)
            {
                return;
            }

            IList<ViewContainerContribution> containers = contributionRegistry.GetViewContainers();
            for (var i = 0; i < containers.Count; i++)
            {
                var container = containers[i];
                snapshot.ToolRailItems.Add(new ToolRailItem
                {
                    ContainerId = container.ContainerId,
                    Title = container.Title,
                    IconId = container.IconId,
                    IconAlias = ResolveIconAlias(contributionRegistry, container.IconId),
                    HostLocation = container.DefaultHostLocation,
                    Active = workbenchState != null &&
                        string.Equals(workbenchState.ActiveContainerId, container.ContainerId)
                });
            }
        }

        private static void PopulateMenus(
            WorkbenchPresentationSnapshot snapshot,
            ICommandRegistry commandRegistry,
            WorkbenchState workbenchState,
            FocusState focusState,
            IContributionRegistry contributionRegistry)
        {
            if (snapshot == null || commandRegistry == null || contributionRegistry == null)
            {
                return;
            }

            var context = new CommandExecutionContext
            {
                ActiveContainerId = workbenchState != null ? workbenchState.ActiveContainerId : string.Empty,
                ActiveDocumentId = string.Empty,
                FocusedRegionId = focusState != null ? focusState.FocusedRegionId : string.Empty,
                Parameter = null
            };

            IList<MenuContribution> menus = contributionRegistry.GetMenus();
            for (var i = 0; i < menus.Count; i++)
            {
                var menu = menus[i];
                var command = commandRegistry.Get(menu.CommandId);
                if (command == null)
                {
                    continue;
                }

                var item = new MenuItemProjection
                {
                    CommandId = menu.CommandId,
                    DisplayName = string.IsNullOrEmpty(command.DisplayName) ? menu.CommandId : command.DisplayName,
                    Description = command.Description ?? string.Empty,
                    DefaultGesture = command.DefaultGesture ?? string.Empty,
                    Group = menu.Group ?? string.Empty,
                    IconAlias = ResolveIconAlias(contributionRegistry, command.IconId),
                    Location = menu.Location,
                    SortOrder = menu.SortOrder
                };

                if (menu.Location == MenuProjectionLocation.MainMenu)
                {
                    snapshot.MainMenuItems.Add(item);
                }
                else if (menu.Location == MenuProjectionLocation.Toolbar && commandRegistry.CanExecute(menu.CommandId, context))
                {
                    snapshot.ToolbarItems.Add(item);
                }
            }
        }

        private static void PopulateStatus(WorkbenchPresentationSnapshot snapshot, IContributionRegistry contributionRegistry)
        {
            if (snapshot == null || contributionRegistry == null)
            {
                return;
            }

            IList<StatusItemContribution> statusItems = contributionRegistry.GetStatusItems();
            for (var i = 0; i < statusItems.Count; i++)
            {
                if (statusItems[i].Alignment == StatusItemAlignment.Left)
                {
                    snapshot.LeftStatusItems.Add(statusItems[i]);
                }
                else
                {
                    snapshot.RightStatusItems.Add(statusItems[i]);
                }
            }
        }

        private static void PopulateCatalog(WorkbenchPresentationSnapshot snapshot, IContributionRegistry contributionRegistry)
        {
            if (snapshot == null || contributionRegistry == null)
            {
                return;
            }

            IList<ThemeContribution> themes = contributionRegistry.GetThemes();
            for (var i = 0; i < themes.Count; i++)
            {
                snapshot.Themes.Add(themes[i]);
            }

            IList<EditorContribution> editors = contributionRegistry.GetEditors();
            for (var i = 0; i < editors.Count; i++)
            {
                snapshot.Editors.Add(editors[i]);
            }

            IList<SettingContribution> settings = contributionRegistry.GetSettings();
            for (var i = 0; i < settings.Count; i++)
            {
                snapshot.Settings.Add(settings[i]);
            }
        }

        private static void PopulateThemeTokens(WorkbenchPresentationSnapshot snapshot, IContributionRegistry contributionRegistry)
        {
            if (snapshot == null || contributionRegistry == null)
            {
                return;
            }

            IList<ThemeContribution> themes = contributionRegistry.GetThemes();
            for (var i = 0; i < themes.Count; i++)
            {
                var theme = themes[i];
                if (theme == null || !string.Equals(theme.ThemeId, snapshot.ActiveThemeId, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ApplyThemeValue(ref snapshot.ThemeTokens.BackgroundColor, theme.BackgroundColor);
                ApplyThemeValue(ref snapshot.ThemeTokens.SurfaceColor, theme.SurfaceColor);
                ApplyThemeValue(ref snapshot.ThemeTokens.HeaderColor, theme.HeaderColor);
                ApplyThemeValue(ref snapshot.ThemeTokens.BorderColor, theme.BorderColor);
                ApplyThemeValue(ref snapshot.ThemeTokens.AccentColor, theme.AccentColor);
                ApplyThemeValue(ref snapshot.ThemeTokens.TextColor, theme.TextColor);
                ApplyThemeValue(ref snapshot.ThemeTokens.MutedTextColor, theme.MutedTextColor);
                ApplyThemeValue(ref snapshot.ThemeTokens.WarningColor, theme.WarningColor);
                ApplyThemeValue(ref snapshot.ThemeTokens.ErrorColor, theme.ErrorColor);
                ApplyThemeValue(ref snapshot.ThemeTokens.FontRole, theme.FontRole);
                break;
            }
        }

        private static void ApplyThemeValue(ref string target, string source)
        {
            if (!string.IsNullOrEmpty(source))
            {
                target = source;
            }
        }

        private static string ResolveIconAlias(IContributionRegistry contributionRegistry, string iconId)
        {
            if (contributionRegistry == null || string.IsNullOrEmpty(iconId))
            {
                return string.Empty;
            }

            IList<IconContribution> icons = contributionRegistry.GetIcons();
            for (var i = 0; i < icons.Count; i++)
            {
                if (string.Equals(icons[i].IconId, iconId))
                {
                    return icons[i].Alias ?? string.Empty;
                }
            }

            return string.Empty;
        }
    }
}
