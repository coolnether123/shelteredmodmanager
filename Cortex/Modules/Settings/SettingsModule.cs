using System;
using System.Collections.Generic;
using System.Reflection;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Presentation.Models;
using UnityEngine;

namespace Cortex.Modules.Settings
{
    public sealed class SettingsModule
    {
        private bool _loaded;
        private Vector2 _settingsScroll = Vector2.zero;
        private readonly Dictionary<string, string> _textValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, bool> _toggleValues = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private string _selectedThemeId = string.Empty;

        public void Draw(ICortexSettingsStore settingsStore, WorkbenchPresentationSnapshot snapshot, ThemeState themeState, CortexShellState state)
        {
            EnsureLoaded(snapshot, themeState, state);

            GUILayout.BeginVertical();
            CortexIdeLayout.DrawTwoPane(
                620f,
                460f,
                delegate
                {
                    DrawSourceSetupGuide(state);
                    GUILayout.Space(6f);
                    CortexIdeLayout.DrawGroup("Registered Settings", delegate
                    {
                        _settingsScroll = GUILayout.BeginScrollView(_settingsScroll, GUI.skin.box, GUILayout.ExpandHeight(true));
                        DrawRegisteredSettings(snapshot);
                        GUILayout.EndScrollView();
                    });
                },
                delegate
                {
                    DrawThemeRegistry(snapshot, themeState, state);
                    GUILayout.Space(6f);
                    DrawEditorRegistry(snapshot);
                    GUILayout.Space(6f);
                    CortexIdeLayout.DrawGroup("Actions", delegate
                    {
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button("Save Settings", GUILayout.Width(120f)))
                        {
                            Apply(snapshot, themeState, state);
                            settingsStore.Save(state.Settings);
                            state.ReloadSettingsRequested = true;
                            _loaded = false;
                            state.StatusMessage = "Saved Cortex settings.";
                        }
                        if (GUILayout.Button("Reset Defaults", GUILayout.Width(120f)))
                        {
                            state.Settings = new CortexSettings();
                            if (themeState != null)
                            {
                                themeState.ThemeId = state.Settings.ThemeId;
                            }
                            _loaded = false;
                            EnsureLoaded(snapshot, themeState, state);
                            state.StatusMessage = "Reset settings fields to defaults.";
                        }
                        if (GUILayout.Button("Show Logs Window", GUILayout.Width(140f)))
                        {
                            state.Logs.ShowDetachedWindow = true;
                        }
                        GUILayout.EndHorizontal();
                    });
                });
            GUILayout.EndVertical();
        }

        private static void DrawSourceSetupGuide(CortexShellState state)
        {
            CortexIdeLayout.DrawGroup("Source Setup", delegate
            {
                GUILayout.Label("To edit a mod, set the source-code folder in Projects under 'Mod Source Folder'.");
                GUILayout.Label("Use Workspace Scan Root for the folder that contains your editable mod projects. Use Loaded Mods Root for the live in-game mod installs.");
                GUILayout.Label("Workspace root: " + (state != null && state.Settings != null && !string.IsNullOrEmpty(state.Settings.WorkspaceRootPath) ? state.Settings.WorkspaceRootPath : "Not configured"));
                GUILayout.Label("Loaded mods root: " + (state != null && state.Settings != null && !string.IsNullOrEmpty(state.Settings.ModsRootPath) ? state.Settings.ModsRootPath : "Not configured"));
                GUILayout.Label("If you're mapping a loaded mod, open Projects and either use 'Insert Mapping' from the loaded-mod assistant or paste the path into 'Mod Source Folder'.");
            });
        }

        private void EnsureLoaded(WorkbenchPresentationSnapshot snapshot, ThemeState themeState, CortexShellState state)
        {
            if (_loaded)
            {
                return;
            }

            _textValues.Clear();
            _toggleValues.Clear();
            var settings = state.Settings ?? new CortexSettings();
            if (snapshot != null)
            {
                for (var i = 0; i < snapshot.Settings.Count; i++)
                {
                    LoadContributionValue(snapshot.Settings[i], settings);
                }
            }

            _selectedThemeId = !string.IsNullOrEmpty(settings.ThemeId)
                ? settings.ThemeId
                : themeState != null && !string.IsNullOrEmpty(themeState.ThemeId)
                    ? themeState.ThemeId
                    : "cortex.default";
            _loaded = true;
        }

        private void DrawRegisteredSettings(WorkbenchPresentationSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Settings.Count == 0)
            {
                GUILayout.Label("No settings contributions were registered.");
                return;
            }

            var currentScope = string.Empty;
            for (var i = 0; i < snapshot.Settings.Count; i++)
            {
                var contribution = snapshot.Settings[i];
                if (contribution == null || string.IsNullOrEmpty(contribution.SettingId) || IsThemeSetting(contribution))
                {
                    continue;
                }

                if (!string.Equals(currentScope, contribution.Scope, StringComparison.OrdinalIgnoreCase))
                {
                    currentScope = contribution.Scope ?? "General";
                    if (i > 0)
                    {
                        GUILayout.Space(6f);
                    }

                    GUILayout.Label(currentScope, GUILayout.Height(22f));
                }

                DrawSettingContribution(contribution);
            }
        }

        private void DrawThemeRegistry(WorkbenchPresentationSnapshot snapshot, ThemeState themeState, CortexShellState state)
        {
            CortexIdeLayout.DrawGroup("Themes", delegate
            {
                if (snapshot == null || snapshot.Themes.Count == 0)
                {
                    GUILayout.Label("No themes were registered.");
                    return;
                }

                GUILayout.Label("Registered themes are contribution-driven. The selected theme id is applied through ThemeState and persisted in Cortex settings.");
                for (var i = 0; i < snapshot.Themes.Count; i++)
                {
                    var theme = snapshot.Themes[i];
                    DrawThemeOption(theme, themeState, state);
                }
            });
        }

        private static void DrawEditorRegistry(WorkbenchPresentationSnapshot snapshot)
        {
            CortexIdeLayout.DrawGroup("Editors", delegate
            {
                if (snapshot == null || snapshot.Editors.Count == 0)
                {
                    GUILayout.Label("No editors were registered.");
                    return;
                }

                GUILayout.Label("Registered editor contributions describe the editor surface rather than hard-coded module state.");
                for (var i = 0; i < snapshot.Editors.Count; i++)
                {
                    var editor = snapshot.Editors[i];
                    GUILayout.BeginVertical(GUI.skin.box);
                    GUILayout.Label(editor.DisplayName);
                    GUILayout.Label("Extension: " + (editor.ResourceExtension ?? string.Empty) + " | Content Type: " + (editor.ContentType ?? string.Empty));
                    GUILayout.EndVertical();
                }
            });
        }

        private void DrawThemeOption(ThemeContribution theme, ThemeState themeState, CortexShellState state)
        {
            if (theme == null)
            {
                return;
            }

            var isSelected = string.Equals(_selectedThemeId, theme.ThemeId, StringComparison.OrdinalIgnoreCase);
            GUILayout.BeginVertical(GUI.skin.box);
            var label = theme.DisplayName + "  [" + theme.ThemeId + "]" + (string.Equals(theme.ThemeId, "cortex.default", StringComparison.OrdinalIgnoreCase) ? "  Default" : string.Empty);
            if (GUILayout.Toggle(isSelected, label, "button", GUILayout.Height(24f)))
            {
                _selectedThemeId = string.IsNullOrEmpty(theme.ThemeId) ? "cortex.default" : theme.ThemeId;
                if (themeState != null)
                {
                    themeState.ThemeId = _selectedThemeId;
                }

                if (state.Settings != null)
                {
                    state.Settings.ThemeId = _selectedThemeId;
                }
            }

            GUILayout.BeginHorizontal();
            DrawThemeSwatch(theme.BackgroundColor, "BG");
            DrawThemeSwatch(theme.SurfaceColor, "Surface");
            DrawThemeSwatch(theme.HeaderColor, "Header");
            DrawThemeSwatch(theme.AccentColor, "Accent");
            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(theme.Description))
            {
                GUILayout.Label(theme.Description);
            }

            GUILayout.Label("Font role: " + (string.IsNullOrEmpty(theme.FontRole) ? "default" : theme.FontRole));
            GUILayout.EndVertical();
        }

        private void DrawSettingContribution(SettingContribution contribution)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            if (contribution.ValueKind == SettingValueKind.Boolean)
            {
                var value = GetToggleValue(contribution);
                value = GUILayout.Toggle(value, contribution.DisplayName ?? contribution.SettingId);
                _toggleValues[contribution.SettingId] = value;
                DrawSettingDescription(contribution);
                GUILayout.EndVertical();
                return;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label(contribution.DisplayName ?? contribution.SettingId, GUILayout.Width(170f));
            _textValues[contribution.SettingId] = GUILayout.TextField(GetTextValue(contribution), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            DrawSettingDescription(contribution);
            if (!string.IsNullOrEmpty(contribution.DefaultValue))
            {
                GUILayout.Label("Default: " + contribution.DefaultValue);
            }
            GUILayout.EndVertical();
        }

        private static void DrawSettingDescription(SettingContribution contribution)
        {
            if (!string.IsNullOrEmpty(contribution.Description))
            {
                GUILayout.Label(contribution.Description);
            }
        }

        private static void DrawThemeSwatch(string hex, string label)
        {
            var previousBackground = GUI.backgroundColor;
            var previousContent = GUI.contentColor;
            var swatchColor = CortexIdeLayout.ParseColor(hex, Color.white);
            GUI.backgroundColor = swatchColor;
            GUI.contentColor = GetReadableSwatchTextColor(swatchColor);
            GUILayout.Box(label, GUILayout.Width(70f), GUILayout.Height(18f));
            GUI.backgroundColor = previousBackground;
            GUI.contentColor = previousContent;
            GUILayout.Space(4f);
        }

        private static Color GetReadableSwatchTextColor(Color swatchColor)
        {
            var luminance = (swatchColor.r * 0.299f) + (swatchColor.g * 0.587f) + (swatchColor.b * 0.114f);
            return luminance >= 0.6f ? Color.black : Color.white;
        }

        private void Apply(WorkbenchPresentationSnapshot snapshot, ThemeState themeState, CortexShellState state)
        {
            if (state.Settings == null)
            {
                state.Settings = new CortexSettings();
            }

            if (snapshot != null)
            {
                for (var i = 0; i < snapshot.Settings.Count; i++)
                {
                    var contribution = snapshot.Settings[i];
                    if (contribution == null || string.IsNullOrEmpty(contribution.SettingId))
                    {
                        continue;
                    }

                    ApplyContributionValue(contribution, state.Settings);
                }
            }

            state.Settings.ThemeId = string.IsNullOrEmpty(_selectedThemeId) ? "cortex.default" : _selectedThemeId;
            if (themeState != null)
            {
                themeState.ThemeId = state.Settings.ThemeId;
            }
        }

        private void LoadContributionValue(SettingContribution contribution, CortexSettings settings)
        {
            var field = GetSettingField(contribution);
            if (field == null)
            {
                return;
            }

            var value = field.GetValue(settings);
            if (contribution.ValueKind == SettingValueKind.Boolean)
            {
                _toggleValues[contribution.SettingId] = value is bool && (bool)value;
                return;
            }

            if (value == null)
            {
                _textValues[contribution.SettingId] = contribution.DefaultValue ?? string.Empty;
                return;
            }

            if (contribution.ValueKind == SettingValueKind.Float)
            {
                _textValues[contribution.SettingId] = ((float)value).ToString("F0");
                return;
            }

            _textValues[contribution.SettingId] = value.ToString();
        }

        private void ApplyContributionValue(SettingContribution contribution, CortexSettings settings)
        {
            var field = GetSettingField(contribution);
            if (field == null)
            {
                return;
            }

            switch (contribution.ValueKind)
            {
                case SettingValueKind.Boolean:
                    field.SetValue(settings, GetToggleValue(contribution));
                    break;
                case SettingValueKind.Integer:
                    field.SetValue(settings, ParseInt(GetTextValue(contribution), ParseInt(contribution.DefaultValue, (int)field.GetValue(settings))));
                    break;
                case SettingValueKind.Float:
                    field.SetValue(settings, ParseFloat(GetTextValue(contribution), ParseFloat(contribution.DefaultValue, (float)field.GetValue(settings))));
                    break;
                case SettingValueKind.String:
                default:
                    field.SetValue(settings, GetTextValue(contribution));
                    break;
            }
        }

        private static FieldInfo GetSettingField(SettingContribution contribution)
        {
            if (contribution == null || string.IsNullOrEmpty(contribution.SettingId))
            {
                return null;
            }

            return typeof(CortexSettings).GetField(contribution.SettingId, BindingFlags.Public | BindingFlags.Instance);
        }

        private string GetTextValue(SettingContribution contribution)
        {
            string value;
            return contribution != null && _textValues.TryGetValue(contribution.SettingId, out value)
                ? value ?? string.Empty
                : contribution != null ? contribution.DefaultValue ?? string.Empty : string.Empty;
        }

        private bool GetToggleValue(SettingContribution contribution)
        {
            bool value;
            if (contribution != null && _toggleValues.TryGetValue(contribution.SettingId, out value))
            {
                return value;
            }

            return contribution != null && string.Equals(contribution.DefaultValue, "true", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsThemeSetting(SettingContribution contribution)
        {
            return contribution != null && string.Equals(contribution.SettingId, nameof(CortexSettings.ThemeId), StringComparison.OrdinalIgnoreCase);
        }

        private static int ParseInt(string raw, int fallback)
        {
            int value;
            return int.TryParse(raw, out value) ? value : fallback;
        }

        private static float ParseFloat(string raw, float fallback)
        {
            float value;
            return float.TryParse(raw, out value) ? value : fallback;
        }
    }
}
