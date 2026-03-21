using System;
using Cortex.Core.Models;
using Cortex.Presentation.Abstractions;

namespace Cortex
{
    internal sealed class CortexShellLayoutContext
    {
        private readonly Func<IWorkbenchRuntime> _workbenchRuntimeAccessor;

        public CortexShellLayoutContext(CortexShellState state, Func<IWorkbenchRuntime> workbenchRuntimeAccessor)
        {
            State = state;
            _workbenchRuntimeAccessor = workbenchRuntimeAccessor;
        }

        public CortexShellState State { get; private set; }

        public IWorkbenchRuntime WorkbenchRuntime
        {
            get { return _workbenchRuntimeAccessor != null ? _workbenchRuntimeAccessor() : null; }
        }
    }
}
