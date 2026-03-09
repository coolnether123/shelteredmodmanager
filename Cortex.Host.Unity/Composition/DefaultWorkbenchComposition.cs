using Cortex.Core.Abstractions;
using Cortex.Core.Models;

namespace Cortex.Host.Unity.Composition
{
    internal static class DefaultWorkbenchComposition
    {
        public static void RegisterBuiltIns(
            ICommandRegistry commandRegistry,
            IContributionRegistry contributionRegistry,
            string rendererDisplayName)
        {
            RegisterContainer(contributionRegistry, CortexWorkbenchIds.LogsContainer, "Logs", WorkbenchHostLocation.PanelHost, 0, ModuleActivationKind.OnContainerOpen, CortexWorkbenchIds.LogsContainer);
            RegisterContainer(contributionRegistry, CortexWorkbenchIds.ProjectsContainer, "Projects", WorkbenchHostLocation.PrimarySideHost, 10, ModuleActivationKind.OnWorkspaceAvailable, "workspace");
            RegisterContainer(contributionRegistry, CortexWorkbenchIds.EditorContainer, "Editor", WorkbenchHostLocation.DocumentHost, 20, ModuleActivationKind.OnDocumentRestore, CortexWorkbenchIds.EditorContainer);
            RegisterContainer(contributionRegistry, CortexWorkbenchIds.BuildContainer, "Build", WorkbenchHostLocation.PanelHost, 30, ModuleActivationKind.OnCommand, "cortex.build.execute");
            RegisterContainer(contributionRegistry, CortexWorkbenchIds.ReferenceContainer, "Reference", WorkbenchHostLocation.PrimarySideHost, 40, ModuleActivationKind.OnContainerOpen, CortexWorkbenchIds.ReferenceContainer);
            RegisterContainer(contributionRegistry, CortexWorkbenchIds.RuntimeContainer, "Runtime", WorkbenchHostLocation.PrimarySideHost, 50, ModuleActivationKind.OnContainerOpen, CortexWorkbenchIds.RuntimeContainer);
            RegisterContainer(contributionRegistry, CortexWorkbenchIds.SettingsContainer, "Settings", WorkbenchHostLocation.PrimarySideHost, 60, ModuleActivationKind.OnContainerOpen, CortexWorkbenchIds.SettingsContainer);

            RegisterCommand(commandRegistry, "cortex.shell.toggle", "Toggle Cortex", "Workbench", "Show or hide the Cortex shell.", "F8", 0, true);
            RegisterCommand(commandRegistry, "cortex.logs.toggleWindow", "Toggle Detached Logs", "Logs", "Show or hide the detached log window.", string.Empty, 10, false);
            RegisterCommand(commandRegistry, "cortex.shell.fitWindow", "Fit Workbench To Screen", "Workbench", "Resize the shell to fill most of the game view.", string.Empty, 20, false);
            RegisterCommand(commandRegistry, "cortex.build.execute", "Open Build Panel", "Build", "Focus the build panel and activate build tooling.", string.Empty, 30, false);
            RegisterMenu(contributionRegistry, "cortex.logs.toggleWindow", MenuProjectionLocation.MainMenu, "Window", 10);
            RegisterMenu(contributionRegistry, "cortex.shell.fitWindow", MenuProjectionLocation.MainMenu, "Window", 20);
            RegisterMenu(contributionRegistry, "cortex.build.execute", MenuProjectionLocation.MainMenu, "Build", 30);
            RegisterMenu(contributionRegistry, "cortex.logs.toggleWindow", MenuProjectionLocation.Toolbar, "Window", 10);
            RegisterMenu(contributionRegistry, "cortex.shell.fitWindow", MenuProjectionLocation.Toolbar, "Window", 20);
            RegisterMenu(contributionRegistry, "cortex.build.execute", MenuProjectionLocation.Toolbar, "Build", 30);

            RegisterTheme(
                contributionRegistry,
                "cortex.default",
                "Cortex Default",
                "Current Cortex shell styling with the existing dark steel palette.",
                "#0c0c11",
                "#181922",
                "#252835",
                "#303545",
                "#4db3ff",
                "#f2f4f8",
                "#c0c6d4",
                "#f2c14e",
                "#ff6b6b",
                "compact-mono",
                0);
            RegisterTheme(
                contributionRegistry,
                "cortex.classic",
                "Classic Terminal",
                "Warm amber terminal styling with brass highlights and softer contrast.",
                "#120d09",
                "#1f1711",
                "#38261a",
                "#59402a",
                "#f0a24a",
                "#f6e0bf",
                "#cfb08d",
                "#f2d46e",
                "#ef7c57",
                "terminal",
                10);
            RegisterTheme(
                contributionRegistry,
                "cortex.phosphor",
                "Phosphor Grid",
                "Green phosphor workstation styling for a more diagnostic terminal feel.",
                "#07100b",
                "#0d1711",
                "#173021",
                "#24523a",
                "#58d68d",
                "#d8f5df",
                "#9cc7aa",
                "#c8f268",
                "#ff7d6b",
                "terminal",
                20);

            RegisterIcon(contributionRegistry, CortexWorkbenchIds.LogsContainer, "LG");
            RegisterIcon(contributionRegistry, CortexWorkbenchIds.ProjectsContainer, "PJ");
            RegisterIcon(contributionRegistry, CortexWorkbenchIds.EditorContainer, "ED");
            RegisterIcon(contributionRegistry, CortexWorkbenchIds.BuildContainer, "BL");
            RegisterIcon(contributionRegistry, CortexWorkbenchIds.ReferenceContainer, "RF");
            RegisterIcon(contributionRegistry, CortexWorkbenchIds.RuntimeContainer, "RT");
            RegisterIcon(contributionRegistry, CortexWorkbenchIds.SettingsContainer, "ST");

            RegisterEditor(contributionRegistry, "cortex.editor.code", "Code Editor", ".cs", "text/x-csharp", 0);
            RegisterEditor(contributionRegistry, "cortex.editor.text", "Text Editor", ".txt", "text/plain", 10);
            RegisterEditor(contributionRegistry, "cortex.editor.log", "Log Viewer", ".log", "text/plain", 20);

            RegisterSetting(contributionRegistry, nameof(CortexSettings.WorkspaceRootPath), "Workspace Scan Root", "Tell Cortex where to scan for editable workspace sources.", "Workspace", string.Empty, SettingValueKind.String, 0);
            RegisterSetting(contributionRegistry, nameof(CortexSettings.ModsRootPath), "Loaded Mods Root", "Points to the live mod folder used for source mapping and discovery.", "Workspace", string.Empty, SettingValueKind.String, 10);
            RegisterSetting(contributionRegistry, nameof(CortexSettings.ManagedAssemblyRootPath), "Game Managed DLLs", "Used to locate assemblies for reference browsing and decompilation.", "Workspace", string.Empty, SettingValueKind.String, 20);
            RegisterSetting(contributionRegistry, nameof(CortexSettings.AdditionalSourceRoots), "Extra Source Roots", "Semicolon-separated fallback roots for source resolution.", "Workspace", string.Empty, SettingValueKind.String, 30);
            RegisterSetting(contributionRegistry, nameof(CortexSettings.ProjectCatalogPath), "Project Catalog", "Path to the persisted Cortex project catalog file.", "Workspace", string.Empty, SettingValueKind.String, 40);

            RegisterSetting(contributionRegistry, nameof(CortexSettings.LogFilePath), "Live Log File", "Optional file that is tailed under the live in-memory log feed.", "Logs", string.Empty, SettingValueKind.String, 0);
            RegisterSetting(contributionRegistry, nameof(CortexSettings.MaxRecentLogs), "Max Recent Logs", "Maximum number of live log entries to keep in the in-memory feed.", "Logs", "300", SettingValueKind.Integer, 10);
            RegisterSetting(contributionRegistry, nameof(CortexSettings.AutoScrollLogs), "Auto-scroll Log List", "Keep the live log list pinned to the newest entry.", "Logs", "true", SettingValueKind.Boolean, 20);
            RegisterSetting(contributionRegistry, nameof(CortexSettings.ShowLogBacklog), "Show File Tail History", "Optional raw file tail for lines that were written before Cortex attached or were not captured in the live feed.", "Logs", "false", SettingValueKind.Boolean, 30);

            RegisterSetting(contributionRegistry, nameof(CortexSettings.DecompilerPathOverride), "Decompiler Override", "Optional path to a custom decompiler executable.", "Decompiler", string.Empty, SettingValueKind.String, 0);
            RegisterSetting(contributionRegistry, nameof(CortexSettings.DecompilerCachePath), "Decompiler Cache", "Location used to cache generated source from runtime and reference browsing.", "Decompiler", string.Empty, SettingValueKind.String, 10);

            RegisterSetting(contributionRegistry, nameof(CortexSettings.DefaultBuildConfiguration), "Default Build Config", "Default build configuration used by build tooling.", "Build", "Debug", SettingValueKind.String, 0);
            RegisterSetting(contributionRegistry, nameof(CortexSettings.BuildTimeoutMs), "Build Timeout (ms)", "Maximum time allowed for build execution before timing out.", "Build", "300000", SettingValueKind.Integer, 10);

            RegisterSetting(contributionRegistry, nameof(CortexSettings.ThemeId), "Theme", "Active workbench theme identifier.", "Appearance", "cortex.default", SettingValueKind.String, 0);
            RegisterSetting(contributionRegistry, nameof(CortexSettings.LogsPaneWidth), "Logs Pane Width", "Preferred width for the logs/details split.", "Layout", "520", SettingValueKind.Float, 0);
            RegisterSetting(contributionRegistry, nameof(CortexSettings.ProjectsPaneWidth), "Projects Pane Width", "Preferred width for the side host.", "Layout", "360", SettingValueKind.Float, 10);
            RegisterSetting(contributionRegistry, nameof(CortexSettings.EditorFilePaneWidth), "Explorer Width", "Preferred width for the editor explorer pane.", "Layout", "320", SettingValueKind.Float, 20);
            RegisterSetting(contributionRegistry, nameof(CortexSettings.WindowX), "Window X", "Saved shell position on the X axis.", "Window", "70", SettingValueKind.Float, 0);
            RegisterSetting(contributionRegistry, nameof(CortexSettings.WindowY), "Window Y", "Saved shell position on the Y axis.", "Window", "70", SettingValueKind.Float, 10);
            RegisterSetting(contributionRegistry, nameof(CortexSettings.WindowWidth), "Window Width", "Saved shell width.", "Window", "1180", SettingValueKind.Float, 20);
            RegisterSetting(contributionRegistry, nameof(CortexSettings.WindowHeight), "Window Height", "Saved shell height.", "Window", "760", SettingValueKind.Float, 30);

            contributionRegistry.RegisterStatusItem(new StatusItemContribution
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

        private static void RegisterMenu(
            IContributionRegistry contributionRegistry,
            string commandId,
            MenuProjectionLocation location,
            string group,
            int sortOrder)
        {
            contributionRegistry.RegisterMenu(new MenuContribution
            {
                CommandId = commandId,
                Location = location,
                Group = group,
                SortOrder = sortOrder
            });
        }

        private static void RegisterTheme(
            IContributionRegistry contributionRegistry,
            string themeId,
            string displayName,
            string description,
            string backgroundColor,
            string surfaceColor,
            string headerColor,
            string borderColor,
            string accentColor,
            string textColor,
            string mutedTextColor,
            string warningColor,
            string errorColor,
            string fontRole,
            int sortOrder)
        {
            contributionRegistry.RegisterTheme(new ThemeContribution
            {
                ThemeId = themeId,
                DisplayName = displayName,
                Description = description,
                BackgroundColor = backgroundColor,
                SurfaceColor = surfaceColor,
                HeaderColor = headerColor,
                BorderColor = borderColor,
                AccentColor = accentColor,
                TextColor = textColor,
                MutedTextColor = mutedTextColor,
                WarningColor = warningColor,
                ErrorColor = errorColor,
                FontRole = fontRole,
                SortOrder = sortOrder
            });
        }

        private static void RegisterIcon(IContributionRegistry contributionRegistry, string iconId, string alias)
        {
            contributionRegistry.RegisterIcon(new IconContribution
            {
                IconId = iconId,
                Alias = alias
            });
        }

        private static void RegisterEditor(
            IContributionRegistry contributionRegistry,
            string editorId,
            string displayName,
            string extension,
            string contentType,
            int sortOrder)
        {
            contributionRegistry.RegisterEditor(new EditorContribution
            {
                EditorId = editorId,
                DisplayName = displayName,
                ResourceExtension = extension,
                ContentType = contentType,
                SortOrder = sortOrder
            });
        }

        private static void RegisterSetting(
            IContributionRegistry contributionRegistry,
            string settingId,
            string displayName,
            string description,
            string scope,
            string defaultValue,
            SettingValueKind valueKind,
            int sortOrder)
        {
            contributionRegistry.RegisterSetting(new SettingContribution
            {
                SettingId = settingId,
                DisplayName = displayName,
                Description = description,
                Scope = scope,
                DefaultValue = defaultValue,
                ValueKind = valueKind,
                SortOrder = sortOrder
            });
        }

        private static void RegisterContainer(
            IContributionRegistry contributionRegistry,
            string containerId,
            string title,
            WorkbenchHostLocation hostLocation,
            int sortOrder,
            ModuleActivationKind activationKind,
            string activationTarget)
        {
            contributionRegistry.RegisterViewContainer(new ViewContainerContribution
            {
                ContainerId = containerId,
                Title = title,
                IconId = containerId,
                DefaultHostLocation = hostLocation,
                SortOrder = sortOrder,
                PinnedByDefault = true,
                ActivationKind = activationKind,
                ActivationTarget = activationTarget
            });

            contributionRegistry.RegisterView(new ViewContribution
            {
                ViewId = containerId + ".main",
                ContainerId = containerId,
                Title = title,
                PersistenceId = containerId + ".main",
                SortOrder = 0,
                VisibleByDefault = true
            });
        }

        private static void RegisterCommand(
            ICommandRegistry commandRegistry,
            string commandId,
            string displayName,
            string category,
            string description,
            string gesture,
            int sortOrder,
            bool isGlobal)
        {
            commandRegistry.Register(new CommandDefinition
            {
                CommandId = commandId,
                DisplayName = displayName,
                Category = category,
                Description = description,
                DefaultGesture = gesture,
                SortOrder = sortOrder,
                ShowInPalette = true,
                IsGlobal = isGlobal
            });
        }
    }
}
