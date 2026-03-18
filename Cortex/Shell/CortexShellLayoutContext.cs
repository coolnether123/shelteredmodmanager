using System;
using Cortex.Core.Models;
using Cortex.Host.Unity.Runtime;

namespace Cortex.Shell
{
    internal sealed class CortexShellLayoutContext
    {
        private readonly Func<UnityWorkbenchRuntime> _workbenchRuntimeAccessor;

        public CortexShellLayoutContext(CortexShellState state, Func<UnityWorkbenchRuntime> workbenchRuntimeAccessor)
        {
            State = state;
            _workbenchRuntimeAccessor = workbenchRuntimeAccessor;
        }

        public CortexShellState State { get; private set; }

        public UnityWorkbenchRuntime WorkbenchRuntime
        {
            get { return _workbenchRuntimeAccessor != null ? _workbenchRuntimeAccessor() : null; }
        }
    }
}
