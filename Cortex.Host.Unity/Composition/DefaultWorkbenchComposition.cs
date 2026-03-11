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
            // ── View containers (determines which host each module lives in) ─────────
            // Solution Explorer defaults to the right side like a traditional IDE.
            RegisterContainer(contributionRegistry, CortexWorkbenchIds.FileExplorerContainer, "Solution Explorer", WorkbenchHostLocation.SecondarySideHost, 0, ModuleActivationKind.OnContainerOpen, CortexWorkbenchIds.FileExplorerContainer);
            // Logs live in the bottom panel; Build output is a sibling tab.
            RegisterContainer(contributionRegistry, CortexWorkbenchIds.LogsContainer, "Logs", WorkbenchHostLocation.PanelHost, 0, ModuleActivationKind.OnContainerOpen, CortexWorkbenchIds.LogsContainer);
            RegisterContainer(contributionRegistry, CortexWorkbenchIds.BuildContainer, "Build", WorkbenchHostLocation.PanelHost, 10, ModuleActivationKind.OnCommand, "cortex.build.execute");
            RegisterContainer(contributionRegistry, CortexWorkbenchIds.RuntimeContainer, "Runtime", WorkbenchHostLocation.PanelHost, 20, ModuleActivationKind.OnContainerOpen, CortexWorkbenchIds.RuntimeContainer);
            // Secondary right side: project management and references.
            RegisterContainer(contributionRegistry, CortexWorkbenchIds.ProjectsContainer, "Projects", WorkbenchHostLocation.SecondarySideHost, 10, ModuleActivationKind.OnContainerOpen, CortexWorkbenchIds.ProjectsContainer);
            RegisterContainer(contributionRegistry, CortexWorkbenchIds.ReferenceContainer, "References", WorkbenchHostLocation.SecondarySideHost, 20, ModuleActivationKind.OnContainerOpen, CortexWorkbenchIds.ReferenceContainer);
            RegisterContainer(contributionRegistry, CortexWorkbenchIds.SearchContainer, "Search", WorkbenchHostLocation.PanelHost, 30, ModuleActivationKind.OnCommand, "cortex.editor.find");
            // Editor is the central document host.
            RegisterContainer(contributionRegistry, CortexWorkbenchIds.EditorContainer, "Editor", WorkbenchHostLocation.DocumentHost, 0, ModuleActivationKind.OnDocumentRestore, CortexWorkbenchIds.EditorContainer);

            // ── Commands ──────────────────────────────────────────────────────────────
            RegisterCommand(commandRegistry, "cortex.shell.toggle", "Toggle Cortex", "Workbench", "Show or hide the Cortex shell.", "F8", 0, true);
            RegisterCommand(commandRegistry, "cortex.shell.fitWindow", "Fit Workbench To Screen", "View", "Resize the shell to fill most of the game view.", string.Empty, 10, false);
            RegisterCommand(commandRegistry, "cortex.logs.toggleWindow", "Detached Logs Window", "View", "Show or hide the detached log window.", string.Empty, 20, false);
            RegisterCommand(commandRegistry, "cortex.build.execute", "Build Project", "Build", "Focus the build panel and trigger a build.", string.Empty, 0, false);
            RegisterCommand(commandRegistry, "cortex.file.saveAll", "Save All", "File", "Save all open documents.", string.Empty, 0, false);
            RegisterCommand(commandRegistry, "cortex.file.closeActive", "Close", "File", "Close the active document.", string.Empty, 10, false);
            RegisterCommand(commandRegistry, "cortex.file.settings", "Settings", "File", "Open the Cortex settings window.", string.Empty, 20, false);
            RegisterCommand(commandRegistry, "cortex.view.fileExplorer", "Toggle File Explorer", "View", "Show or hide the file explorer pane.", string.Empty, 0, false);
            RegisterCommand(commandRegistry, "cortex.view.zoomIn", "Increase Font Size", "View", "Increase the editor font size.", string.Empty, 10, false);
            RegisterCommand(commandRegistry, "cortex.view.zoomOut", "Decrease Font Size", "View", "Decrease the editor font size.", string.Empty, 20, false);
            RegisterCommand(commandRegistry, "cortex.win.theme", "Switch Theme", "View", "Cycle through registered themes.", string.Empty, 30, false);
            RegisterCommand(commandRegistry, "cortex.window.explorer", "Explorer", "Window", "Show the explorer tool window.", string.Empty, 0, false);
            RegisterCommand(commandRegistry, "cortex.window.projects", "Projects", "Window", "Show the projects tool window.", string.Empty, 10, false);
            RegisterCommand(commandRegistry, "cortex.window.references", "References", "Window", "Show the references tool window.", string.Empty, 20, false);
            RegisterCommand(commandRegistry, "cortex.window.search", "Search", "Window", "Show the search results tool window.", string.Empty, 30, false);
            RegisterCommand(commandRegistry, "cortex.window.logs", "Logs", "Window", "Show the logs tool window.", string.Empty, 40, false);
            RegisterCommand(commandRegistry, "cortex.window.build", "Build", "Window", "Show the build tool window.", string.Empty, 50, false);
            RegisterCommand(commandRegistry, "cortex.window.runtime", "Runtime", "Window", "Show the runtime tool window.", string.Empty, 60, false);
            RegisterCommand(commandRegistry, "cortex.window.settings", "Settings", "Window", "Show the Cortex settings window.", string.Empty, 70, false);
            RegisterCommand(commandRegistry, "cortex.editor.find", "Find...", "Editor", "Open the find bar for text search.", "Ctrl+F", 0, false);
            RegisterCommand(commandRegistry, "cortex.search.next", "Find Next", "Editor", "Advance to the next search result.", "F3", 110, false);
            RegisterCommand(commandRegistry, "cortex.search.previous", "Find Previous", "Editor", "Move to the previous search result.", "Shift+F3", 120, false);
            RegisterCommand(commandRegistry, "cortex.search.close", "Close Find", "Editor", "Close the active find bar.", "Escape", 130, false);
            RegisterCommand(commandRegistry, "cortex.editor.quickActions", "Quick Actions and Refactorings...", "Editor", "Show editor quick actions for the current symbol.", "Ctrl+.", 0, false);
            RegisterCommand(commandRegistry, "cortex.editor.rename", "Rename...", "Editor", "Rename the current symbol.", "F2", 10, false);
            RegisterCommand(commandRegistry, "cortex.editor.removeAndSortUsings", "Remove and Sort Usings", "Editor", "Clean and reorder using directives.", "Ctrl+R, Ctrl+G", 20, false);
            RegisterCommand(commandRegistry, "cortex.editor.peekDefinition", "Peek Definition", "Editor", "Open an inline peek view for the current symbol definition.", "Alt+F12", 30, false);
            RegisterCommand(commandRegistry, "cortex.editor.goToDefinition", "Go To Definition", "Editor", "Navigate to the current symbol definition.", "F12", 40, false);
            RegisterCommand(commandRegistry, "cortex.editor.goToBase", "Go To Base", "Editor", "Navigate to the base symbol.", "Alt+Home", 50, false);
            RegisterCommand(commandRegistry, "cortex.editor.goToImplementation", "Go To Implementation", "Editor", "Navigate to the current symbol implementation.", "Ctrl+F12", 60, false);
            RegisterCommand(commandRegistry, "cortex.editor.findAllReferences", "Find All References", "Editor", "Find references for the current symbol.", "Ctrl+K, R", 70, false);
            RegisterCommand(commandRegistry, "cortex.editor.viewCallHierarchy", "View Call Hierarchy", "Editor", "Show call hierarchy for the current symbol.", "Ctrl+K, Ctrl+T", 80, false);
            RegisterCommand(commandRegistry, "cortex.editor.copySymbol", "Copy Symbol", "Editor", "Copy the selected symbol text.", "Ctrl+C", 90, false);
            RegisterCommand(commandRegistry, "cortex.editor.copyHoverInfo", "Copy Hover Info", "Editor", "Copy the current hover/signature information.", string.Empty, 100, false);

            // ── Menu projections (File / Edit / View / Build / Window) ────────────────
            RegisterMenu(contributionRegistry, "cortex.file.saveAll", MenuProjectionLocation.MainMenu, "File", 0, string.Empty);
            RegisterMenu(contributionRegistry, "cortex.file.closeActive", MenuProjectionLocation.MainMenu, "File", 10, string.Empty);
            RegisterMenu(contributionRegistry, "cortex.file.settings", MenuProjectionLocation.MainMenu, "File", 20, string.Empty);
            RegisterMenu(contributionRegistry, "cortex.view.fileExplorer", MenuProjectionLocation.MainMenu, "View", 0, string.Empty);
            RegisterMenu(contributionRegistry, "cortex.view.zoomIn", MenuProjectionLocation.MainMenu, "View", 10, string.Empty);
            RegisterMenu(contributionRegistry, "cortex.view.zoomOut", MenuProjectionLocation.MainMenu, "View", 20, string.Empty);
            RegisterMenu(contributionRegistry, "cortex.win.theme", MenuProjectionLocation.MainMenu, "View", 30, string.Empty);
            RegisterMenu(contributionRegistry, "cortex.logs.toggleWindow", MenuProjectionLocation.MainMenu, "View", 40, string.Empty);
            RegisterMenu(contributionRegistry, "cortex.window.explorer", MenuProjectionLocation.MainMenu, "Window", 0, string.Empty);
            RegisterMenu(contributionRegistry, "cortex.window.projects", MenuProjectionLocation.MainMenu, "Window", 10, string.Empty);
            RegisterMenu(contributionRegistry, "cortex.window.references", MenuProjectionLocation.MainMenu, "Window", 20, string.Empty);
            RegisterMenu(contributionRegistry, "cortex.window.search", MenuProjectionLocation.MainMenu, "Window", 30, string.Empty);
            RegisterMenu(contributionRegistry, "cortex.window.logs", MenuProjectionLocation.MainMenu, "Window", 40, string.Empty);
            RegisterMenu(contributionRegistry, "cortex.window.build", MenuProjectionLocation.MainMenu, "Window", 50, string.Empty);
            RegisterMenu(contributionRegistry, "cortex.window.runtime", MenuProjectionLocation.MainMenu, "Window", 60, string.Empty);
            RegisterMenu(contributionRegistry, "cortex.window.settings", MenuProjectionLocation.MainMenu, "Window", 70, string.Empty);
            RegisterMenu(contributionRegistry, "cortex.shell.fitWindow", MenuProjectionLocation.MainMenu, "Window", 110, string.Empty);
            RegisterMenu(contributionRegistry, "cortex.editor.find", MenuProjectionLocation.MainMenu, "Edit", 0, string.Empty);
            RegisterMenu(contributionRegistry, "cortex.build.execute", MenuProjectionLocation.MainMenu, "Build", 0, string.Empty);
            RegisterMenu(contributionRegistry, "cortex.editor.quickActions", MenuProjectionLocation.ContextMenu, "01_actions", 0, EditorContextIds.Symbol);
            RegisterMenu(contributionRegistry, "cortex.editor.rename", MenuProjectionLocation.ContextMenu, "01_actions", 10, EditorContextIds.Symbol);
            RegisterMenu(contributionRegistry, "cortex.editor.removeAndSortUsings", MenuProjectionLocation.ContextMenu, "01_actions", 20, EditorContextIds.Symbol);
            RegisterMenu(contributionRegistry, "cortex.editor.peekDefinition", MenuProjectionLocation.ContextMenu, "02_navigation", 0, EditorContextIds.Symbol);
            RegisterMenu(contributionRegistry, "cortex.editor.goToDefinition", MenuProjectionLocation.ContextMenu, "02_navigation", 10, EditorContextIds.Symbol);
            RegisterMenu(contributionRegistry, "cortex.editor.goToBase", MenuProjectionLocation.ContextMenu, "02_navigation", 20, EditorContextIds.Symbol);
            RegisterMenu(contributionRegistry, "cortex.editor.goToImplementation", MenuProjectionLocation.ContextMenu, "02_navigation", 30, EditorContextIds.Symbol);
            RegisterMenu(contributionRegistry, "cortex.editor.findAllReferences", MenuProjectionLocation.ContextMenu, "02_navigation", 40, EditorContextIds.Symbol);
            RegisterMenu(contributionRegistry, "cortex.editor.viewCallHierarchy", MenuProjectionLocation.ContextMenu, "02_navigation", 50, EditorContextIds.Symbol);
            RegisterMenu(contributionRegistry, "cortex.editor.copySymbol", MenuProjectionLocation.ContextMenu, "03_clipboard", 0, EditorContextIds.Symbol);
            RegisterMenu(contributionRegistry, "cortex.editor.copyHoverInfo", MenuProjectionLocation.ContextMenu, "03_clipboard", 10, EditorContextIds.Symbol);

            RegisterTheme(
                contributionRegistry,
                "cortex.vs-dark",
                "Visual Studio Dark",
                "A clean, dark theme matching Visual Studio 2022.",
                "#1E1E1E",
                "#252526",
                "#2D2D30",
                "#3F3F46",
                "#007ACC",
                "#D4D4D4",
                "#858585",
                "#C8A155",
                "#F48771",
                "compact-mono",
                0);
            RegisterTheme(
                contributionRegistry,
                "cortex.default",
                "Cortex Default",
                "Alias of the Visual Studio Dark workbench theme.",
                "#1E1E1E",
                "#252526",
                "#2D2D30",
                "#3F3F46",
                "#007ACC",
                "#D4D4D4",
                "#858585",
                "#C8A155",
                "#F48771",
                "compact-mono",
                1);
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

            RegisterIcon(contributionRegistry, CortexWorkbenchIds.FileExplorerContainer, "EX");
            RegisterIcon(contributionRegistry, CortexWorkbenchIds.LogsContainer, "LG");
            RegisterIcon(contributionRegistry, CortexWorkbenchIds.ProjectsContainer, "PJ");
            RegisterIcon(contributionRegistry, CortexWorkbenchIds.ReferenceContainer, "RF");
            RegisterIcon(contributionRegistry, CortexWorkbenchIds.SearchContainer, "SR");
            RegisterIcon(contributionRegistry, CortexWorkbenchIds.EditorContainer, "ED");
            RegisterIcon(contributionRegistry, CortexWorkbenchIds.BuildContainer, "BL");
            RegisterIcon(contributionRegistry, CortexWorkbenchIds.RuntimeContainer, "RT");

            RegisterEditor(contributionRegistry, "cortex.editor.code", "Code Editor", ".cs", "text/x-csharp", 0);
            RegisterEditor(contributionRegistry, "cortex.editor.text", "Text Editor", ".txt", "text/plain", 10);
            RegisterEditor(contributionRegistry, "cortex.editor.log", "Log Viewer", ".log", "text/plain", 20);

            RegisterSetting(contributionRegistry, nameof(CortexSettings.WorkspaceRootPath), "Workspace Scan Root", "Tell Cortex where to scan for editable workspace sources.", "Workspace", string.Empty, SettingValueKind.String, 0);
            RegisterSetting(contributionRegistry, nameof(CortexSettings.ModsRootPath), "Loaded Mods Root", "Points to the live mod folder used for source mapping and discovery.", "Workspace", string.Empty, SettingValueKind.String, 10);
            RegisterSetting(contributionRegistry, nameof(CortexSettings.ManagedAssemblyRootPath), "Game Managed DLLs", "Used to locate assemblies for reference browsing and decompilation.", "Workspace", string.Empty, SettingValueKind.String, 20);
            RegisterSetting(contributionRegistry, nameof(CortexSettings.AdditionalSourceRoots), "Extra Source Roots", "Semicolon-separated fallback roots for source resolution.", "Workspace", string.Empty, SettingValueKind.String, 30);
            RegisterSetting(contributionRegistry, nameof(CortexSettings.ProjectCatalogPath), "Project Catalog", "Path to the persisted Cortex project catalog file.", "Workspace", string.Empty, SettingValueKind.String, 40);
            RegisterSetting(contributionRegistry, nameof(CortexSettings.CortexPluginSearchRoots), "Cortex Plugin Roots", "Semicolon-separated roots scanned for external Cortex workbench plugins.", "Workspace", string.Empty, SettingValueKind.String, 50);

            RegisterSetting(contributionRegistry, nameof(CortexSettings.LogFilePath), "Live Log File", "Optional file that is tailed under the live in-memory log feed.", "Logs", string.Empty, SettingValueKind.String, 0);
            RegisterSetting(contributionRegistry, nameof(CortexSettings.MaxRecentLogs), "Max Recent Logs", "Maximum number of live log entries to keep in the in-memory feed.", "Logs", "300", SettingValueKind.Integer, 10);
            RegisterSetting(contributionRegistry, nameof(CortexSettings.AutoScrollLogs), "Auto-scroll Log List", "Keep the live log list pinned to the newest entry.", "Logs", "true", SettingValueKind.Boolean, 20);
            RegisterSetting(contributionRegistry, nameof(CortexSettings.ShowLogBacklog), "Show File Tail History", "Optional raw file tail for lines that were written before Cortex attached or were not captured in the live feed.", "Logs", "false", SettingValueKind.Boolean, 30);

            RegisterSetting(contributionRegistry, nameof(CortexSettings.DecompilerPathOverride), "Decompiler Override", "Optional path to a custom decompiler executable.", "Decompiler", string.Empty, SettingValueKind.String, 0);
            RegisterSetting(contributionRegistry, nameof(CortexSettings.DecompilerCachePath), "Decompiler Cache", "Location used to cache generated source from runtime and reference browsing.", "Decompiler", string.Empty, SettingValueKind.String, 10);
            RegisterSetting(contributionRegistry, nameof(CortexSettings.EnableRoslynLanguageService), "Enable Roslyn Worker", "Start the external Roslyn language service for diagnostics, hover info, and semantic classification.", "Language Service", "true", SettingValueKind.Boolean, 0);
            RegisterSetting(contributionRegistry, nameof(CortexSettings.RoslynServicePathOverride), "Roslyn Worker Path", "Optional override for the external Cortex.Roslyn.Worker executable or DLL.", "Language Service", string.Empty, SettingValueKind.String, 10);
            RegisterSetting(contributionRegistry, nameof(CortexSettings.RoslynServiceTimeoutMs), "Roslyn Timeout (ms)", "Maximum request time for the external Roslyn language service.", "Language Service", "15000", SettingValueKind.Integer, 20);

            RegisterSetting(contributionRegistry, nameof(CortexSettings.DefaultBuildConfiguration), "Default Build Config", "Default build configuration used by build tooling.", "Build", "Debug", SettingValueKind.String, 0);
            RegisterSetting(contributionRegistry, nameof(CortexSettings.BuildTimeoutMs), "Build Timeout (ms)", "Maximum time allowed for build execution before timing out.", "Build", "300000", SettingValueKind.Integer, 10);

            RegisterSetting(contributionRegistry, nameof(CortexSettings.ThemeId), "Theme", "Active workbench theme identifier.", "Appearance", "cortex.vs-dark", SettingValueKind.String, 0);
            RegisterSetting(contributionRegistry, nameof(CortexSettings.EnableFileEditing), "Enable File Editing", "Allow source files to be unlocked for direct editing inside Cortex.", "Editing", "false", SettingValueKind.Boolean, 0);
            RegisterSetting(contributionRegistry, nameof(CortexSettings.EnableFileSaving), "Enable File Saving", "Allow Save and Save All to write snapshot-based changes back into source files.", "Editing", "false", SettingValueKind.Boolean, 10);
            RegisterSetting(contributionRegistry, nameof(CortexSettings.LogsPaneWidth), "Logs Pane Width", "Preferred width for the logs/details split.", "Layout", "520", SettingValueKind.Float, 0);
            RegisterSetting(contributionRegistry, nameof(CortexSettings.ProjectsPaneWidth), "Projects Pane Width", "Preferred width for the side host.", "Layout", "360", SettingValueKind.Float, 10);
            RegisterSetting(contributionRegistry, nameof(CortexSettings.EditorFilePaneWidth), "Secondary Tool Width", "Preferred width for the right-side tool window host.", "Layout", "420", SettingValueKind.Float, 20);
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
            int sortOrder,
            string contextId)
        {
            contributionRegistry.RegisterMenu(new MenuContribution
            {
                CommandId = commandId,
                Location = location,
                ContextId = contextId,
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
