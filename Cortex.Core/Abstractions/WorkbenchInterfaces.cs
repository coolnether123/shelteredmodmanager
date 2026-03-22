using System.Collections.Generic;
using Cortex.Core.Models;

namespace Cortex.Core.Abstractions
{
    public interface ICommandRegistry
    {
        void Register(CommandDefinition definition);
        void RegisterHandler(string commandId, CommandHandler handler, CommandEnablement canExecute);
        CommandDefinition Get(string commandId);
        IList<CommandDefinition> GetAll();
        bool CanExecute(string commandId, CommandExecutionContext context);
        bool Execute(string commandId, CommandExecutionContext context);
    }

    public interface IContributionRegistry
    {
        void RegisterViewContainer(ViewContainerContribution contribution);
        void RegisterView(ViewContribution contribution);
        void RegisterEditor(EditorContribution contribution);
        void RegisterMenu(MenuContribution contribution);
        void RegisterEditorContextAction(EditorContextActionContribution contribution);
        void RegisterStatusItem(StatusItemContribution contribution);
        void RegisterTheme(ThemeContribution contribution);
        void RegisterIcon(IconContribution contribution);
        void RegisterOnboardingProfile(OnboardingProfileContribution contribution);
        void RegisterOnboardingLayoutPreset(OnboardingLayoutPresetContribution contribution);
        void RegisterSettingSection(SettingSectionContribution contribution);
        void RegisterSetting(SettingContribution contribution);

        IList<ViewContainerContribution> GetViewContainers();
        IList<ViewContribution> GetViews(string containerId);
        IList<EditorContribution> GetEditors();
        IList<MenuContribution> GetMenus();
        IList<EditorContextActionContribution> GetEditorContextActions();
        IList<StatusItemContribution> GetStatusItems();
        IList<ThemeContribution> GetThemes();
        IList<IconContribution> GetIcons();
        IList<OnboardingProfileContribution> GetOnboardingProfiles();
        IList<OnboardingLayoutPresetContribution> GetOnboardingLayoutPresets();
        IList<SettingSectionContribution> GetSettingSections();
        IList<SettingContribution> GetSettings();
    }

    public interface ILayoutPersistenceService
    {
        LayoutState LoadLayout(string workspaceId);
        void SaveLayout(string workspaceId, LayoutState layoutState);
    }

    public interface IWorkbenchPersistenceService
    {
        PersistedWorkbenchState Load(string workspaceId);
        void Save(string workspaceId, PersistedWorkbenchState state);
    }
}
