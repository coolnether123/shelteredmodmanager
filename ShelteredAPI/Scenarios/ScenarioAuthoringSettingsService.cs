using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security;
using System.Text;
using System.Xml;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioAuthoringSettingsService
    {
        private readonly List<ScenarioAuthoringSettingDefinition> _definitions = new List<ScenarioAuthoringSettingDefinition>();
        private readonly object _sync = new object();
        private ScenarioAuthoringSettingsSnapshot _cached;

        public ScenarioAuthoringSettingsService()
        {
            RegisterFloat("shell.ui_scale", "Shell", "UI Scale", "Overall scale for the authoring shell.", "1.10", 0.85f, 1.50f, 0.05f);
            RegisterFloat("shell.hover_delay", "Shell", "Hover Tooltip Delay", "Delay before hover details appear.", "0.20", 0f, 2f, 0.05f);
            RegisterToggle("shell.compact_mode", "Shell", "Compact Mode", "Reduce padding and row height across the shell.", "false");
            RegisterFloat("shell.panel_opacity", "Visuals", "Panel Opacity", "Opacity applied to bunker panels.", "0.82", 0.55f, 1f, 0.05f);
            RegisterToggle("layout.remember_windows", "Layout", "Remember Window Layout", "Persist editor layout between launches.", "true");
            RegisterToggle("layout.remember_hidden", "Layout", "Remember Hidden Windows", "Persist hidden-window state between launches.", "true");
            RegisterToggle("layout.auto_open_last_draft", "Layout", "Auto-Open Last Draft", "Reopen the previous draft when authoring starts.", "false");
            RegisterToggle("input.marquee_selection", "Input", "Marquee Selection", "Enable marquee selection where a tool supports it.", "false");
            RegisterToggle("visuals.show_grid", "Visuals", "Show Grid", "Show the shelter grid while authoring.", "true");
            RegisterToggle("visuals.snap_to_grid", "Visuals", "Snap To Grid", "Snap authored placements and previews to shelter cells.", "true");
            RegisterToggle("visuals.stronger_hover", "Visuals", "Stronger Hover Outlines", "Use stronger hover and focus outlines.", "true");
            RegisterToggle("input.playtest_auto_pause", "Input", "Playtest Auto-Pause On Open", "Pause the scenario when the shell reopens during playtest.", "false");
            RegisterToggle("input.block_vanilla_camera", "Input", "Block Vanilla Camera Input While Shell Focused", "Suppress vanilla camera pan and zoom while the shell owns pointer focus.", "true");
            RegisterFloat("input.scroll_speed", "Input", "Scroll Speed", "Scroll speed for lists and event timelines.", "1.00", 0.50f, 3f, 0.10f);
            RegisterInteger("sprite.zoom", "Sprite Tools", "Sprite Editor Default Zoom", "Default zoom for the in-game sprite editor.", "8", 1f, 32f, 1f);
            RegisterToggle("sprite.checkerboard", "Sprite Tools", "Sprite Editor Checkerboard", "Show a checkerboard behind transparent pixels.", "true");
            RegisterToggle("sprite.confirm_overwrite", "Sprite Tools", "Save PNG Overwrite Confirmation", "Confirm before overwriting an existing authored PNG.", "true");
            RegisterReadOnly("sprite.asset_root", "Sprite Tools", "Asset Root Path", "Preview of the mod-owned sprite output path.", ScenarioAuthoringStoragePaths.GetAssetsRootPath());
            RegisterToggle("debug.overlays", "Debug", "Debug Overlays", "Draw shell layout and capture diagnostics.", "false");
        }

        public ScenarioAuthoringSettingDefinition[] GetDefinitions()
        {
            return _definitions.ToArray();
        }

        public ScenarioAuthoringSettingDefinition FindDefinition(string id)
        {
            for (int i = 0; i < _definitions.Count; i++)
            {
                ScenarioAuthoringSettingDefinition definition = _definitions[i];
                if (definition != null && string.Equals(definition.Id, id, StringComparison.OrdinalIgnoreCase))
                    return definition;
            }

            return null;
        }

        public ScenarioAuthoringSettingsSnapshot Load()
        {
            lock (_sync)
            {
                if (_cached != null)
                    return _cached.Copy();

                ScenarioAuthoringSettingsSnapshot snapshot = BuildDefaults();
                string path = ScenarioAuthoringStoragePaths.GetSettingsFilePath();
                if (!File.Exists(path))
                {
                    _cached = snapshot;
                    return snapshot.Copy();
                }

                try
                {
                    XmlDocument document = new XmlDocument();
                    document.Load(path);
                    XmlNodeList nodes = document.SelectNodes("/ScenarioAuthoringSettings/Setting");
                    for (int i = 0; nodes != null && i < nodes.Count; i++)
                    {
                        XmlElement element = nodes[i] as XmlElement;
                        if (element == null)
                            continue;

                        string id = element.GetAttribute("id");
                        string value = element.GetAttribute("value");
                        if (!string.IsNullOrEmpty(id))
                            snapshot.Set(id, value);
                    }
                }
                catch (Exception ex)
                {
                    ModAPI.Core.MMLog.WriteWarning("[ScenarioAuthoringSettings] Failed to load settings: " + ex.Message);
                }

                ApplyDefinitionDefaults(snapshot);
                _cached = snapshot;
                return snapshot.Copy();
            }
        }

        public ScenarioAuthoringSettingsSnapshot ResetToDefaults()
        {
            lock (_sync)
            {
                _cached = BuildDefaults();
                Save(_cached);
                return _cached.Copy();
            }
        }

        public void Save(ScenarioAuthoringSettingsSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            lock (_sync)
            {
                ApplyDefinitionDefaults(snapshot);
                _cached = snapshot.Copy();
                StringBuilder builder = new StringBuilder();
                builder.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
                builder.AppendLine("<ScenarioAuthoringSettings>");
                for (int i = 0; i < _definitions.Count; i++)
                {
                    ScenarioAuthoringSettingDefinition definition = _definitions[i];
                    if (definition == null)
                        continue;

                    string value = snapshot.Get(definition.Id, definition.DefaultValue ?? string.Empty);
                    builder.Append("  <Setting id=\"")
                        .Append(Escape(definition.Id))
                        .Append("\" value=\"")
                        .Append(Escape(value))
                        .AppendLine("\" />");
                }

                builder.AppendLine("</ScenarioAuthoringSettings>");
                File.WriteAllText(ScenarioAuthoringStoragePaths.GetSettingsFilePath(), builder.ToString());
            }
        }

        public void ApplyDefinitionDefaults(ScenarioAuthoringSettingsSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            for (int i = 0; i < _definitions.Count; i++)
            {
                ScenarioAuthoringSettingDefinition definition = _definitions[i];
                if (definition == null || string.IsNullOrEmpty(definition.Id))
                    continue;

                snapshot.Set(definition.Id, NormalizeValue(definition, snapshot.Get(definition.Id, definition.DefaultValue)));
            }
        }

        private ScenarioAuthoringSettingsSnapshot BuildDefaults()
        {
            ScenarioAuthoringSettingsSnapshot snapshot = new ScenarioAuthoringSettingsSnapshot();
            for (int i = 0; i < _definitions.Count; i++)
            {
                ScenarioAuthoringSettingDefinition definition = _definitions[i];
                if (definition != null)
                    snapshot.Set(definition.Id, NormalizeValue(definition, definition.DefaultValue));
            }

            return snapshot;
        }

        private string NormalizeValue(ScenarioAuthoringSettingDefinition definition, string value)
        {
            if (definition == null)
                return value ?? string.Empty;

            switch (definition.Kind)
            {
                case ScenarioAuthoringSettingKind.Toggle:
                    bool toggle;
                    return bool.TryParse(value, out toggle)
                        ? (toggle ? "true" : "false")
                        : (string.Equals(definition.DefaultValue, "true", StringComparison.OrdinalIgnoreCase) ? "true" : "false");

                case ScenarioAuthoringSettingKind.Integer:
                    int intValue;
                    if (!int.TryParse(value, out intValue))
                        intValue = ParseInt(definition.DefaultValue, (int)definition.MinValue);
                    intValue = Math.Max((int)definition.MinValue, Math.Min((int)definition.MaxValue, intValue));
                    return intValue.ToString(CultureInfo.InvariantCulture);

                case ScenarioAuthoringSettingKind.Float:
                    float floatValue;
                    if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out floatValue))
                        floatValue = ParseFloat(definition.DefaultValue, definition.MinValue);
                    floatValue = Math.Max(definition.MinValue, Math.Min(definition.MaxValue, floatValue));
                    return floatValue.ToString("0.00", CultureInfo.InvariantCulture);

                case ScenarioAuthoringSettingKind.Choice:
                    if (definition.ChoiceValues != null)
                    {
                        for (int i = 0; i < definition.ChoiceValues.Length; i++)
                        {
                            if (string.Equals(definition.ChoiceValues[i], value, StringComparison.OrdinalIgnoreCase))
                                return definition.ChoiceValues[i];
                        }
                    }

                    return definition.DefaultValue ?? string.Empty;

                default:
                    return value ?? definition.DefaultValue ?? string.Empty;
            }
        }

        private static float ParseFloat(string value, float fallback)
        {
            float parsed;
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) ? parsed : fallback;
        }

        private static int ParseInt(string value, int fallback)
        {
            int parsed;
            return int.TryParse(value, out parsed) ? parsed : fallback;
        }

        private static string Escape(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : SecurityElement.Escape(value);
        }

        private void RegisterToggle(string id, string section, string label, string description, string defaultValue)
        {
            _definitions.Add(new ScenarioAuthoringSettingDefinition
            {
                Id = id,
                Section = section,
                Label = label,
                Description = description,
                Kind = ScenarioAuthoringSettingKind.Toggle,
                DefaultValue = defaultValue
            });
        }

        private void RegisterFloat(string id, string section, string label, string description, string defaultValue, float min, float max, float step)
        {
            _definitions.Add(new ScenarioAuthoringSettingDefinition
            {
                Id = id,
                Section = section,
                Label = label,
                Description = description,
                Kind = ScenarioAuthoringSettingKind.Float,
                DefaultValue = defaultValue,
                MinValue = min,
                MaxValue = max,
                Step = step
            });
        }

        private void RegisterInteger(string id, string section, string label, string description, string defaultValue, float min, float max, float step)
        {
            _definitions.Add(new ScenarioAuthoringSettingDefinition
            {
                Id = id,
                Section = section,
                Label = label,
                Description = description,
                Kind = ScenarioAuthoringSettingKind.Integer,
                DefaultValue = defaultValue,
                MinValue = min,
                MaxValue = max,
                Step = step
            });
        }

        private void RegisterReadOnly(string id, string section, string label, string description, string defaultValue)
        {
            _definitions.Add(new ScenarioAuthoringSettingDefinition
            {
                Id = id,
                Section = section,
                Label = label,
                Description = description,
                Kind = ScenarioAuthoringSettingKind.ReadOnly,
                DefaultValue = defaultValue
            });
        }
    }
}
