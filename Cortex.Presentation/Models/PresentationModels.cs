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
            BackgroundColor = "#0c0c11";
            SurfaceColor = "#181922";
            HeaderColor = "#252835";
            BorderColor = "#303545";
            AccentColor = "#4db3ff";
            TextColor = "#f2f4f8";
            MutedTextColor = "#c0c6d4";
            WarningColor = "#f2c14e";
            ErrorColor = "#ff6b6b";
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

    public sealed class WorkbenchPresentationSnapshot
    {
        public readonly List<ToolRailItem> ToolRailItems;
        public readonly List<MenuItemProjection> MainMenuItems;
        public readonly List<MenuItemProjection> ToolbarItems;
        public readonly List<StatusItemContribution> LeftStatusItems;
        public readonly List<StatusItemContribution> RightStatusItems;
        public readonly List<ThemeContribution> Themes;
        public readonly List<EditorContribution> Editors;
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
            Editors = new List<EditorContribution>();
            Settings = new List<SettingContribution>();
            ThemeTokens = new ThemeTokenSet();
            ActiveContainerId = string.Empty;
            FocusedRegionId = string.Empty;
            ActiveThemeId = "cortex.default";
            RendererSummary = string.Empty;
        }
    }
}
