using System;
using Cortex.Presentation.Models;

namespace Cortex.Plugins.Abstractions
{
    /// <summary>
    /// Registers renderable workbench modules for specific view containers.
    /// </summary>
    public interface IWorkbenchModuleRegistry
    {
        /// <summary>
        /// Registers a module contribution for a workbench container.
        /// </summary>
        /// <param name="contribution">The module contribution to register.</param>
        void Register(IWorkbenchModuleContribution contribution);
    }

    /// <summary>
    /// Describes a renderable workbench module contribution.
    /// </summary>
    public interface IWorkbenchModuleContribution
    {
        /// <summary>
        /// Gets the descriptor that identifies the target container and module type.
        /// </summary>
        WorkbenchModuleDescriptor Descriptor { get; }

        /// <summary>
        /// Creates the runtime module instance for this contribution.
        /// </summary>
        /// <param name="runtime">The module-owned runtime capabilities exposed by Cortex.</param>
        /// <returns>A renderable workbench module instance.</returns>
        IWorkbenchModule CreateModule(IWorkbenchModuleRuntime runtime);
    }

    /// <summary>
    /// Represents a renderable workbench module instance.
    /// </summary>
    public interface IWorkbenchModule
    {
        /// <summary>
        /// Returns a message explaining why the module is not available, or an empty string when it can render.
        /// </summary>
        /// <returns>An unavailable message, or an empty string.</returns>
        string GetUnavailableMessage();

        /// <summary>
        /// Renders the module inside its workbench host.
        /// </summary>
        /// <param name="context">The render-time workbench context.</param>
        /// <param name="detachedWindow">Whether the module is rendering inside a detached host window.</param>
        void Render(WorkbenchModuleRenderContext context, bool detachedWindow);
    }

    /// <summary>
    /// Data-only descriptor for a workbench module contribution.
    /// </summary>
    public sealed class WorkbenchModuleDescriptor
    {
        /// <summary>
        /// Initializes a new module descriptor.
        /// </summary>
        /// <param name="moduleId">The stable runtime module identifier.</param>
        /// <param name="containerId">The container identifier that owns the module.</param>
        /// <param name="moduleType">The concrete runtime module type, when known.</param>
        public WorkbenchModuleDescriptor(string moduleId, string containerId, Type moduleType)
        {
            ModuleId = moduleId ?? string.Empty;
            ContainerId = containerId ?? string.Empty;
            ModuleType = moduleType;
        }

        /// <summary>
        /// Gets the stable runtime module identifier.
        /// </summary>
        public string ModuleId { get; private set; }

        /// <summary>
        /// Gets the target workbench container identifier.
        /// </summary>
        public string ContainerId { get; private set; }

        /// <summary>
        /// Gets the concrete runtime module type.
        /// </summary>
        public Type ModuleType { get; private set; }
    }

    /// <summary>
    /// Render-time context passed to workbench modules.
    /// </summary>
    public sealed class WorkbenchModuleRenderContext
    {
        /// <summary>
        /// Initializes a new render-time context.
        /// </summary>
        /// <param name="containerId">The container currently being rendered.</param>
        /// <param name="snapshot">The current workbench presentation snapshot.</param>
        /// <param name="ui">The host-owned UI surface for shared workbench controls.</param>
        /// <param name="runtime">The module-owned runtime capabilities exposed by Cortex.</param>
        public WorkbenchModuleRenderContext(
            string containerId,
            WorkbenchPresentationSnapshot snapshot,
            IWorkbenchUiSurface ui,
            IWorkbenchModuleRuntime runtime)
        {
            ContainerId = containerId ?? string.Empty;
            Snapshot = snapshot ?? new WorkbenchPresentationSnapshot();
            Ui = ui;
            Runtime = runtime;
        }

        /// <summary>
        /// Gets the container currently being rendered.
        /// </summary>
        public string ContainerId { get; private set; }

        /// <summary>
        /// Gets the current presentation snapshot.
        /// </summary>
        public WorkbenchPresentationSnapshot Snapshot { get; private set; }

        /// <summary>
        /// Gets the host-owned UI surface for shared workbench controls.
        /// </summary>
        public IWorkbenchUiSurface Ui { get; private set; }

        /// <summary>
        /// Gets the module-owned runtime capabilities exposed by Cortex.
        /// </summary>
        public IWorkbenchModuleRuntime Runtime { get; private set; }
    }
}
