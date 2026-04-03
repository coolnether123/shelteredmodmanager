using System.Collections.Generic;
using Cortex.Core.Models;

namespace Cortex.Presentation.Models
{
    public sealed class FocusState
    {
        public string FocusedRegionId;
        public string FocusedControlId;
        public string CommandTargetId;
        public readonly List<string> FocusHistory;

        public FocusState()
        {
            FocusedRegionId = string.Empty;
            FocusedControlId = string.Empty;
            CommandTargetId = string.Empty;
            FocusHistory = new List<string>();
        }
    }

    public sealed class ThemeTokenSet
    {
        public string BackgroundColor;
        public string SurfaceColor;
        public string HeaderColor;
        public string BorderColor;
        public string AccentColor;
        public string TextColor;
        public string MutedTextColor;
        public string WarningColor;
        public string ErrorColor;
        public string FontRole;

        public ThemeTokenSet()
        {
            BackgroundColor = "#1E1E1E";
            SurfaceColor = "#252526";
            HeaderColor = "#2D2D30";
            BorderColor = "#3F3F46";
            AccentColor = "#007ACC";
            TextColor = "#D4D4D4";
            MutedTextColor = "#858585";
            WarningColor = "#C8A155";
            ErrorColor = "#F48771";
            FontRole = "compact-mono";
        }
    }

    public sealed class ToolRailItem
    {
        public string ContainerId;
        public string Title;
        public string IconId;
        public string IconAlias;
        public WorkbenchHostLocation HostLocation;
        public bool Active;
    }

    public sealed class MenuItemProjection
    {
        public string CommandId;
        public string DisplayName;
        public string Description;
        public string DefaultGesture;
        public string Group;
        public string IconAlias;
        public MenuProjectionLocation Location;
        public int SortOrder;
    }

    public sealed class WorkbenchPresentationMetadata
    {
        public string RendererSummary;

        public WorkbenchPresentationMetadata()
        {
            RendererSummary = string.Empty;
        }
    }

    public sealed class WorkbenchPresentationSnapshot
    {
        public readonly List<ToolRailItem> ToolRailItems;
        public readonly List<MenuItemProjection> MainMenuItems;
        public readonly List<MenuItemProjection> ToolbarItems;
        public readonly List<StatusItemContribution> LeftStatusItems;
        public readonly List<StatusItemContribution> RightStatusItems;
        public readonly List<ThemeContribution> Themes;
        public readonly List<OnboardingProfileContribution> OnboardingProfiles;
        public readonly List<OnboardingLayoutPresetContribution> OnboardingLayoutPresets;
        public readonly List<EditorContribution> Editors;
        public readonly List<SettingSectionContribution> SettingSections;
        public readonly List<SettingContribution> Settings;
        public readonly ThemeTokenSet ThemeTokens;
        public string ActiveContainerId;
        public string FocusedRegionId;
        public string ActiveThemeId;
        public string RendererSummary;

        public WorkbenchPresentationSnapshot()
        {
            ToolRailItems = new List<ToolRailItem>();
            MainMenuItems = new List<MenuItemProjection>();
            ToolbarItems = new List<MenuItemProjection>();
            LeftStatusItems = new List<StatusItemContribution>();
            RightStatusItems = new List<StatusItemContribution>();
            Themes = new List<ThemeContribution>();
            OnboardingProfiles = new List<OnboardingProfileContribution>();
            OnboardingLayoutPresets = new List<OnboardingLayoutPresetContribution>();
            Editors = new List<EditorContribution>();
            SettingSections = new List<SettingSectionContribution>();
            Settings = new List<SettingContribution>();
            ThemeTokens = new ThemeTokenSet();
            ActiveContainerId = string.Empty;
            FocusedRegionId = string.Empty;
            ActiveThemeId = "cortex.vs-dark";
            RendererSummary = string.Empty;
        }
    }
}
