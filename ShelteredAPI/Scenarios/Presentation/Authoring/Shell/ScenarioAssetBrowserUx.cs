using System;
using System.Collections.Generic;
using ModAPI.Scenarios;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Domain.Stages;
using ShelteredAPI.Scenarios.Presentation.Authoring.Windows;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell
{
    internal static class ScenarioAssetBrowserUx
    {
        internal const string AllFilter = "all";
        internal const string FavoritesFilter = "favorites";
        internal const string RecentFilter = "recent";
        private const string FavoritesKey = "asset_browser.favorites";
        private const string RecentKey = "asset_browser.recent";
        private const int RecentLimit = 20;
        private static readonly object MembershipCacheSync = new object();
        private static readonly CachedMembership FavoritesMembership = new CachedMembership();
        private static readonly CachedMembership RecentMembership = new CachedMembership();

        internal sealed class CategoryLabel
        {
            public string Primary;
            public string Secondary;
        }

        internal static string ResolveDefaultFilter(
            ScenarioAuthoringState state,
            ScenarioAuthoringInspectorSection[] sections)
        {
            string selectedActionId = state != null ? state.AssetBrowserSelectedActionId : null;
            string selectedSection = FindSectionForAction(sections, selectedActionId);
            if (!string.IsNullOrEmpty(selectedSection))
                return selectedSection;

            ScenarioAuthoringTarget target = state != null ? state.SelectedTarget : null;
            if (target != null)
            {
                switch (target.Kind)
                {
                    case ScenarioAuthoringTargetKind.SceneSprite:
                        return FindSectionByHint(sections, "scene_");
                    case ScenarioAuthoringTargetKind.Wall:
                    case ScenarioAuthoringTargetKind.Wire:
                    case ScenarioAuthoringTargetKind.Background:
                        return FindSectionByHint(sections, "wall", "wire");
                    case ScenarioAuthoringTargetKind.Room:
                    case ScenarioAuthoringTargetKind.Tile:
                    case ScenarioAuthoringTargetKind.Light:
                        return FindSectionByHint(sections, "structure", "room");
                    case ScenarioAuthoringTargetKind.PlaceableObject:
                        return FindObjectSection(sections);
                }
            }

            ScenarioStageKind stage = state != null ? state.ActiveStage : ScenarioStageKind.None;
            switch (stage)
            {
                case ScenarioStageKind.BunkerBackground:
                    return Coalesce(FindSectionByHint(sections, "wall", "wire"), ResolvePopulatedOverviewFilter(state, sections));
                case ScenarioStageKind.BunkerInside:
                    return Coalesce(FindObjectSection(sections), ResolvePopulatedOverviewFilter(state, sections));
                case ScenarioStageKind.BunkerSurface:
                    return Coalesce(FindSectionByHint(sections, "scene_"), ResolvePopulatedOverviewFilter(state, sections));
                case ScenarioStageKind.Bunker:
                    return Coalesce(FindSectionByHint(sections, "structure", "room"), ResolvePopulatedOverviewFilter(state, sections));
                default:
                    return ResolvePopulatedOverviewFilter(state, sections);
            }
        }

        internal static CategoryLabel GetCategoryLabel(ScenarioAuthoringInspectorSection section)
        {
            string id = section != null ? section.Id ?? string.Empty : string.Empty;
            string title = section != null ? section.Title ?? "Assets" : "Assets";
            if (Contains(id, "scene_vanilla"))
                return Label("Vanilla Sprites", "Scene sprite source");
            if (Contains(id, "scene_scenario"))
                return Label("Scenario Sprites", "Imported / mod source");
            if (Contains(id, "weather_effect"))
                return Label("Weather FX", "Editable effect sprites");
            if (Contains(id, "structure") || Contains(id, "room"))
                return Label("Rooms", "Structure type");
            if (Contains(id, "wall"))
                return Label("Walls", "Room finish type");
            if (Contains(id, "wire"))
                return Label("Wiring", "Room utility type");

            return Label(ShortUniqueTitle(title), "Object type");
        }

        internal static bool IsFavorite(ScenarioAuthoringState state, string sourceActionId)
        {
            return GetCachedMembership(state, FavoritesKey).Contains(sourceActionId ?? string.Empty);
        }

        internal static bool IsRecent(ScenarioAuthoringState state, string sourceActionId)
        {
            return GetCachedMembership(state, RecentKey).Contains(sourceActionId ?? string.Empty);
        }

        internal static bool ToggleFavorite(ScenarioAuthoringState state, string sourceActionId)
        {
            if (state == null || state.Settings == null || string.IsNullOrEmpty(sourceActionId))
                return false;

            List<string> values = ReadList(state, FavoritesKey);
            int index = IndexOf(values, sourceActionId);
            bool favorite = index < 0;
            if (favorite)
                values.Insert(0, sourceActionId);
            else
                values.RemoveAt(index);
            WriteList(state, FavoritesKey, values);
            return favorite;
        }

        internal static void RecordRecent(ScenarioAuthoringState state, string sourceActionId)
        {
            if (state == null || state.Settings == null || string.IsNullOrEmpty(sourceActionId))
                return;

            List<string> values = ReadList(state, RecentKey);
            int existing = IndexOf(values, sourceActionId);
            if (existing >= 0)
                values.RemoveAt(existing);
            values.Insert(0, sourceActionId);
            while (values.Count > RecentLimit)
                values.RemoveAt(values.Count - 1);
            WriteList(state, RecentKey, values);
        }

        internal static int CountMatches(
            ScenarioAuthoringShellWindowViewModel window,
            ScenarioAuthoringState state,
            string filter)
        {
            int count = 0;
            for (int i = 0; window != null && window.Sections != null && i < window.Sections.Length; i++)
            {
                ScenarioAuthoringInspectorSection section = window.Sections[i];
                if (section == null || section.Layout != ScenarioAuthoringInspectorSectionLayout.CandidateGrid)
                    continue;
                for (int j = 0; section.Items != null && j < section.Items.Length; j++)
                {
                    ScenarioAuthoringInspectorAction action = section.Items[j] != null ? section.Items[j].Action : null;
                    if (ActionMatches(state, action, filter))
                        count++;
                }
            }
            return count;
        }

        internal static bool ActionMatches(
            ScenarioAuthoringState state,
            ScenarioAuthoringInspectorAction action,
            string filter)
        {
            if (action == null)
                return false;
            string sourceActionId = DecodeSourceActionId(action.Id);
            if (string.Equals(filter, FavoritesFilter, StringComparison.OrdinalIgnoreCase))
                return IsFavorite(state, sourceActionId);
            if (string.Equals(filter, RecentFilter, StringComparison.OrdinalIgnoreCase))
                return IsRecent(state, sourceActionId);
            return true;
        }

        internal static string DecodeSourceActionId(string browserActionId)
        {
            string sourceActionId;
            return ScenarioAuthoringActionCodec.TryDecodeTokenActionId(
                browserActionId,
                ScenarioAuthoringActionIds.ActionAssetBrowserSelectPrefix,
                out sourceActionId)
                ? sourceActionId
                : null;
        }

        // Kept pure so the persistence contract can round-trip arbitrary action ids in tests.
        internal static string SerializeList(IList<string> values)
        {
            string result = string.Empty;
            for (int i = 0; values != null && i < values.Count; i++)
            {
                if (string.IsNullOrEmpty(values[i]))
                    continue;
                string encoded = Uri.EscapeDataString(values[i]);
                result = result.Length == 0 ? encoded : result + "|" + encoded;
            }
            return result;
        }

        internal static List<string> DeserializeList(string value)
        {
            List<string> result = new List<string>();
            if (string.IsNullOrEmpty(value))
                return result;
            string[] parts = value.Split('|');
            for (int i = 0; i < parts.Length; i++)
            {
                string decoded = Uri.UnescapeDataString(parts[i]);
                if (!string.IsNullOrEmpty(decoded) && !ContainsAction(result, decoded))
                    result.Add(decoded);
            }
            return result;
        }

        private static List<string> ReadList(ScenarioAuthoringState state, string key)
        {
            return DeserializeList(state != null && state.Settings != null ? state.Settings.Get(key, string.Empty) : string.Empty);
        }

        private static HashSet<string> GetCachedMembership(ScenarioAuthoringState state, string key)
        {
            string serialized = state != null && state.Settings != null
                ? state.Settings.Get(key, string.Empty)
                : string.Empty;
            CachedMembership cache = string.Equals(key, FavoritesKey, StringComparison.Ordinal)
                ? FavoritesMembership
                : RecentMembership;
            lock (MembershipCacheSync)
            {
                if (!string.Equals(cache.Serialized, serialized, StringComparison.Ordinal))
                {
                    List<string> values = DeserializeList(serialized);
                    cache.Serialized = serialized;
                    cache.Values = new HashSet<string>(values, StringComparer.Ordinal);
                }

                return cache.Values;
            }
        }

        private static void WriteList(ScenarioAuthoringState state, string key, IList<string> values)
        {
            state.Settings.Set(key, SerializeList(values));
            ScenarioAuthoringSettingsService settings = ScenarioCompositionRoot.Resolve<ScenarioAuthoringSettingsService>();
            if (settings != null)
                settings.Save(state.Settings);
        }

        private static string FindSectionForAction(ScenarioAuthoringInspectorSection[] sections, string sourceActionId)
        {
            if (string.IsNullOrEmpty(sourceActionId))
                return null;
            for (int i = 0; sections != null && i < sections.Length; i++)
            {
                ScenarioAuthoringInspectorSection section = sections[i];
                for (int j = 0; section != null && section.Items != null && j < section.Items.Length; j++)
                {
                    ScenarioAuthoringInspectorAction action = section.Items[j] != null ? section.Items[j].Action : null;
                    if (action != null && string.Equals(DecodeSourceActionId(action.Id), sourceActionId, StringComparison.Ordinal))
                        return section.Id;
                }
            }
            return null;
        }

        private static string FindSectionByHint(ScenarioAuthoringInspectorSection[] sections, params string[] hints)
        {
            for (int i = 0; sections != null && i < sections.Length; i++)
            {
                ScenarioAuthoringInspectorSection section = sections[i];
                if (!HasCandidateAction(section))
                    continue;
                for (int j = 0; hints != null && j < hints.Length; j++)
                {
                    if (Contains(section.Id, hints[j]) || Contains(section.Title, hints[j]))
                        return section.Id;
                }
            }
            return null;
        }

        private static string FindObjectSection(ScenarioAuthoringInspectorSection[] sections)
        {
            for (int i = 0; sections != null && i < sections.Length; i++)
            {
                ScenarioAuthoringInspectorSection section = sections[i];
                string id = section != null ? section.Id ?? string.Empty : string.Empty;
                if (section != null
                    && HasCandidateAction(section)
                    && !Contains(id, "scene_")
                    && !Contains(id, "weather")
                    && !Contains(id, "wall")
                    && !Contains(id, "wire")
                    && !Contains(id, "room")
                    && !Contains(id, "structure"))
                    return id;
            }
            return null;
        }

        private static string ResolvePopulatedOverviewFilter(
            ScenarioAuthoringState state,
            ScenarioAuthoringInspectorSection[] sections)
        {
            if (HasMatchingAction(state, sections, RecentFilter))
                return RecentFilter;
            if (HasMatchingAction(state, sections, FavoritesFilter))
                return FavoritesFilter;

            for (int i = 0; sections != null && i < sections.Length; i++)
            {
                if (HasCandidateAction(sections[i]))
                    return AllFilter;
            }

            // Preserve the empty-catalog landing state; the renderer explains that
            // no assets are currently available instead of suggesting a dead action.
            return RecentFilter;
        }

        private static bool HasMatchingAction(
            ScenarioAuthoringState state,
            ScenarioAuthoringInspectorSection[] sections,
            string filter)
        {
            for (int i = 0; sections != null && i < sections.Length; i++)
            {
                ScenarioAuthoringInspectorSection section = sections[i];
                for (int j = 0; section != null && section.Items != null && j < section.Items.Length; j++)
                {
                    ScenarioAuthoringInspectorAction action = section.Items[j] != null ? section.Items[j].Action : null;
                    if (ActionMatches(state, action, filter))
                        return true;
                }
            }
            return false;
        }

        private static bool HasCandidateAction(ScenarioAuthoringInspectorSection section)
        {
            if (section == null || section.Layout != ScenarioAuthoringInspectorSectionLayout.CandidateGrid)
                return false;
            for (int i = 0; section.Items != null && i < section.Items.Length; i++)
            {
                if (section.Items[i] != null && section.Items[i].Action != null)
                    return true;
            }
            return false;
        }

        private static string ShortUniqueTitle(string title)
        {
            string value = title ?? "Assets";
            int separator = value.IndexOf(" - ", StringComparison.Ordinal);
            return separator > 0 ? value.Substring(0, separator) : value;
        }

        private static CategoryLabel Label(string primary, string secondary)
        {
            return new CategoryLabel { Primary = primary, Secondary = secondary };
        }

        private static string Coalesce(string value, string fallback)
        {
            return !string.IsNullOrEmpty(value) ? value : fallback;
        }

        private static bool Contains(string value, string fragment)
        {
            return !string.IsNullOrEmpty(value)
                && !string.IsNullOrEmpty(fragment)
                && value.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ContainsAction(IList<string> values, string actionId)
        {
            return IndexOf(values, actionId) >= 0;
        }

        private static int IndexOf(IList<string> values, string actionId)
        {
            for (int i = 0; values != null && i < values.Count; i++)
            {
                if (string.Equals(values[i], actionId, StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        private sealed class CachedMembership
        {
            public string Serialized = string.Empty;
            public HashSet<string> Values = new HashSet<string>(StringComparer.Ordinal);
        }
    }
}
