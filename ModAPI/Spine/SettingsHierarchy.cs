using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ModAPI.Spine
{
    /// <summary>
    /// Builds and queries a parent-child hierarchy for settings definitions.
    /// Handles sorting, depth calculation, ancestor checks, and view filtering.
    /// </summary>
    public class SettingsHierarchy
    {
        private readonly Dictionary<string, SettingDefinition> _byId;
        private readonly Dictionary<string, List<SettingDefinition>> _childrenOf;
        private readonly List<SettingDefinition> _rootSettings;

        /// <summary>
        /// Creates a new hierarchy from a flat list of definitions.
        /// </summary>
        public SettingsHierarchy(IEnumerable<SettingDefinition> definitions)
        {
            _byId = new Dictionary<string, SettingDefinition>();
            _childrenOf = new Dictionary<string, List<SettingDefinition>>();
            _rootSettings = new List<SettingDefinition>();

            foreach (var def in definitions ?? Enumerable.Empty<SettingDefinition>())
            {
                if (def == null || string.IsNullOrEmpty(def.Id))
                {
                    continue;
                }

                _byId[def.Id] = def;
            }

            foreach (var def in _byId.Values)
            {
                if (string.IsNullOrEmpty(def.ParentId) || !_byId.ContainsKey(def.ParentId))
                {
                    _rootSettings.Add(def);
                    continue;
                }

                if (!_childrenOf.TryGetValue(def.ParentId, out var list))
                {
                    list = new List<SettingDefinition>();
                    _childrenOf[def.ParentId] = list;
                }

                list.Add(def);
            }

            SortList(_rootSettings);
            foreach (var childList in _childrenOf.Values)
            {
                SortList(childList);
            }
        }

        /// <summary>
        /// Returns all root-level settings sorted by sort order.
        /// </summary>
        public List<SettingDefinition> GetRootSettings()
        {
            return _rootSettings;
        }

        /// <summary>
        /// Returns direct children for a parent identifier (empty list when none).
        /// </summary>
        public List<SettingDefinition> GetChildren(string parentId)
        {
            if (string.IsNullOrEmpty(parentId) || !_childrenOf.TryGetValue(parentId, out var list))
            {
                return new List<SettingDefinition>();
            }

            return list;
        }

        /// <summary>
        /// Calculates hierarchy depth (0 for root).
        /// </summary>
        public int GetDepth(SettingDefinition setting)
        {
            int depth = 0;
            var current = setting;

            while (current != null && !string.IsNullOrEmpty(current.ParentId) &&
                   _byId.TryGetValue(current.ParentId, out var parent))
            {
                depth++;
                current = parent;
            }

            return depth;
        }

        /// <summary>
        /// Returns the parent definition for a setting, or null if it is root or unknown.
        /// </summary>
        public SettingDefinition GetParent(SettingDefinition setting)
        {
            if (setting == null || string.IsNullOrEmpty(setting.ParentId))
            {
                return null;
            }

            _byId.TryGetValue(setting.ParentId, out var parent);
            return parent;
        }

        /// <summary>
        /// Checks if the setting is disabled by any ancestor that controls visibility.
        /// </summary>
        public bool IsDisabledByAncestor(SettingDefinition setting, object settingsObject)
        {
            var current = setting;
            while (current != null && !string.IsNullOrEmpty(current.ParentId) &&
                   _byId.TryGetValue(current.ParentId, out var parent))
            {
                if (parent.ControlsChildVisibility && parent.Type == SettingType.Bool)
                {
                    if (!ReadBoolValue(parent, settingsObject))
                    {
                        return true;
                    }
                }

                current = parent;
            }

            return false;
        }

        /// <summary>
        /// Returns a flattened list in display order for the requested view.
        /// Applies VisibleWhen predicates using the supplied settings object.
        /// </summary>
        public IEnumerable<SettingDefinition> GetFlattenedForView(
            SettingsViewMode viewMode,
            object settingsObject)
        {
            foreach (var root in _rootSettings)
            {
                foreach (var item in EnumerateWithChildren(root, viewMode, settingsObject))
                {
                    yield return item;
                }
            }
        }

        /// <summary>
        /// Searches settings by label, tooltip, or identifier within the requested view.
        /// </summary>
        public IEnumerable<SettingDefinition> Search(string query, SettingsViewMode viewMode)
        {
            var ordered = _rootSettings.SelectMany(s => EnumerateWithChildren(s, viewMode, null));

            if (string.IsNullOrEmpty(query) || query.Trim().Length == 0)
            {
                return ordered;
            }

            string needle = query.ToLowerInvariant();
            return ordered.Where(def =>
                (!string.IsNullOrEmpty(def.Label) && def.Label.ToLowerInvariant().Contains(needle)) ||
                (!string.IsNullOrEmpty(def.Tooltip) && def.Tooltip.ToLowerInvariant().Contains(needle)) ||
                (!string.IsNullOrEmpty(def.Id) && def.Id.ToLowerInvariant().Contains(needle)));
        }

        private IEnumerable<SettingDefinition> EnumerateWithChildren(
            SettingDefinition setting,
            SettingsViewMode viewMode,
            object settingsObject)
        {
            if (IsVisibleInView(setting, viewMode))
            {
                if (settingsObject == null || setting.VisibleWhen == null || setting.VisibleWhen(settingsObject))
                {
                    yield return setting;
                }
                else
                {
                    // If hidden by predicate, skip children too
                    yield break;
                }
            }

            if (_childrenOf.TryGetValue(setting.Id, out var children))
            {
                foreach (var child in children)
                {
                    foreach (var desc in EnumerateWithChildren(child, viewMode, settingsObject))
                    {
                        yield return desc;
                    }
                }
            }
        }


        private static bool IsVisibleInView(SettingDefinition def, SettingsViewMode viewMode)
        {
            return viewMode == SettingsViewMode.Simple ? def.ShowInSimpleView : def.ShowInAdvancedView;
        }

        private static bool ReadBoolValue(SettingDefinition def, object settingsObject)
        {
            if (settingsObject == null || string.IsNullOrEmpty(def.FieldName))
            {
                return true;
            }

            var field = settingsObject.GetType().GetField(def.FieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (field != null && field.FieldType == typeof(bool))
            {
                return (bool)field.GetValue(settingsObject);
            }

            return true;
        }

        private static void SortList(List<SettingDefinition> list)
        {
            list.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
        }
    }

    /// <summary>
    /// View mode used to filter which settings are displayed.
    /// </summary>
    public enum SettingsViewMode
    {
        Simple,
        Advanced
    }
}
