using System;
using System.Collections.Generic;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;

namespace Cortex.Core.Services
{
    public sealed class ContributionRegistry : IContributionRegistry
    {
        private readonly Dictionary<string, ViewContainerContribution> _containers;
        private readonly Dictionary<string, List<ViewContribution> > _viewsByContainer;
        private readonly Dictionary<string, EditorContribution> _editors;
        private readonly List<MenuContribution> _menus;
        private readonly Dictionary<string, StatusItemContribution> _statusItems;
        private readonly Dictionary<string, ThemeContribution> _themes;
        private readonly Dictionary<string, IconContribution> _icons;
        private readonly Dictionary<string, SettingSectionContribution> _settingSections;
        private readonly Dictionary<string, SettingContribution> _settings;

        public ContributionRegistry()
        {
            _containers = new Dictionary<string, ViewContainerContribution>(StringComparer.OrdinalIgnoreCase);
            _viewsByContainer = new Dictionary<string, List<ViewContribution> >(StringComparer.OrdinalIgnoreCase);
            _editors = new Dictionary<string, EditorContribution>(StringComparer.OrdinalIgnoreCase);
            _menus = new List<MenuContribution>();
            _statusItems = new Dictionary<string, StatusItemContribution>(StringComparer.OrdinalIgnoreCase);
            _themes = new Dictionary<string, ThemeContribution>(StringComparer.OrdinalIgnoreCase);
            _icons = new Dictionary<string, IconContribution>(StringComparer.OrdinalIgnoreCase);
            _settingSections = new Dictionary<string, SettingSectionContribution>(StringComparer.OrdinalIgnoreCase);
            _settings = new Dictionary<string, SettingContribution>(StringComparer.OrdinalIgnoreCase);
        }

        public void RegisterViewContainer(ViewContainerContribution contribution)
        {
            if (contribution == null || string.IsNullOrEmpty(contribution.ContainerId))
            {
                return;
            }

            _containers[contribution.ContainerId] = contribution;
            if (!_viewsByContainer.ContainsKey(contribution.ContainerId))
            {
                _viewsByContainer[contribution.ContainerId] = new List<ViewContribution>();
            }
        }

        public void RegisterView(ViewContribution contribution)
        {
            if (contribution == null || string.IsNullOrEmpty(contribution.ContainerId) || string.IsNullOrEmpty(contribution.ViewId))
            {
                return;
            }

            List<ViewContribution> list;
            if (!_viewsByContainer.TryGetValue(contribution.ContainerId, out list))
            {
                list = new List<ViewContribution>();
                _viewsByContainer[contribution.ContainerId] = list;
            }

            for (var i = list.Count - 1; i >= 0; i--)
            {
                if (string.Equals(list[i].ViewId, contribution.ViewId, StringComparison.OrdinalIgnoreCase))
                {
                    list.RemoveAt(i);
                }
            }

            list.Add(contribution);
            list.Sort(delegate(ViewContribution left, ViewContribution right)
            {
                var order = left.SortOrder.CompareTo(right.SortOrder);
                return order != 0
                    ? order
                    : string.Compare(left.Title, right.Title, StringComparison.OrdinalIgnoreCase);
            });
        }

        public void RegisterEditor(EditorContribution contribution)
        {
            if (contribution == null || string.IsNullOrEmpty(contribution.EditorId))
            {
                return;
            }

            _editors[contribution.EditorId] = contribution;
        }

        public void RegisterMenu(MenuContribution contribution)
        {
            if (contribution == null || string.IsNullOrEmpty(contribution.CommandId))
            {
                return;
            }

            _menus.Add(contribution);
        }

        public void RegisterStatusItem(StatusItemContribution contribution)
        {
            if (contribution == null || string.IsNullOrEmpty(contribution.ItemId))
            {
                return;
            }

            _statusItems[contribution.ItemId] = contribution;
        }

        public void RegisterTheme(ThemeContribution contribution)
        {
            if (contribution == null || string.IsNullOrEmpty(contribution.ThemeId))
            {
                return;
            }

            _themes[contribution.ThemeId] = contribution;
        }

        public void RegisterIcon(IconContribution contribution)
        {
            if (contribution == null || string.IsNullOrEmpty(contribution.IconId))
            {
                return;
            }

            _icons[contribution.IconId] = contribution;
        }

        public void RegisterSetting(SettingContribution contribution)
        {
            if (contribution == null || string.IsNullOrEmpty(contribution.SettingId))
            {
                return;
            }

            _settings[contribution.SettingId] = contribution;
        }

        public void RegisterSettingSection(SettingSectionContribution contribution)
        {
            if (contribution == null || string.IsNullOrEmpty(contribution.Scope))
            {
                return;
            }

            _settingSections[contribution.Scope] = contribution;
        }

        public IList<ViewContainerContribution> GetViewContainers()
        {
            var results = new List<ViewContainerContribution>(_containers.Values);
            results.Sort(delegate(ViewContainerContribution left, ViewContainerContribution right)
            {
                var order = left.SortOrder.CompareTo(right.SortOrder);
                return order != 0
                    ? order
                    : string.Compare(left.Title, right.Title, StringComparison.OrdinalIgnoreCase);
            });
            return results;
        }

        public IList<ViewContribution> GetViews(string containerId)
        {
            List<ViewContribution> list;
            if (string.IsNullOrEmpty(containerId) || !_viewsByContainer.TryGetValue(containerId, out list))
            {
                return new List<ViewContribution>();
            }

            return new List<ViewContribution>(list);
        }

        public IList<EditorContribution> GetEditors()
        {
            var results = new List<EditorContribution>(_editors.Values);
            results.Sort(delegate(EditorContribution left, EditorContribution right)
            {
                var order = left.SortOrder.CompareTo(right.SortOrder);
                return order != 0
                    ? order
                    : string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
            });
            return results;
        }

        public IList<MenuContribution> GetMenus()
        {
            var results = new List<MenuContribution>(_menus);
            results.Sort(delegate(MenuContribution left, MenuContribution right)
            {
                var order = left.Location.CompareTo(right.Location);
                if (order != 0)
                {
                    return order;
                }

                order = string.Compare(left.Group, right.Group, StringComparison.OrdinalIgnoreCase);
                if (order != 0)
                {
                    return order;
                }

                order = string.Compare(left.ContextId, right.ContextId, StringComparison.OrdinalIgnoreCase);
                if (order != 0)
                {
                    return order;
                }

                order = left.SortOrder.CompareTo(right.SortOrder);
                return order != 0
                    ? order
                    : string.Compare(left.CommandId, right.CommandId, StringComparison.OrdinalIgnoreCase);
            });
            return results;
        }

        public IList<StatusItemContribution> GetStatusItems()
        {
            var results = new List<StatusItemContribution>(_statusItems.Values);
            results.Sort(delegate(StatusItemContribution left, StatusItemContribution right)
            {
                var order = left.Alignment.CompareTo(right.Alignment);
                if (order != 0)
                {
                    return order;
                }

                order = left.Priority.CompareTo(right.Priority);
                return order != 0
                    ? order
                    : string.Compare(left.Text, right.Text, StringComparison.OrdinalIgnoreCase);
            });
            return results;
        }

        public IList<ThemeContribution> GetThemes()
        {
            var results = new List<ThemeContribution>(_themes.Values);
            results.Sort(delegate(ThemeContribution left, ThemeContribution right)
            {
                var order = left.SortOrder.CompareTo(right.SortOrder);
                return order != 0
                    ? order
                    : string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
            });
            return results;
        }

        public IList<IconContribution> GetIcons()
        {
            var results = new List<IconContribution>(_icons.Values);
            results.Sort(delegate(IconContribution left, IconContribution right)
            {
                var order = string.Compare(left.Alias, right.Alias, StringComparison.OrdinalIgnoreCase);
                return order != 0
                    ? order
                    : string.Compare(left.IconId, right.IconId, StringComparison.OrdinalIgnoreCase);
            });
            return results;
        }

        public IList<SettingSectionContribution> GetSettingSections()
        {
            var results = new List<SettingSectionContribution>(_settingSections.Values);
            results.Sort(delegate(SettingSectionContribution left, SettingSectionContribution right)
            {
                var order = left.SortOrder.CompareTo(right.SortOrder);
                return order != 0
                    ? order
                    : string.Compare(left.SectionTitle, right.SectionTitle, StringComparison.OrdinalIgnoreCase);
            });
            return results;
        }

        public IList<SettingContribution> GetSettings()
        {
            var results = new List<SettingContribution>(_settings.Values);
            results.Sort(delegate(SettingContribution left, SettingContribution right)
            {
                var order = string.Compare(left.Scope, right.Scope, StringComparison.OrdinalIgnoreCase);
                if (order != 0)
                {
                    return order;
                }

                order = left.SortOrder.CompareTo(right.SortOrder);
                return order != 0
                    ? order
                    : string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
            });
            return results;
        }
    }
}
