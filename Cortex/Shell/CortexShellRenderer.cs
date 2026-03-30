using System;
using System.Collections.Generic;
using Cortex.Chrome;
using Cortex.Core.Models;
using Cortex.Presentation.Abstractions;
using Cortex.Presentation.Models;
using Cortex.Services.Editor.Commands;
using Cortex.Services.Editor.Context;
using Cortex.Services.Editor.Input;
using Cortex.Services.Editor.Presentation;
using Cortex.Services.Harmony.Workflow;
using Cortex.Services.Inspector;
using Cortex.Services.Inspector.Identity;
using Cortex.Services.Onboarding;
using Cortex.Services.Search;
using Cortex.Services.Semantics.Completion.Augmentation;
using Cortex.Services.Semantics.Diagnostics;
using Cortex.Services.Semantics.Workbench;
using Cortex.Shell;
using UnityEngine;

namespace Cortex.Shell
{
    /// <summary>
    /// Responsible for the high-level IMGUI rendering of the Cortex Shell, 
    /// including window chrome, headers, status bars, and menus.
    /// </summary>
    internal interface ICortexShellRenderer
    {
        void Render(
            CortexShellState state, 
            WorkbenchPresentationSnapshot snapshot, 
            bool isVisible,
            Action<int, Rect> drawWindowAction,
            Action<int, Rect> drawLogsWindowAction,
            Action drawOnboardingAction);
            
        void EnsureStyles(ThemeTokenSet themeTokens, string themeId);
        GUIStyle WindowStyle { get; }
        GUIStyle SectionStyle { get; }
    }

    internal sealed class CortexShellRenderer : ICortexShellRenderer
    {
        private readonly ShellLayoutCoordinator _layoutCoordinator;
        private readonly ShellStatusPresenter _statusPresenter;
        private readonly Func<GUISkin, GUISkin> _skinProvider;

        private GUIStyle _titleStyle;
        private GUIStyle _menuStyle;
        private GUIStyle _statusStyle;
        private GUIStyle _captionStyle;
        private GUIStyle _sectionStyle;
        private GUIStyle _windowStyle;
        private GUIStyle _tabStyle;
        private GUIStyle _activeTabStyle;
        private GUIStyle _tabCloseButtonStyle;
        private GUIStyle _collapsedWindowStyle;
        
        private Texture2D _windowBackground;
        private Texture2D _sectionBackground;
        private Texture2D _tabBackground;
        private Texture2D _tabActiveBackground;
        private Texture2D _collapsedWindowBackground;
        
        private string _appliedThemeId = string.Empty;

        public CortexShellRenderer(
            ShellLayoutCoordinator layoutCoordinator,
            ShellStatusPresenter statusPresenter,
            Func<GUISkin, GUISkin> skinProvider)
        {
            _layoutCoordinator = layoutCoordinator;
            _statusPresenter = statusPresenter;
            _skinProvider = skinProvider;
        }

        public GUIStyle WindowStyle => _windowStyle;
        public GUIStyle SectionStyle => _sectionStyle;

        public void Render(
            CortexShellState state, 
            WorkbenchPresentationSnapshot snapshot, 
            bool isVisible,
            Action<int, Rect> drawWindowAction,
            Action<int, Rect> drawLogsWindowAction,
            Action drawOnboardingAction)
        {
            if (!isVisible) return;

            CortexIdeLayout.ApplyTheme(snapshot.ThemeTokens, snapshot.ActiveThemeId);
            EnsureStyles(snapshot.ThemeTokens, snapshot.ActiveThemeId);
            
            var previousSkin = GUI.skin;
            if (_skinProvider != null)
            {
                GUI.skin = _skinProvider(previousSkin);
            }

            if (state.Chrome.Main.IsCollapsed)
            {
                DrawCollapsedWindowButton(state.Chrome.Main, ">", "Cortex");
            }
            else
            {
                state.Chrome.Main.ExpandedRect = GUI.Window(0xC07E, state.Chrome.Main.ExpandedRect, (id) => drawWindowAction(id, state.Chrome.Main.ExpandedRect), "Cortex IDE", _windowStyle);
            }

            if (state.Logs.ShowDetachedWindow && !state.Onboarding.IsActive)
            {
                if (state.Chrome.Logs.IsCollapsed)
                {
                    DrawCollapsedWindowButton(state.Chrome.Logs, ">", "Logs");
                }
                else
                {
                    state.Chrome.Logs.ExpandedRect = GUI.Window(0xC07F, state.Chrome.Logs.ExpandedRect, (id) => drawLogsWindowAction(id, state.Chrome.Logs.ExpandedRect), "Cortex Logs", _windowStyle);
                }
            }

            if (state.Onboarding.IsActive)
            {
                drawOnboardingAction?.Invoke();
            }

            GUI.skin = previousSkin;
        }

        public void EnsureStyles(ThemeTokenSet themeTokens, string themeId)
        {
            var effectiveThemeId = string.IsNullOrEmpty(themeId) ? "cortex.vs-dark" : themeId;
            if (!string.Equals(_appliedThemeId, effectiveThemeId, StringComparison.OrdinalIgnoreCase))
            {
                ClearStyles();
                _appliedThemeId = effectiveThemeId;
            }

            var textColor = CortexIdeLayout.ParseColor(themeTokens?.TextColor, new Color(0.96f, 0.96f, 0.96f, 1f));
            var mutedTextColor = CortexIdeLayout.ParseColor(themeTokens?.MutedTextColor, new Color(0.72f, 0.76f, 0.82f, 1f));
            var surfaceColor = CortexIdeLayout.ParseColor(themeTokens?.SurfaceColor, new Color(0.1f, 0.1f, 0.12f, 0.96f));
            var backgroundColor = CortexIdeLayout.ParseColor(themeTokens?.BackgroundColor, new Color(0.05f, 0.05f, 0.07f, 0.97f));
            var headerColor = CortexIdeLayout.ParseColor(themeTokens?.HeaderColor, new Color(0.16f, 0.17f, 0.2f, 1f));
            var accentColor = CortexIdeLayout.ParseColor(themeTokens?.AccentColor, new Color(0.22f, 0.3f, 0.4f, 1f));

            if (_titleStyle == null) { _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold }; GuiStyleUtil.ApplyTextColorToAllStates(_titleStyle, textColor); }
            if (_menuStyle == null) { _menuStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Normal, padding = new RectOffset(6, 6, 3, 3) }; GuiStyleUtil.ApplyTextColorToAllStates(_menuStyle, textColor); }
            if (_statusStyle == null) { _statusStyle = new GUIStyle(GUI.skin.label) { wordWrap = true }; GuiStyleUtil.ApplyTextColorToAllStates(_statusStyle, CortexIdeLayout.ParseColor(themeTokens?.TextColor, new Color(0.88f, 0.88f, 0.9f, 1f))); }
            if (_captionStyle == null) { _captionStyle = new GUIStyle(GUI.skin.label) { wordWrap = true }; GuiStyleUtil.ApplyTextColorToAllStates(_captionStyle, mutedTextColor); }
            if (_sectionStyle == null) { _sectionStyle = new GUIStyle(GUI.skin.box); _sectionBackground = MakeTex(surfaceColor); GuiStyleUtil.ApplyBackgroundToAllStates(_sectionStyle, _sectionBackground); _sectionStyle.padding = new RectOffset(8, 8, 6, 6); _sectionStyle.margin = new RectOffset(0, 0, 0, 0); }
            if (_windowStyle == null) { _windowBackground = MakeTex(surfaceColor); _windowStyle = new GUIStyle(GUI.skin.window); GuiStyleUtil.ApplyBackgroundToAllStates(_windowStyle, _windowBackground); GuiStyleUtil.ApplyTextColorToAllStates(_windowStyle, textColor); _windowStyle.padding = new RectOffset(6, 6, 24, 6); _windowStyle.margin = new RectOffset(0, 0, 0, 0); }
            if (_tabStyle == null) { _tabBackground = MakeTex(headerColor); _tabStyle = new GUIStyle(GUI.skin.button); GuiStyleUtil.ApplyBackgroundToAllStates(_tabStyle, _tabBackground); GuiStyleUtil.ApplyTextColorToAllStates(_tabStyle, mutedTextColor); _tabStyle.alignment = TextAnchor.MiddleCenter; _tabStyle.padding = new RectOffset(10, 10, 5, 5); _tabStyle.margin = new RectOffset(0, 2, 0, 0); }
            if (_activeTabStyle == null) { _tabActiveBackground = MakeTex(accentColor); _activeTabStyle = new GUIStyle(_tabStyle); GuiStyleUtil.ApplyBackgroundToAllStates(_activeTabStyle, _tabActiveBackground); GuiStyleUtil.ApplyTextColorToAllStates(_activeTabStyle, Color.white); _activeTabStyle.fontStyle = FontStyle.Bold; }
            if (_tabCloseButtonStyle == null) { _tabCloseButtonStyle = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleCenter, fontSize = 10, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0) }; GuiStyleUtil.ApplyBackgroundToAllStates(_tabCloseButtonStyle, MakeTex(CortexIdeLayout.Blend(headerColor, backgroundColor, 0.45f))); GuiStyleUtil.ApplyTextColorToAllStates(_tabCloseButtonStyle, textColor); }
            if (_collapsedWindowStyle == null) { _collapsedWindowBackground = MakeTex(CortexIdeLayout.ParseColor(themeTokens?.HeaderColor, new Color(0.09f, 0.11f, 0.15f, 0.98f))); _collapsedWindowStyle = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(10, 10, 4, 4), fontStyle = FontStyle.Bold }; GuiStyleUtil.ApplyBackgroundToAllStates(_collapsedWindowStyle, _collapsedWindowBackground); GuiStyleUtil.ApplyTextColorToAllStates(_collapsedWindowStyle, textColor); }
        }

        private void ClearStyles()
        {
            _titleStyle = null; _menuStyle = null; _statusStyle = null; _captionStyle = null; _sectionStyle = null; _windowStyle = null;
            _tabStyle = null; _activeTabStyle = null; _tabCloseButtonStyle = null; _collapsedWindowStyle = null;
            _windowBackground = null; _sectionBackground = null; _tabBackground = null; _tabActiveBackground = null; _collapsedWindowBackground = null;
        }

        private void DrawCollapsedWindowButton(CortexWindowChromeState chromeState, string glyph, string title)
        {
            if (chromeState != null && CortexWindowChromeController.DrawCollapsedButton(chromeState.CollapsedRect, glyph + " " + title, _collapsedWindowStyle)) 
                chromeState.IsCollapsed = false;
        }

        private static Texture2D MakeTex(Color color) { var texture = new Texture2D(1, 1); texture.SetPixel(0, 0, color); texture.Apply(); return texture; }

        // ── Drawing Helpers (formerly in CortexShell.Chrome.cs) ────────────────────────

        public void DrawHeader(WorkbenchPresentationSnapshot snapshot, CortexShellState state, Action<string> onMenuToggle, string openMenuGroup, Dictionary<string, Rect> menuGroupRects, Action<CortexWindowAction> onAction)
        {
            GUILayout.BeginHorizontal(_sectionStyle, GUILayout.Height(26f));
            GUILayout.Label("Cortex", _titleStyle, GUILayout.Width(56f));
            GUILayout.Space(6f);

            DrawMenuBar(snapshot, onMenuToggle, openMenuGroup, menuGroupRects);

            GUILayout.FlexibleSpace();

            if (state.SelectedProject != null)
            {
                GUILayout.Label(state.SelectedProject.GetDisplayName(), _captionStyle, GUILayout.ExpandWidth(false));
                GUILayout.Space(12f);
            }

            var actions = new List<CortexWindowAction>();
            // Note: Actions are built by the caller or passed in. 
            // For now, let's assume the caller handles the chrome buttons as they are functional.
            GUILayout.EndHorizontal();
        }

        private void DrawMenuBar(WorkbenchPresentationSnapshot snapshot, Action<string> onMenuToggle, string openMenuGroup, Dictionary<string, Rect> menuGroupRects)
        {
            var menuItems = snapshot?.MainMenuItems;
            var staticGroups = new[] { "File", "Edit", "View", "Build", "Window" };

            if (menuItems == null || menuItems.Count == 0)
            {
                foreach (var group in staticGroups) DrawStaticMenuGroup(group, onMenuToggle, openMenuGroup, menuGroupRects);
                return;
            }

            var seenGroups = new List<string>();
            for (var i = 0; i < menuItems.Count; i++)
            {
                var g = menuItems[i].Group ?? "Misc";
                if (!seenGroups.Contains(g)) seenGroups.Add(g);
            }

            foreach (var group in seenGroups) DrawStaticMenuGroup(group, onMenuToggle, openMenuGroup, menuGroupRects);
        }

        private void DrawStaticMenuGroup(string group, Action<string> onMenuToggle, string openMenuGroup, Dictionary<string, Rect> menuGroupRects)
        {
            var isOpen = string.Equals(openMenuGroup, group, StringComparison.OrdinalIgnoreCase);
            if (GUILayout.Button(group, _menuStyle, GUILayout.ExpandWidth(false)))
            {
                onMenuToggle?.Invoke(isOpen ? string.Empty : group);
            }
            menuGroupRects[group] = GUILayoutUtility.GetLastRect();
        }

        public void DrawStatusStrip(WorkbenchPresentationSnapshot snapshot, CortexShellState state)
        {
            _statusPresenter.DrawStatusStrip(snapshot, _sectionStyle, _statusStyle, _captionStyle);
        }

        public void DrawWorkbenchSurface(WorkbenchPresentationSnapshot snapshot, Rect workspaceRect)
        {
            _layoutCoordinator.DrawWorkbenchSurface(snapshot, workspaceRect, _tabStyle, _activeTabStyle, _tabCloseButtonStyle, _captionStyle);
        }
    }
}
