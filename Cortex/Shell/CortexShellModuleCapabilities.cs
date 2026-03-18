using System;
using System.Collections.Generic;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Host.Unity.Runtime;
using Cortex.Services;

namespace Cortex.Shell
{
    internal interface ICortexShellStateCapability
    {
        CortexShellState State { get; }
    }

    internal interface ICortexShellNavigationCapability
    {
        CortexNavigationService NavigationService { get; }
    }

    internal interface ICortexShellSourceCapability
    {
        ISourcePathResolver SourcePathResolver { get; }
    }

    internal interface ICortexShellRuntimeLogCapability
    {
        IRuntimeLogFeed RuntimeLogFeed { get; }
    }

    internal interface ICortexShellProjectCapability
    {
        IProjectCatalog ProjectCatalog { get; }
        IProjectWorkspaceService ProjectWorkspaceService { get; }
        ILoadedModCatalog LoadedModCatalog { get; }
    }

    internal interface ICortexShellWorkspaceBrowserCapability
    {
        IWorkspaceBrowserService WorkspaceBrowserService { get; }
        IDecompilerExplorerService DecompilerExplorerService { get; }
    }

    internal interface ICortexShellDocumentCapability
    {
        IDocumentService DocumentService { get; }
    }

    internal interface ICortexShellWorkbenchCapability
    {
        UnityWorkbenchRuntime WorkbenchRuntime { get; }
    }

    internal interface ICortexShellSearchCapability
    {
        WorkbenchSearchService WorkbenchSearchService { get; }
    }

    internal interface ICortexShellBuildCapability
    {
        IBuildCommandResolver BuildCommandResolver { get; }
        IBuildExecutor BuildExecutor { get; }
        IRestartCoordinator RestartCoordinator { get; }
    }

    internal interface ICortexShellReferenceCapability
    {
        IReferenceCatalogService ReferenceCatalogService { get; }
    }

    internal interface ICortexShellRuntimeToolCapability
    {
        IRuntimeToolBridge RuntimeToolBridge { get; }
    }

    internal interface ICortexShellSettingsCapability
    {
        ICortexSettingsStore SettingsStore { get; }
    }

    internal sealed class CortexShellModuleCapabilityCollection
    {
        private readonly Dictionary<Type, object> _capabilities = new Dictionary<Type, object>();

        /// <summary>
        /// Registers a capability implementation under its contract type.
        /// </summary>
        /// <typeparam name="TCapability">The capability contract type.</typeparam>
        /// <param name="capability">The capability implementation to expose.</param>
        public void Add<TCapability>(TCapability capability)
            where TCapability : class
        {
            if (capability == null)
            {
                return;
            }

            _capabilities[typeof(TCapability)] = capability;
        }

        /// <summary>
        /// Resolves a capability implementation by contract type.
        /// </summary>
        /// <typeparam name="TCapability">The capability contract type.</typeparam>
        /// <returns>The registered capability implementation, or <c>null</c> when unavailable.</returns>
        public TCapability Get<TCapability>()
            where TCapability : class
        {
            object capability;
            return _capabilities.TryGetValue(typeof(TCapability), out capability) ? capability as TCapability : null;
        }

        /// <summary>
        /// Determines whether a capability contract has been registered.
        /// </summary>
        /// <param name="capabilityType">The capability contract type to test.</param>
        /// <returns><c>true</c> when a matching capability is registered; otherwise <c>false</c>.</returns>
        public bool Has(Type capabilityType)
        {
            return capabilityType != null && _capabilities.ContainsKey(capabilityType);
        }

        /// <summary>
        /// Attempts to resolve a capability implementation by contract type.
        /// </summary>
        /// <typeparam name="TCapability">The capability contract type.</typeparam>
        /// <param name="capability">Receives the registered capability when found.</param>
        /// <returns><c>true</c> when the capability exists; otherwise <c>false</c>.</returns>
        public bool TryGet<TCapability>(out TCapability capability)
            where TCapability : class
        {
            capability = Get<TCapability>();
            return capability != null;
        }
    }
}
