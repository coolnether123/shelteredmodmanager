using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ModAPI.Core;
using UnityEngine;

namespace ShelteredAPI.Scenarios.Presentation.Selection
{
    [Serializable]
    internal sealed class ScenarioLibraryPreferenceData
    {
        public int version = 1;
        public string sortMode = ScenarioLibrarySortMode.PinnedFirst.ToString();
        public List<string> pinnedScenarioIds = new List<string>();
    }

    internal sealed class ScenarioLibraryPreferenceStore
    {
        private readonly string _path;
        private readonly object _sync = new object();
        private ScenarioLibraryPreferenceData _data;

        public ScenarioLibraryPreferenceStore()
            : this(System.IO.Path.Combine(System.IO.Path.Combine(ModApiPaths.UserRoot, "ScenarioLibrary"), "library.json"))
        {
        }

        internal ScenarioLibraryPreferenceStore(string path)
        {
            _path = path;
            _data = Load(path);
        }

        public ScenarioLibrarySortMode SortMode
        {
            get
            {
                lock (_sync)
                {
                    try
                    {
                        return (ScenarioLibrarySortMode)Enum.Parse(
                            typeof(ScenarioLibrarySortMode),
                            _data.sortMode ?? string.Empty,
                            true);
                    }
                    catch
                    {
                        return ScenarioLibrarySortMode.PinnedFirst;
                    }
                }
            }
        }

        public bool IsPinned(string scenarioId)
        {
            if (string.IsNullOrEmpty(scenarioId))
                return false;

            lock (_sync)
            {
                for (int i = 0; i < _data.pinnedScenarioIds.Count; i++)
                {
                    if (string.Equals(_data.pinnedScenarioIds[i], scenarioId, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        public void SetSortMode(ScenarioLibrarySortMode mode)
        {
            lock (_sync)
            {
                _data.sortMode = mode.ToString();
                Save();
            }
        }

        public bool TogglePinned(string scenarioId)
        {
            if (string.IsNullOrEmpty(scenarioId))
                return false;

            lock (_sync)
            {
                for (int i = 0; i < _data.pinnedScenarioIds.Count; i++)
                {
                    if (!string.Equals(_data.pinnedScenarioIds[i], scenarioId, StringComparison.OrdinalIgnoreCase))
                        continue;

                    _data.pinnedScenarioIds.RemoveAt(i);
                    Save();
                    return false;
                }

                _data.pinnedScenarioIds.Add(scenarioId);
                Save();
                return true;
            }
        }

        private static ScenarioLibraryPreferenceData Load(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    ScenarioLibraryPreferenceData loaded = JsonUtility.FromJson<ScenarioLibraryPreferenceData>(File.ReadAllText(path));
                    if (loaded != null)
                    {
                        if (loaded.pinnedScenarioIds == null)
                            loaded.pinnedScenarioIds = new List<string>();
                        return loaded;
                    }
                }
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioLibrary] Could not read preferences: " + ex.Message);
            }

            return new ScenarioLibraryPreferenceData();
        }

        private void Save()
        {
            string directory = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string temporary = _path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(_data, true), Encoding.UTF8);
            if (File.Exists(_path))
            {
                try { File.Replace(temporary, _path, null); }
                catch
                {
                    File.Delete(_path);
                    File.Move(temporary, _path);
                }
            }
            else
            {
                File.Move(temporary, _path);
            }
        }
    }

    internal static class ScenarioLibraryOrganizer
    {
        public static List<ScenarioBookRowModel> Order(
            IList<ScenarioBookRowModel> rows,
            ScenarioLibrarySortMode mode,
            ScenarioLibraryPreferenceStore preferences)
        {
            List<ScenarioBookRowModel> tools = new List<ScenarioBookRowModel>();
            List<ScenarioBookRowModel> scenarios = new List<ScenarioBookRowModel>();
            for (int i = 0; rows != null && i < rows.Count; i++)
            {
                ScenarioBookRowModel row = rows[i];
                if (row != null
                    && (row.Kind == ScenarioBookRowKind.Type || row.Kind == ScenarioBookRowKind.OpenInstallScenarios))
                {
                    tools.Add(row);
                    continue;
                }

                if (row != null && row.Kind == ScenarioBookRowKind.Scenario && row.Scenario != null)
                {
                    row.IsPinned = preferences != null && preferences.IsPinned(row.Scenario.ScenarioId);
                    row.LibrarySortMode = mode;
                }
                scenarios.Add(row);
            }

            scenarios.Sort(delegate(ScenarioBookRowModel left, ScenarioBookRowModel right)
            {
                return Compare(left, right, mode);
            });
            tools.AddRange(scenarios);
            return tools;
        }

        public static string Label(ScenarioLibrarySortMode mode)
        {
            switch (mode)
            {
                case ScenarioLibrarySortMode.RecentlyPlayed: return "Recently played";
                case ScenarioLibrarySortMode.RecentlyDownloaded: return "Recently downloaded";
                case ScenarioLibrarySortMode.CreationDate: return "Creation date";
                case ScenarioLibrarySortMode.Name: return "Name";
                default: return "Pinned first";
            }
        }

        public static ScenarioLibrarySortMode Next(ScenarioLibrarySortMode mode)
        {
            int next = ((int)mode + 1) % Enum.GetValues(typeof(ScenarioLibrarySortMode)).Length;
            return (ScenarioLibrarySortMode)next;
        }

        public static string RelativePlayed(DateTime playedUtc, DateTime nowUtc)
        {
            TimeSpan age = nowUtc - playedUtc;
            if (age.TotalMinutes < 1) return "played just now";
            if (age.TotalHours < 1) return "played " + Math.Max(1, (int)age.TotalMinutes) + "m ago";
            if (age.TotalDays < 1) return "played " + Math.Max(1, (int)age.TotalHours) + "h ago";
            if (age.TotalDays < 30) return "played " + Math.Max(1, (int)age.TotalDays) + "d ago";
            if (age.TotalDays < 365) return "played " + Math.Max(1, (int)(age.TotalDays / 30)) + "mo ago";
            return "played " + Math.Max(1, (int)(age.TotalDays / 365)) + "y ago";
        }

        private static int Compare(ScenarioBookRowModel left, ScenarioBookRowModel right, ScenarioLibrarySortMode mode)
        {
            if (object.ReferenceEquals(left, right)) return 0;
            if (left == null) return 1;
            if (right == null) return -1;
            if (left.IsPinned != right.IsPinned) return left.IsPinned ? -1 : 1;

            int value = 0;
            switch (mode)
            {
                case ScenarioLibrarySortMode.RecentlyPlayed:
                    value = CompareNewest(left.Scenario != null ? left.Scenario.LastPlayedUtc : null, right.Scenario != null ? right.Scenario.LastPlayedUtc : null);
                    break;
                case ScenarioLibrarySortMode.RecentlyDownloaded:
                    value = CompareNewest(left.Scenario != null ? left.Scenario.InstalledUtc : null, right.Scenario != null ? right.Scenario.InstalledUtc : null);
                    break;
                case ScenarioLibrarySortMode.CreationDate:
                    value = CompareNewest(left.Scenario != null ? left.Scenario.CreatedUtc : null, right.Scenario != null ? right.Scenario.CreatedUtc : null);
                    break;
                case ScenarioLibrarySortMode.Name:
                    value = string.Compare(left.Title, right.Title, StringComparison.OrdinalIgnoreCase);
                    break;
                default:
                    int leftOrder = left.Scenario != null ? left.Scenario.Order : int.MaxValue;
                    int rightOrder = right.Scenario != null ? right.Scenario.Order : int.MaxValue;
                    value = leftOrder.CompareTo(rightOrder);
                    break;
            }

            if (value != 0) return value;
            value = string.Compare(left.Title, right.Title, StringComparison.OrdinalIgnoreCase);
            if (value != 0) return value;
            return string.Compare(
                left.Scenario != null ? left.Scenario.ScenarioId : string.Empty,
                right.Scenario != null ? right.Scenario.ScenarioId : string.Empty,
                StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareNewest(DateTime? left, DateTime? right)
        {
            if (left.HasValue && right.HasValue) return right.Value.CompareTo(left.Value);
            if (left.HasValue) return -1;
            if (right.HasValue) return 1;
            return 0;
        }
    }
}
