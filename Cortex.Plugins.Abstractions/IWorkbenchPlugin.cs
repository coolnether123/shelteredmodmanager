using Cortex.Core.Abstractions;

namespace Cortex.Plugins.Abstractions
{
    /// <summary>
    /// Legacy workbench plugin entry point that registers commands and declarative contributions
    /// against the raw runtime registries.
    /// </summary>
    public interface IWorkbenchPlugin
    {
        /// <summary>
        /// Gets the stable plugin identifier.
        /// </summary>
        string PluginId { get; }

        /// <summary>
        /// Gets the human-readable plugin display name.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Registers commands and declarative workbench contributions.
        /// </summary>
        /// <param name="commandRegistry">The command registry for command definitions and handlers.</param>
        /// <param name="contributionRegistry">The declarative contribution registry for views, settings, and menus.</param>
        void Register(ICommandRegistry commandRegistry, IContributionRegistry contributionRegistry);
    }

    /// <summary>
    /// Preferred workbench plugin entry point that registers through a higher-level authoring context.
    /// </summary>
    public interface IWorkbenchPluginContributor
    {
        /// <summary>
        /// Gets the stable plugin identifier.
        /// </summary>
        string PluginId { get; }

        /// <summary>
        /// Gets the human-readable plugin display name.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Registers commands, contributions, and optional workbench modules.
        /// </summary>
        /// <param name="context">The workbench authoring context exposed by Cortex.</param>
        void Register(WorkbenchPluginContext context);
    }
}
