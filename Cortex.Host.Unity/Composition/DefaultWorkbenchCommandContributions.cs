using Cortex.Core.Models;
using Cortex.Plugins.Abstractions;

namespace Cortex.Host.Unity.Composition
{
    internal sealed class DefaultWorkbenchCommandContributions
    {
        public void Register(WorkbenchPluginContext context)
        {
            if (context == null)
            {
                return;
            }

            context.RegisterCommand("cortex.shell.toggle", "Toggle Cortex", "Workbench", "Show or hide the Cortex shell.", "F8", 0, true, true);
            context.RegisterCommand("cortex.shell.fitWindow", "Fit Workbench To Screen", "View", "Resize the shell to fill most of the game view.", string.Empty, 10, true, false);
            context.RegisterCommand("cortex.logs.toggleWindow", "Detached Logs Window", "View", "Show or hide the detached log window.", string.Empty, 20, true, false);
            context.RegisterCommand("cortex.build.execute", "Build Project", "Build", "Focus the build panel and trigger a build.", string.Empty, 0, true, false);
            context.RegisterCommand("cortex.file.saveAll", "Save All", "File", "Save all open documents.", string.Empty, 0, true, false);
            context.RegisterCommand("cortex.file.closeActive", "Close", "File", "Close the active document.", string.Empty, 10, true, false);
            context.RegisterCommand("cortex.file.settings", "Settings", "File", "Open Cortex settings in the editor surface.", string.Empty, 20, true, false);
            context.RegisterCommand("cortex.view.fileExplorer", "Toggle File Explorer", "View", "Show or hide the file explorer pane.", string.Empty, 0, true, false);
            context.RegisterCommand("cortex.view.zoomIn", "Increase Font Size", "View", "Increase the editor font size.", string.Empty, 10, true, false);
            context.RegisterCommand("cortex.view.zoomOut", "Decrease Font Size", "View", "Decrease the editor font size.", string.Empty, 20, true, false);
            context.RegisterCommand("cortex.win.theme", "Switch Theme", "View", "Cycle through registered themes.", string.Empty, 30, true, false);
            context.RegisterCommand("cortex.window.explorer", "Explorer", "Window", "Show the explorer tool window.", string.Empty, 0, true, false);
            context.RegisterCommand("cortex.window.projects", "Projects", "Window", "Show the projects tool window.", string.Empty, 10, true, false);
            context.RegisterCommand("cortex.window.references", "References", "Window", "Show the references tool window.", string.Empty, 20, true, false);
            context.RegisterCommand("cortex.window.search", "Search", "Window", "Show the search results tool window.", string.Empty, 30, true, false);
            context.RegisterCommand("cortex.window.logs", "Logs", "Window", "Show the logs tool window.", string.Empty, 40, true, false);
            context.RegisterCommand("cortex.window.build", "Build", "Window", "Show the build tool window.", string.Empty, 50, true, false);
            context.RegisterCommand("cortex.window.runtime", "Runtime", "Window", "Show the runtime tool window.", string.Empty, 60, true, false);
            context.RegisterCommand("cortex.window.settings", "Settings", "Window", "Show Cortex settings in the editor surface.", string.Empty, 70, true, false);
            context.RegisterCommand("cortex.editor.find", "Find...", "Editor", "Open the find bar for text search.", "Ctrl+F", 0, true, false);
            context.RegisterCommand("cortex.search.next", "Find Next", "Editor", "Advance to the next search result.", "F3", 110, true, false);
            context.RegisterCommand("cortex.search.previous", "Find Previous", "Editor", "Move to the previous search result.", "Shift+F3", 120, true, false);
            context.RegisterCommand("cortex.search.close", "Close Find", "Editor", "Close the active find bar.", "Escape", 130, true, false);
            context.RegisterCommand("cortex.editor.quickActions", "Quick Actions and Refactorings...", "Editor", "Show editor quick actions for the current symbol.", "Ctrl+.", 0, true, false);
            context.RegisterCommand("cortex.editor.rename", "Rename...", "Editor", "Rename the current symbol.", "F2", 10, true, false);
            context.RegisterCommand("cortex.editor.removeAndSortUsings", "Remove and Sort Usings", "Editor", "Clean and reorder using directives.", "Ctrl+R, Ctrl+G", 20, true, false);
            context.RegisterCommand("cortex.editor.peekDefinition", "Peek Definition", "Editor", "Open an inline peek view for the current symbol definition.", "Alt+F12", 30, true, false);
            context.RegisterCommand("cortex.editor.goToDefinition", "Go To Definition", "Editor", "Navigate to the current symbol definition.", "F12", 40, true, false);
            context.RegisterCommand("cortex.editor.goToBase", "Go To Base", "Editor", "Navigate to the base symbol.", "Alt+Home", 50, true, false);
            context.RegisterCommand("cortex.editor.goToImplementation", "Go To Implementation", "Editor", "Navigate to the current symbol implementation.", "Ctrl+F12", 60, true, false);
            context.RegisterCommand("cortex.editor.findAllReferences", "Find All References", "Editor", "Find references for the current symbol.", "Ctrl+K, R", 70, true, false);
            context.RegisterCommand("cortex.editor.viewCallHierarchy", "View Call Hierarchy", "Editor", "Show call hierarchy for the current symbol.", "Ctrl+K, Ctrl+T", 80, true, false);
            context.RegisterCommand("cortex.editor.copySymbol", "Copy Symbol", "Editor", "Copy the selected symbol text.", "Ctrl+C", 90, true, false);
            context.RegisterCommand("cortex.editor.copyHoverInfo", "Copy Hover Info", "Editor", "Copy the current hover/signature information.", string.Empty, 100, true, false);

            context.RegisterMenu("cortex.file.saveAll", MenuProjectionLocation.MainMenu, "File", 0, string.Empty);
            context.RegisterMenu("cortex.file.closeActive", MenuProjectionLocation.MainMenu, "File", 10, string.Empty);
            context.RegisterMenu("cortex.file.settings", MenuProjectionLocation.MainMenu, "File", 20, string.Empty);
            context.RegisterMenu("cortex.view.fileExplorer", MenuProjectionLocation.MainMenu, "View", 0, string.Empty);
            context.RegisterMenu("cortex.view.zoomIn", MenuProjectionLocation.MainMenu, "View", 10, string.Empty);
            context.RegisterMenu("cortex.view.zoomOut", MenuProjectionLocation.MainMenu, "View", 20, string.Empty);
            context.RegisterMenu("cortex.win.theme", MenuProjectionLocation.MainMenu, "View", 30, string.Empty);
            context.RegisterMenu("cortex.logs.toggleWindow", MenuProjectionLocation.MainMenu, "View", 40, string.Empty);
            context.RegisterMenu("cortex.window.explorer", MenuProjectionLocation.MainMenu, "Window", 0, string.Empty);
            context.RegisterMenu("cortex.window.projects", MenuProjectionLocation.MainMenu, "Window", 10, string.Empty);
            context.RegisterMenu("cortex.window.references", MenuProjectionLocation.MainMenu, "Window", 20, string.Empty);
            context.RegisterMenu("cortex.window.search", MenuProjectionLocation.MainMenu, "Window", 30, string.Empty);
            context.RegisterMenu("cortex.window.logs", MenuProjectionLocation.MainMenu, "Window", 40, string.Empty);
            context.RegisterMenu("cortex.window.build", MenuProjectionLocation.MainMenu, "Window", 50, string.Empty);
            context.RegisterMenu("cortex.window.runtime", MenuProjectionLocation.MainMenu, "Window", 60, string.Empty);
            context.RegisterMenu("cortex.window.settings", MenuProjectionLocation.MainMenu, "Window", 70, string.Empty);
            context.RegisterMenu("cortex.shell.fitWindow", MenuProjectionLocation.MainMenu, "Window", 110, string.Empty);
            context.RegisterMenu("cortex.editor.find", MenuProjectionLocation.MainMenu, "Edit", 0, string.Empty);
            context.RegisterMenu("cortex.build.execute", MenuProjectionLocation.MainMenu, "Build", 0, string.Empty);
            context.RegisterMenu("cortex.editor.quickActions", MenuProjectionLocation.ContextMenu, "01_actions", 0, EditorContextIds.Symbol);
            context.RegisterMenu("cortex.editor.rename", MenuProjectionLocation.ContextMenu, "01_actions", 10, EditorContextIds.Symbol);
            context.RegisterMenu("cortex.editor.removeAndSortUsings", MenuProjectionLocation.ContextMenu, "01_actions", 20, EditorContextIds.Symbol);
            context.RegisterMenu("cortex.editor.peekDefinition", MenuProjectionLocation.ContextMenu, "02_navigation", 0, EditorContextIds.Symbol);
            context.RegisterMenu("cortex.editor.goToDefinition", MenuProjectionLocation.ContextMenu, "02_navigation", 10, EditorContextIds.Symbol);
            context.RegisterMenu("cortex.editor.goToBase", MenuProjectionLocation.ContextMenu, "02_navigation", 20, EditorContextIds.Symbol);
            context.RegisterMenu("cortex.editor.goToImplementation", MenuProjectionLocation.ContextMenu, "02_navigation", 30, EditorContextIds.Symbol);
            context.RegisterMenu("cortex.editor.findAllReferences", MenuProjectionLocation.ContextMenu, "02_navigation", 40, EditorContextIds.Symbol);
            context.RegisterMenu("cortex.editor.viewCallHierarchy", MenuProjectionLocation.ContextMenu, "02_navigation", 50, EditorContextIds.Symbol);
            context.RegisterMenu("cortex.editor.copySymbol", MenuProjectionLocation.ContextMenu, "03_clipboard", 0, EditorContextIds.Symbol);
            context.RegisterMenu("cortex.editor.copyHoverInfo", MenuProjectionLocation.ContextMenu, "03_clipboard", 10, EditorContextIds.Symbol);
        }
    }
}
