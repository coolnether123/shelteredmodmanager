using System;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;

namespace Cortex.Plugins.Abstractions
{
    /// <summary>
    /// High-level authoring context used to register commands, declarative contributions, and optional modules.
    /// </summary>
    public sealed class WorkbenchPluginContext
    {
        /// <summary>
        /// Initializes a new workbench plugin context.
        /// </summary>
        /// <param name="commandRegistry">The command registry to populate.</param>
        /// <param name="contributionRegistry">The declarative contribution registry to populate.</param>
        /// <param name="moduleRegistry">The optional module registry for renderable workbench modules.</param>
        public WorkbenchPluginContext(
            ICommandRegistry commandRegistry,
            IContributionRegistry contributionRegistry,
            IWorkbenchModuleRegistry moduleRegistry,
            IWorkbenchExtensionRegistry extensionRegistry,
            IWorkbenchRuntimeAccess runtime)
        {
            CommandRegistry = commandRegistry;
            ContributionRegistry = contributionRegistry;
            ModuleRegistry = moduleRegistry;
            ExtensionRegistry = extensionRegistry;
            Runtime = runtime;
        }

        /// <summary>
        /// Gets the underlying command registry.
        /// </summary>
        public ICommandRegistry CommandRegistry { get; private set; }

        /// <summary>
        /// Gets the underlying declarative contribution registry.
        /// </summary>
        public IContributionRegistry ContributionRegistry { get; private set; }

        /// <summary>
        /// Gets the optional module registry.
        /// </summary>
        public IWorkbenchModuleRegistry ModuleRegistry { get; private set; }

        /// <summary>
        /// Gets the runtime-driven extension registry.
        /// </summary>
        public IWorkbenchExtensionRegistry ExtensionRegistry { get; private set; }

        /// <summary>
        /// Gets narrow runtime access for contributed commands and extensions.
        /// </summary>
        public IWorkbenchRuntimeAccess Runtime { get; private set; }

        /// <summary>
        /// Registers a command definition.
        /// </summary>
        /// <param name="definition">The command definition to register.</param>
        public void RegisterCommand(CommandDefinition definition)
        {
            if (CommandRegistry == null || definition == null)
            {
                return;
            }

            CommandRegistry.Register(definition);
        }

        /// <summary>
        /// Registers a command using common workbench metadata.
        /// </summary>
        /// <param name="commandId">The stable command identifier.</param>
        /// <param name="displayName">The display label shown to users.</param>
        /// <param name="category">The command category.</param>
        /// <param name="description">The command description.</param>
        /// <param name="gesture">The default key gesture.</param>
        /// <param name="sortOrder">The display sort order.</param>
        /// <param name="showInPalette">Whether the command should appear in the command palette.</param>
        /// <param name="isGlobal">Whether the command is globally available.</param>
        public void RegisterCommand(
            string commandId,
            string displayName,
            string category,
            string description,
            string gesture,
            int sortOrder,
            bool showInPalette,
            bool isGlobal)
        {
            RegisterCommand(new CommandDefinition
            {
                CommandId = commandId,
                DisplayName = displayName,
                Category = category,
                Description = description,
                DefaultGesture = gesture,
                SortOrder = sortOrder,
                ShowInPalette = showInPalette,
                IsGlobal = isGlobal
            });
        }

        /// <summary>
        /// Registers a command handler and optional enablement callback.
        /// </summary>
        /// <param name="commandId">The command identifier to bind.</param>
        /// <param name="handler">The handler invoked when the command executes.</param>
        /// <param name="canExecute">Optional enablement callback.</param>
        public void RegisterCommandHandler(string commandId, CommandHandler handler, CommandEnablement canExecute)
        {
            if (CommandRegistry == null || string.IsNullOrEmpty(commandId) || handler == null)
            {
                return;
            }

            CommandRegistry.RegisterHandler(commandId, handler, canExecute);
        }

        /// <summary>
        /// Registers a view container contribution.
        /// </summary>
        /// <param name="contribution">The view container contribution.</param>
        public void RegisterViewContainer(ViewContainerContribution contribution)
        {
            if (ContributionRegistry == null || contribution == null)
            {
                return;
            }

            ContributionRegistry.RegisterViewContainer(contribution);
        }

        /// <summary>
        /// Registers a view container using common workbench metadata.
        /// </summary>
        /// <param name="containerId">The stable container identifier.</param>
        /// <param name="title">The container title shown in the workbench.</param>
        /// <param name="defaultHostLocation">The default host location.</param>
        /// <param name="sortOrder">The display sort order.</param>
        /// <param name="pinnedByDefault">Whether the container is pinned by default.</param>
        /// <param name="activationKind">The activation policy for the backing module.</param>
        /// <param name="activationTarget">The activation target associated with the policy.</param>
        /// <param name="iconId">The icon identifier to project for the container.</param>
        public void RegisterViewContainer(
            string containerId,
            string title,
            WorkbenchHostLocation defaultHostLocation,
            int sortOrder,
            bool pinnedByDefault,
            ModuleActivationKind activationKind,
            string activationTarget,
            string iconId)
        {
            RegisterViewContainer(new ViewContainerContribution
            {
                ContainerId = containerId,
                Title = title,
                IconId = iconId,
                DefaultHostLocation = defaultHostLocation,
                SortOrder = sortOrder,
                PinnedByDefault = pinnedByDefault,
                ActivationKind = activationKind,
                ActivationTarget = activationTarget
            });
        }

        /// <summary>
        /// Registers a view contribution.
        /// </summary>
        /// <param name="contribution">The view contribution.</param>
        public void RegisterView(ViewContribution contribution)
        {
            if (ContributionRegistry == null || contribution == null)
            {
                return;
            }

            ContributionRegistry.RegisterView(contribution);
        }

        /// <summary>
        /// Registers a default view for a container.
        /// </summary>
        /// <param name="viewId">The stable view identifier.</param>
        /// <param name="containerId">The owning container identifier.</param>
        /// <param name="title">The view title.</param>
        /// <param name="persistenceId">The persisted identity for the view.</param>
        /// <param name="sortOrder">The display sort order.</param>
        /// <param name="visibleByDefault">Whether the view is visible by default.</param>
        public void RegisterView(
            string viewId,
            string containerId,
            string title,
            string persistenceId,
            int sortOrder,
            bool visibleByDefault)
        {
            RegisterView(new ViewContribution
            {
                ViewId = viewId,
                ContainerId = containerId,
                Title = title,
                PersistenceId = persistenceId,
                SortOrder = sortOrder,
                VisibleByDefault = visibleByDefault
            });
        }

        /// <summary>
        /// Registers an editor contribution.
        /// </summary>
        /// <param name="contribution">The editor contribution.</param>
        public void RegisterEditor(EditorContribution contribution)
        {
            if (ContributionRegistry == null || contribution == null)
            {
                return;
            }

            ContributionRegistry.RegisterEditor(contribution);
        }

        /// <summary>
        /// Registers an editor contribution using common editor metadata.
        /// </summary>
        /// <param name="editorId">The editor identifier.</param>
        /// <param name="displayName">The editor display name.</param>
        /// <param name="resourceExtension">The associated resource extension.</param>
        /// <param name="contentType">The associated content type.</param>
        /// <param name="sortOrder">The display sort order.</param>
        public void RegisterEditor(
            string editorId,
            string displayName,
            string resourceExtension,
            string contentType,
            int sortOrder)
        {
            RegisterEditor(new EditorContribution
            {
                EditorId = editorId,
                DisplayName = displayName,
                ResourceExtension = resourceExtension,
                ContentType = contentType,
                SortOrder = sortOrder
            });
        }

        /// <summary>
        /// Registers a menu contribution.
        /// </summary>
        /// <param name="contribution">The menu contribution.</param>
        public void RegisterMenu(MenuContribution contribution)
        {
            if (ContributionRegistry == null || contribution == null)
            {
                return;
            }

            ContributionRegistry.RegisterMenu(contribution);
        }

        /// <summary>
        /// Registers a menu projection using common menu metadata.
        /// </summary>
        /// <param name="commandId">The command projected into the menu.</param>
        /// <param name="location">The menu location.</param>
        /// <param name="group">The menu group identifier.</param>
        /// <param name="sortOrder">The display sort order.</param>
        /// <param name="contextId">The optional visibility context identifier.</param>
        public void RegisterMenu(
            string commandId,
            MenuProjectionLocation location,
            string group,
            int sortOrder,
            string contextId)
        {
            RegisterMenu(new MenuContribution
            {
                CommandId = commandId,
                Location = location,
                ContextId = contextId,
                Group = group,
                SortOrder = sortOrder
            });
        }

        /// <summary>
        /// Registers a status item contribution.
        /// </summary>
        /// <param name="contribution">The status item contribution.</param>
        public void RegisterStatusItem(StatusItemContribution contribution)
        {
            if (ContributionRegistry == null || contribution == null)
            {
                return;
            }

            ContributionRegistry.RegisterStatusItem(contribution);
        }

        /// <summary>
        /// Registers a theme contribution.
        /// </summary>
        /// <param name="contribution">The theme contribution.</param>
        public void RegisterTheme(ThemeContribution contribution)
        {
            if (ContributionRegistry == null || contribution == null)
            {
                return;
            }

            ContributionRegistry.RegisterTheme(contribution);
        }

        /// <summary>
        /// Registers an onboarding profile contribution.
        /// </summary>
        /// <param name="contribution">The onboarding profile contribution.</param>
        public void RegisterOnboardingProfile(OnboardingProfileContribution contribution)
        {
            if (ContributionRegistry == null || contribution == null)
            {
                return;
            }

            ContributionRegistry.RegisterOnboardingProfile(contribution);
        }

        /// <summary>
        /// Registers an onboarding layout preset contribution.
        /// </summary>
        /// <param name="contribution">The onboarding layout preset contribution.</param>
        public void RegisterOnboardingLayoutPreset(OnboardingLayoutPresetContribution contribution)
        {
            if (ContributionRegistry == null || contribution == null)
            {
                return;
            }

            ContributionRegistry.RegisterOnboardingLayoutPreset(contribution);
        }

        /// <summary>
        /// Registers an icon contribution.
        /// </summary>
        /// <param name="contribution">The icon contribution.</param>
        public void RegisterIcon(IconContribution contribution)
        {
            if (ContributionRegistry == null || contribution == null)
            {
                return;
            }

            ContributionRegistry.RegisterIcon(contribution);
        }

        /// <summary>
        /// Registers section metadata for a settings scope.
        /// </summary>
        /// <param name="contribution">The settings section contribution.</param>
        public void RegisterSettingSection(SettingSectionContribution contribution)
        {
            if (ContributionRegistry == null || contribution == null)
            {
                return;
            }

            ContributionRegistry.RegisterSettingSection(contribution);
        }

        /// <summary>
        /// Registers section metadata for a settings scope using common fields.
        /// </summary>
        public void RegisterSettingSection(
            string scope,
            string groupId,
            string groupTitle,
            string sectionId,
            string sectionTitle,
            string description,
            string[] keywords,
            int sortOrder)
        {
            RegisterSettingSection(new SettingSectionContribution
            {
                Scope = scope,
                GroupId = groupId,
                GroupTitle = groupTitle,
                SectionId = sectionId,
                SectionTitle = sectionTitle,
                Description = description,
                Keywords = keywords ?? new string[0],
                SortOrder = sortOrder
            });
        }

        /// <summary>
        /// Registers an editor-context action contribution shared by the
        /// right-click menu, the action bar, and the quick-actions picker.
        /// </summary>
        public void RegisterEditorContextAction(EditorContextActionContribution contribution)
        {
            if (ContributionRegistry == null || contribution == null)
            {
                return;
            }

            ContributionRegistry.RegisterEditorContextAction(contribution);
        }

        /// <summary>
        /// Registers an editor-context action using common editor action metadata.
        /// </summary>
        public void RegisterEditorContextAction(
            string actionId,
            string commandId,
            string contextId,
            string group,
            int sortOrder,
            EditorContextActionPlacement placements,
            string requiredCapability,
            bool includeWhenNoSymbol,
            bool showWhenDisabled,
            string title,
            string description)
        {
            RegisterEditorContextAction(new EditorContextActionContribution
            {
                ActionId = actionId,
                CommandId = commandId,
                ContextId = contextId,
                Group = group,
                SortOrder = sortOrder,
                Placements = placements,
                RequiredCapability = requiredCapability,
                IncludeWhenNoSymbol = includeWhenNoSymbol,
                ShowWhenDisabled = showWhenDisabled,
                Title = title,
                Description = description
            });
        }

        /// <summary>
        /// Registers a settings contribution.
        /// </summary>
        /// <param name="contribution">The setting contribution.</param>
        public void RegisterSetting(SettingContribution contribution)
        {
            if (ContributionRegistry == null || contribution == null)
            {
                return;
            }

            ContributionRegistry.RegisterSetting(contribution);
        }

        /// <summary>
        /// Registers a setting using common metadata.
        /// </summary>
        /// <param name="settingId">The setting identifier.</param>
        /// <param name="displayName">The user-facing setting name.</param>
        /// <param name="description">The setting description.</param>
        /// <param name="scope">The setting scope label.</param>
        /// <param name="defaultValue">The serialized default value.</param>
        /// <param name="valueKind">The setting value type.</param>
        /// <param name="sortOrder">The display sort order.</param>
        public void RegisterSetting(
            string settingId,
            string displayName,
            string description,
            string scope,
            string defaultValue,
            SettingValueKind valueKind,
            int sortOrder)
        {
            RegisterSetting(new SettingContribution
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

        /// <summary>
        /// Registers a setting using extended metadata so Cortex can build groups, search, and editors automatically.
        /// </summary>
        public void RegisterSetting(
            string settingId,
            string displayName,
            string description,
            string scope,
            string defaultValue,
            SettingValueKind valueKind,
            int sortOrder,
            SettingEditorKind editorKind,
            string placeholderText,
            string helpText,
            string[] keywords,
            SettingChoiceOption[] options,
            bool isSecret,
            bool isRequired = false,
            Func<CortexSettings, string> readSettingsValue = null,
            Action<CortexSettings, string> writeSettingsValue = null,
            Func<string> readValue = null,
            Action<string> writeValue = null,
            Func<string> readDefaultValue = null,
            Func<string, SettingValidationResult> validateValue = null,
            SettingActionContribution[] actions = null,
            bool requiresRestart = false)
        {
            RegisterSetting(new SettingContribution
            {
                SettingId = settingId,
                DisplayName = displayName,
                Description = description,
                Scope = scope,
                DefaultValue = defaultValue,
                ValueKind = valueKind,
                EditorKind = editorKind,
                PlaceholderText = placeholderText,
                HelpText = helpText,
                Keywords = keywords ?? new string[0],
                Options = options ?? new SettingChoiceOption[0],
                IsSecret = isSecret,
                IsRequired = isRequired,
                ReadSettingsValue = readSettingsValue,
                WriteSettingsValue = writeSettingsValue,
                ReadValue = readValue,
                WriteValue = writeValue,
                ReadDefaultValue = readDefaultValue,
                ValidateValue = validateValue,
                Actions = actions ?? new SettingActionContribution[0],
                RequiresRestart = requiresRestart,
                SortOrder = sortOrder
            });
        }

        /// <summary>
        /// Registers a renderable workbench module contribution.
        /// </summary>
        /// <param name="contribution">The module contribution.</param>
        public void RegisterModule(IWorkbenchModuleContribution contribution)
        {
            if (ModuleRegistry == null || contribution == null)
            {
                return;
            }

            ModuleRegistry.Register(contribution);
        }

        /// <summary>
        /// Registers a contributed method-inspector section.
        /// </summary>
        public void RegisterMethodInspectorSection(WorkbenchMethodInspectorSectionContribution contribution)
        {
            if (ExtensionRegistry == null || contribution == null)
            {
                return;
            }

            ExtensionRegistry.RegisterMethodInspectorSection(contribution);
        }

        /// <summary>
        /// Registers contributed method-relationship augmentations for the inspector.
        /// </summary>
        public void RegisterMethodRelationshipAugmentation(WorkbenchMethodRelationshipAugmentationContribution contribution)
        {
            if (ExtensionRegistry == null || contribution == null)
            {
                return;
            }

            ExtensionRegistry.RegisterMethodRelationshipAugmentation(contribution);
        }

        /// <summary>
        /// Registers contributed method-relationship actions for the inspector.
        /// </summary>
        public void RegisterMethodRelationshipAction(WorkbenchMethodRelationshipActionContribution contribution)
        {
            if (ExtensionRegistry == null || contribution == null)
            {
                return;
            }

            ExtensionRegistry.RegisterMethodRelationshipAction(contribution);
        }

        /// <summary>
        /// Registers a host-rendered editor adornment contribution.
        /// </summary>
        public void RegisterEditorAdornment(WorkbenchEditorAdornmentContribution contribution)
        {
            if (ExtensionRegistry == null || contribution == null)
            {
                return;
            }

            ExtensionRegistry.RegisterEditorAdornment(contribution);
        }

        /// <summary>
        /// Registers a contributed editor workflow.
        /// </summary>
        public void RegisterEditorWorkflow(WorkbenchEditorWorkflowContribution contribution)
        {
            if (ExtensionRegistry == null || contribution == null)
            {
                return;
            }

            ExtensionRegistry.RegisterEditorWorkflow(contribution);
        }
    }
}
