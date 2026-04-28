using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ModAPI.Core;
using ModAPI.Spine;
using ModAPI.UI;
using UnityEngine;

namespace ModAPI.Internal.UI
{
    internal sealed class ModSettingsPresetController
    {
        private readonly List<string> _availablePresets = new List<string>();
        private ModEntry _currentMod;
        private string _currentPresetName = "Custom";
        private string _presetOverride;
        private string _customSnapshotJson;

        internal IList<string> AvailablePresets
        {
            get { return _availablePresets; }
        }

        internal string CurrentPresetName
        {
            get { return _currentPresetName; }
        }

        internal bool HasPresets
        {
            get { return _availablePresets.Count > 0; }
        }

        internal void Initialize(ModEntry mod)
        {
            _currentMod = mod;
            CaptureCurrentSettingsAsCustom();
        }

        internal void CaptureCurrentSettingsAsCustom()
        {
            if (_currentMod == null || _currentMod.SettingsProvider == null)
                return;

            object settings = _currentMod.SettingsProvider.GetSettingsObject();
            if (_currentMod.SettingsProvider is ISettingsProvider2)
            {
                _customSnapshotJson = ((ISettingsProvider2)_currentMod.SettingsProvider).SerializeToJson();
            }
            else if (settings != null)
            {
                _customSnapshotJson = JsonUtility.ToJson(settings);
            }
        }

        internal void MarkCurrentStateAsCustom()
        {
            CaptureCurrentSettingsAsCustom();
            _presetOverride = "Custom";
        }

        internal void ClearOverride()
        {
            _presetOverride = null;
        }

        internal void RefreshAvailablePresets(List<SettingDefinition> allDefs)
        {
            _availablePresets.Clear();
            if (allDefs == null)
                return;

            _availablePresets.AddRange(
                allDefs.Where(d => d.Presets != null)
                    .SelectMany(d => d.Presets.Keys)
                    .Distinct()
                    .OrderBy(k => GetPresetPriority(k))
                    .ThenBy(k => k));
        }

        internal void UpdateCurrentPresetState(object settings, List<SettingDefinition> allDefs)
        {
            if (_presetOverride != null)
            {
                _currentPresetName = _presetOverride;
                return;
            }

            _currentPresetName = "Custom";
            if (allDefs == null)
                return;

            for (int i = 0; i < _availablePresets.Count; i++)
            {
                string preset = _availablePresets[i];
                bool match = true;
                bool hasCheck = false;

                for (int j = 0; j < allDefs.Count; j++)
                {
                    SettingDefinition def = allDefs[j];
                    if (def.Presets == null || !def.Presets.ContainsKey(preset))
                        continue;

                    hasCheck = true;
                    object presetValue = def.Presets[preset];
                    object currentValue = ModSettingsPanel.ReflectionHelper.GetValue(def, settings);

                    string left = Convert.ToString(presetValue, CultureInfo.InvariantCulture);
                    string right = Convert.ToString(currentValue, CultureInfo.InvariantCulture);
                    if (!string.Equals(left, right))
                    {
                        match = false;
                        break;
                    }
                }

                if (match && hasCheck)
                {
                    _currentPresetName = preset;
                    break;
                }
            }
        }

        internal bool CyclePreset(int delta, List<SettingDefinition> allDefs, object settings)
        {
            if (_availablePresets.Count == 0)
                return false;

            List<string> cycleList = new List<string>();
            cycleList.Add("Custom");
            cycleList.AddRange(_availablePresets);

            int currentIndex = cycleList.IndexOf(_currentPresetName);
            if (currentIndex < 0)
                currentIndex = 0;

            int nextIndex = currentIndex + delta;
            if (nextIndex >= cycleList.Count)
                nextIndex = 0;
            if (nextIndex < 0)
                nextIndex = cycleList.Count - 1;

            string targetPreset = cycleList[nextIndex];
            MMLog.WriteDebug("Cycling Preset to: " + targetPreset);

            if (targetPreset == "Custom")
            {
                RestoreCustomState();
                _presetOverride = "Custom";
                return true;
            }

            ApplyPreset(targetPreset, allDefs, settings);
            _presetOverride = targetPreset;
            return true;
        }

        internal void ApplyPresetByName(string presetName, List<SettingDefinition> allDefs, object settings)
        {
            if (string.IsNullOrEmpty(presetName))
                return;

            ApplyPreset(presetName, allDefs, settings);
            _presetOverride = presetName;
        }

        private void RestoreCustomState()
        {
            if (_currentMod == null || _currentMod.SettingsProvider == null || string.IsNullOrEmpty(_customSnapshotJson))
                return;

            object settingsObj = _currentMod.SettingsProvider.GetSettingsObject();
            if (settingsObj == null)
                return;

            MMLog.WriteDebug("Restoring Custom Snapshot...");
            JsonUtility.FromJsonOverwrite(_customSnapshotJson, settingsObj);
            _currentMod.SettingsProvider.OnSettingsLoaded();
        }

        private void ApplyPreset(string presetName, List<SettingDefinition> allDefs, object settings)
        {
            MMLog.WriteDebug("Applying Preset: " + presetName);
            int appliedCount = 0;

            if (allDefs != null)
            {
                for (int i = 0; i < allDefs.Count; i++)
                {
                    SettingDefinition def = allDefs[i];
                    if (def.Presets == null)
                        continue;

                    object presetValue;
                    if (!def.Presets.TryGetValue(presetName, out presetValue))
                        continue;

                    ModSettingsPanel.ReflectionHelper.SetValue(def, settings, presetValue);
                    appliedCount++;
                }
            }

            if (_currentMod != null && _currentMod.SettingsProvider != null)
                _currentMod.SettingsProvider.OnSettingsLoaded();

            if (_currentMod != null && _currentMod.SettingsProvider is ISettingsProvider2)
                ((ISettingsProvider2)_currentMod.SettingsProvider).Save();

            MMLog.WriteDebug("Preset '" + presetName + "' applied (" + appliedCount + " fields updated)");
        }

        private static int GetPresetPriority(string name)
        {
            if (string.IsNullOrEmpty(name)) return 999;

            string normalized = name.ToLowerInvariant();
            if (normalized == "easy") return 1;
            if (normalized == "medium" || normalized == "normal") return 2;
            if (normalized == "hard") return 3;
            if (normalized == "insane" || normalized == "extreme" || normalized == "hardcore") return 4;
            return 100;
        }
    }
}
