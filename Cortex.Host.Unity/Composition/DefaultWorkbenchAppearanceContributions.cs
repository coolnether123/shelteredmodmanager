using Cortex.Core.Models;
using Cortex.Plugins.Abstractions;

namespace Cortex.Host.Unity.Composition
{
    internal sealed class DefaultWorkbenchAppearanceContributions
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
                Description = "A clean, dark theme matching Visual Studio 2022.",
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
                SortOrder = 0
            });
            context.RegisterTheme(new ThemeContribution
            {
                ThemeId = "cortex.default",
                DisplayName = "Cortex Default",
                Description = "Alias of the Visual Studio Dark workbench theme.",
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
                SortOrder = 1
            });
            context.RegisterTheme(new ThemeContribution
            {
                ThemeId = "cortex.classic",
                DisplayName = "Classic Terminal",
                Description = "Warm amber terminal styling with brass highlights and softer contrast.",
                BackgroundColor = "#120d09",
                SurfaceColor = "#1f1711",
                HeaderColor = "#38261a",
                BorderColor = "#59402a",
                AccentColor = "#f0a24a",
                TextColor = "#f6e0bf",
                MutedTextColor = "#cfb08d",
                WarningColor = "#f2d46e",
                ErrorColor = "#ef7c57",
                FontRole = "terminal",
                SortOrder = 10
            });
            context.RegisterTheme(new ThemeContribution
            {
                ThemeId = "cortex.phosphor",
                DisplayName = "Phosphor Grid",
                Description = "Green phosphor workstation styling for a more diagnostic terminal feel.",
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
                SortOrder = 20
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
