namespace Cortex.Plugins.Abstractions
{
    /// <summary>
    /// Workbench plugin entry point that registers through a higher-level authoring context.
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
