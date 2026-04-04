using System.Collections.Generic;
using Cortex.Host.Avalonia.Models;

namespace Cortex.Host.Avalonia.Services
{
    internal sealed class DesktopWorkbenchSurfaceRegistry
    {
        public const string LeftGroupId = "cortex.group.left";
        public const string CenterGroupId = "cortex.group.center";
        public const string RightGroupId = "cortex.group.right";
        public const string BottomGroupId = "cortex.group.bottom";

        public const string OnboardingSurfaceId = "cortex.surface.onboarding";
        public const string WorkspaceSurfaceId = "cortex.surface.workspace";
        public const string EditorSurfaceId = "cortex.surface.editor";
        public const string SettingsSurfaceId = "cortex.surface.settings";
        public const string ReferenceSurfaceId = "cortex.surface.reference";
        public const string SearchSurfaceId = "cortex.surface.search";
        public const string StatusSurfaceId = "cortex.surface.status";

        public DesktopWorkbenchSurfaceRegistry()
        {
            Definitions = new List<DesktopWorkbenchSurfaceDefinition>
            {
                new DesktopWorkbenchSurfaceDefinition
                {
                    SurfaceId = OnboardingSurfaceId,
                    Title = "Onboarding",
                    DefaultGroupId = LeftGroupId
                },
                new DesktopWorkbenchSurfaceDefinition
                {
                    SurfaceId = WorkspaceSurfaceId,
                    Title = "Workspace",
                    DefaultGroupId = CenterGroupId,
                    IsDocument = true,
                    IsRequired = true
                },
                new DesktopWorkbenchSurfaceDefinition
                {
                    SurfaceId = EditorSurfaceId,
                    Title = "Editor",
                    DefaultGroupId = CenterGroupId,
                    IsDocument = true
                },
                new DesktopWorkbenchSurfaceDefinition
                {
                    SurfaceId = SettingsSurfaceId,
                    Title = "Settings",
                    DefaultGroupId = RightGroupId
                },
                new DesktopWorkbenchSurfaceDefinition
                {
                    SurfaceId = ReferenceSurfaceId,
                    Title = "Reference",
                    DefaultGroupId = RightGroupId
                },
                new DesktopWorkbenchSurfaceDefinition
                {
                    SurfaceId = SearchSurfaceId,
                    Title = "Search",
                    DefaultGroupId = BottomGroupId
                },
                new DesktopWorkbenchSurfaceDefinition
                {
                    SurfaceId = StatusSurfaceId,
                    Title = "Runtime",
                    DefaultGroupId = BottomGroupId
                }
            };
        }

        public IReadOnlyList<DesktopWorkbenchSurfaceDefinition> Definitions { get; }
    }
}
