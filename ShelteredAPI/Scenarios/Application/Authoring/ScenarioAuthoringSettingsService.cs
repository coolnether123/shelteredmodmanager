using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security;
using System.Text;
using System.Xml;

using ShelteredAPI.Hooks;
using ShelteredAPI.Scenarios.Application.Authoring.Tutorial;
using ShelteredAPI.Scenarios.Infrastructure.Persistence;
namespace ShelteredAPI.Scenarios.Application.Authoring{
    internal sealed class ScenarioAuthoringSettingsService
    {
        private readonly List<ScenarioAuthoringSettingDefinition> _definitions = new List<ScenarioAuthoringSettingDefinition>();
        private readonly object _sync = new object();
        private ScenarioAuthoringSettingsSnapshot _cached;

        public ScenarioAuthoringSettingsService()
        {
            RegisterFloat("shell.ui_scale", "Shell", "UI Scale", "Overall scale for the authoring shell.", "1.10", 0.85f, 1.50f, 0.05f);
            RegisterFloat("shell.panel_opacity", "Visuals", "Panel Opacity", "Opacity applied to bunker panels.", "0.82", 0.55f, 1f, 0.05f);
            RegisterToggle("layout.remember_windows", "Layout", "Remember Window Layout", "Persist editor layout between launches.", "true");
            RegisterToggle("visuals.ui_animations", "Visuals", "UI Animations", "Enable animated authoring UI transitions.", "true");
            RegisterToggle("visuals.show_grid", "Visuals", "Show Grid", "Show the shelter grid while authoring.", "true");
            RegisterToggle("visuals.snap_to_grid", "Visuals", "Snap To Grid", "Snap authored placements and previews to shelter cells.", "true");
            RegisterToggle("layers.lock_background", "Layers", "Lock Backdrop Layer (Prevents Selecting)", "Prevent accidental backdrop/background selection.", "false");
            RegisterToggle("layers.lock_surface", "Layers", "Lock Surface Layer (Prevents Selecting)", "Prevent accidental exterior surface selection.", "false");
            RegisterToggle("layers.lock_inside", "Layers", "Lock Inside Layer (Prevents Selecting)", "Prevent accidental bunker-inside selection.", "false");
            RegisterChoice("shell.renderer_mode", "Advanced", "Renderer Mode", "Preferred scenario editor renderer.", "imgui", new[] { "imgui", "ngui" }, new[] { "Shell IMGUI", "NGUI Experimental" });
            RegisterToggle("input.block_vanilla_camera", "Input", "Block Vanilla Camera Input While Shell Focused", "Suppress vanilla camera pan and zoom while the shell owns pointer focus.", "true");
            RegisterFloat("input.scroll_speed", "Input", "Scroll Speed", "Scroll speed for lists and event timelines.", "1.00", 0.50f, 3f, 0.10f);
            RegisterInteger("sprite.zoom", "Sprite Tools", "Sprite Editor Default Zoom", "Default zoom for the in-game sprite editor.", "8", 1f, 32f, 1f);
            RegisterToggle("sprite.checkerboard", "Sprite Tools", "Sprite Editor Checkerboard", "Show a checkerboard behind transparent pixels.", "true");
            RegisterToggle("inspector.pin_edit_mode", "Advanced", "Edit Pinned Facts", "Show pin controls beside inspector facts.", "false");
            RegisterToggle("debug.show_advanced_details", "Advanced", "Show Advanced Details", "Show internal ids and runtime diagnostics in editor windows.", "false");
            RegisterToggle("debug.overlays", "Advanced", "Debug Overlays", "Draw shell layout and dump scene classification diagnostics.", "false");
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
                string path = ShelteredAPI.Scenarios.Infrastructure.Persistence.ScenarioAuthoringStoragePaths.GetSettingsFilePath();
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
                ScenarioAuthoringSettingsSnapshot previous = _cached != null ? _cached.Copy() : Load();
                ScenarioAuthoringSettingsSnapshot reset = BuildDefaults();
                string[] preservedKeys =
                {
                    TutorialContent.CompletedKey,
                    TutorialContent.SkippedKey,
                    TutorialContent.StepKey,
                    TutorialContent.HelpPageKey,
                    TutorialContent.HelpTopicKey
                };

                for (int i = 0; i < preservedKeys.Length; i++)
                {
                    string key = preservedKeys[i];
                    string value = previous != null ? previous.Get(key, null) : null;
                    if (value != null)
                        reset.Set(key, value);
                }

                _cached = reset;
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

                for (int i = 0; snapshot.Values != null && i < snapshot.Values.Count; i++)
                {
                    ScenarioAuthoringSettingValue value = snapshot.Values[i];
                    if (value == null || string.IsNullOrEmpty(value.Id) || IsDefined(value.Id))
                        continue;

                    builder.Append("  <Setting id=\"")
                        .Append(Escape(value.Id))
                        .Append("\" value=\"")
                        .Append(Escape(value.Value ?? string.Empty))
                        .AppendLine("\" />");
                }

                builder.AppendLine("</ScenarioAuthoringSettings>");
                File.WriteAllText(ShelteredAPI.Scenarios.Infrastructure.Persistence.ScenarioAuthoringStoragePaths.GetSettingsFilePath(), builder.ToString());
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

        private bool IsDefined(string id)
        {
            for (int i = 0; i < _definitions.Count; i++)
            {
                ScenarioAuthoringSettingDefinition definition = _definitions[i];
                if (definition != null && string.Equals(definition.Id, id, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
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

        private void RegisterChoice(string id, string section, string label, string description, string defaultValue, string[] values, string[] labels)
        {
            _definitions.Add(new ScenarioAuthoringSettingDefinition
            {
                Id = id,
                Section = section,
                Label = label,
                Description = description,
                Kind = ScenarioAuthoringSettingKind.Choice,
                DefaultValue = defaultValue,
                ChoiceValues = values ?? new string[0],
                ChoiceLabels = labels ?? new string[0]
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
