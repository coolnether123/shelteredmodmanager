using Cortex.Core.Models;
using Cortex.Plugins.Abstractions;

namespace Cortex.Host.Sheltered.Composition
{
    internal sealed class ShelteredWorkbenchAppearanceContributions
    {
        public void Register(WorkbenchPluginContext context, string rendererDisplayName)
        {
            if (context == null)
            {
                return;
            }

            context.RegisterTheme(new ThemeContribution
            {
                ThemeId = "cortex.vs-dark",
                DisplayName = "Visual Studio Dark",
                Description = "Dark Visual Studio-style chrome with a restrained blue workspace accent.",
                IsDefault = true,
                ShowInOnboarding = true,
                SupportsCustomColors = true,
                BackgroundColor = "#1E1E1E",
                SurfaceColor = "#252526",
                HeaderColor = "#2D2D30",
                BorderColor = "#3F3F46",
                AccentColor = "#5C8DFF",
                TextColor = "#D4D4D4",
                MutedTextColor = "#858585",
                WarningColor = "#C8A155",
                ErrorColor = "#F48771",
                FontRole = "compact-mono",
                SortOrder = 0
            });
            context.RegisterTheme(new ThemeContribution
            {
                ThemeId = "cortex.vs-blue",
                DisplayName = "Visual Studio Blue",
                Description = "Cool Visual Studio-inspired blue chrome with lighter pane separation.",
                IsDefault = false,
                ShowInOnboarding = true,
                SupportsCustomColors = true,
                BackgroundColor = "#EEF3F9",
                SurfaceColor = "#F7FAFE",
                HeaderColor = "#DCE7F4",
                BorderColor = "#AFC3D9",
                AccentColor = "#2F74C0",
                TextColor = "#1F2A37",
                MutedTextColor = "#5D7288",
                WarningColor = "#B67A11",
                ErrorColor = "#C94C4C",
                FontRole = "compact-mono",
                SortOrder = 10
            });
            context.RegisterTheme(new ThemeContribution
            {
                ThemeId = "cortex.vs-light",
                DisplayName = "Visual Studio Light",
                Description = "Neutral light theme with classic Visual Studio document and tool window contrast.",
                IsDefault = false,
                ShowInOnboarding = true,
                SupportsCustomColors = true,
                BackgroundColor = "#F3F3F3",
                SurfaceColor = "#FFFFFF",
                HeaderColor = "#E5E5E5",
                BorderColor = "#C9C9C9",
                AccentColor = "#006FC9",
                TextColor = "#1E1E1E",
                MutedTextColor = "#5F5F5F",
                WarningColor = "#B77B00",
                ErrorColor = "#C74646",
                FontRole = "compact-mono",
                SortOrder = 20
            });
            context.RegisterTheme(new ThemeContribution
            {
                ThemeId = "cortex.default",
                DisplayName = "Cortex Default",
                Description = "Alias of the Visual Studio Dark workbench theme.",
                IsDefault = false,
                ShowInOnboarding = false,
                SupportsCustomColors = true,
                BackgroundColor = "#1E1E1E",
                SurfaceColor = "#252526",
                HeaderColor = "#2D2D30",
                BorderColor = "#3F3F46",
                AccentColor = "#007ACC",
                TextColor = "#D4D4D4",
                MutedTextColor = "#858585",
                WarningColor = "#C8A155",
                ErrorColor = "#F48771",
                FontRole = "compact-mono",
                SortOrder = 25
            });
            context.RegisterTheme(new ThemeContribution
            {
                ThemeId = "cortex.dotpeek-dark",
                DisplayName = "dotPeek Dark",
                Description = "JetBrains-style dark decompiler chrome with violet selection accents.",
                IsDefault = false,
                ShowInOnboarding = true,
                SupportsCustomColors = true,
                BackgroundColor = "#1E2127",
                SurfaceColor = "#2A2D34",
                HeaderColor = "#313540",
                BorderColor = "#4A4F5D",
                AccentColor = "#9B5DE5",
                TextColor = "#D9DEE7",
                MutedTextColor = "#98A0AE",
                WarningColor = "#C8A155",
                ErrorColor = "#E06C75",
                FontRole = "compact-mono",
                SortOrder = 30
            });
            context.RegisterTheme(new ThemeContribution
            {
                ThemeId = "cortex.dotpeek-light",
                DisplayName = "dotPeek Light",
                Description = "JetBrains-style light chrome with soft violet accents and crisp tree separation.",
                IsDefault = false,
                ShowInOnboarding = true,
                SupportsCustomColors = true,
                BackgroundColor = "#F4F5F7",
                SurfaceColor = "#FFFFFF",
                HeaderColor = "#ECEEF2",
                BorderColor = "#C7CCD8",
                AccentColor = "#8A56D8",
                TextColor = "#242933",
                MutedTextColor = "#697180",
                WarningColor = "#AF7A1F",
                ErrorColor = "#C94F5D",
                FontRole = "compact-mono",
                SortOrder = 40
            });
            context.RegisterTheme(new ThemeContribution
            {
                ThemeId = "cortex.one-dark-pro",
                DisplayName = "One Dark Pro",
                Description = "Atom-inspired dark theme with balanced contrast and one of the most installed VS Code looks.",
                IsDefault = false,
                ShowInOnboarding = true,
                SupportsCustomColors = true,
                BackgroundColor = "#282C34",
                SurfaceColor = "#2F343F",
                HeaderColor = "#21252B",
                BorderColor = "#3B4048",
                AccentColor = "#61AFEF",
                TextColor = "#ABB2BF",
                MutedTextColor = "#7F848E",
                WarningColor = "#E5C07B",
                ErrorColor = "#E06C75",
                FontRole = "compact-mono",
                SortOrder = 50
            });
            context.RegisterTheme(new ThemeContribution
            {
                ThemeId = "cortex.dracula",
                DisplayName = "Dracula",
                Description = "Widely used cross-IDE dark theme with bold violet, pink, and cyan contrast.",
                IsDefault = false,
                ShowInOnboarding = true,
                SupportsCustomColors = true,
                BackgroundColor = "#282A36",
                SurfaceColor = "#303341",
                HeaderColor = "#21222C",
                BorderColor = "#44475A",
                AccentColor = "#BD93F9",
                TextColor = "#F8F8F2",
                MutedTextColor = "#A4A9C2",
                WarningColor = "#F1FA8C",
                ErrorColor = "#FF5555",
                FontRole = "compact-mono",
                SortOrder = 60
            });
            context.RegisterTheme(new ThemeContribution
            {
                ThemeId = "cortex.tokyo-night",
                DisplayName = "Tokyo Night",
                Description = "Calm night-shift theme with low-contrast chrome and cool blue-violet highlights.",
                IsDefault = false,
                ShowInOnboarding = true,
                SupportsCustomColors = true,
                BackgroundColor = "#1A1B26",
                SurfaceColor = "#24283B",
                HeaderColor = "#16161E",
                BorderColor = "#414868",
                AccentColor = "#7AA2F7",
                TextColor = "#C0CAF5",
                MutedTextColor = "#7A88B7",
                WarningColor = "#E0AF68",
                ErrorColor = "#F7768E",
                FontRole = "compact-mono",
                SortOrder = 70
            });
            context.RegisterTheme(new ThemeContribution
            {
                ThemeId = "cortex.phosphor",
                DisplayName = "Phosphor Grid",
                Description = "Green phosphor workstation styling for a more diagnostic terminal feel.",
                IsDefault = false,
                ShowInOnboarding = false,
                SupportsCustomColors = true,
                BackgroundColor = "#07100b",
                SurfaceColor = "#0d1711",
                HeaderColor = "#173021",
                BorderColor = "#24523a",
                AccentColor = "#58d68d",
                TextColor = "#d8f5df",
                MutedTextColor = "#9cc7aa",
                WarningColor = "#c8f268",
                ErrorColor = "#ff7d6b",
                FontRole = "terminal",
                SortOrder = 80
            });

            context.RegisterStatusItem(new StatusItemContribution
            {
                ItemId = "cortex.status.renderer",
                Text = rendererDisplayName,
                ToolTip = "Active Cortex renderer backend.",
                CommandId = string.Empty,
                Severity = "Info",
                Alignment = StatusItemAlignment.Right,
                Priority = 100
            });
        }
    }
}
