using Cortex.Presentation.Abstractions;

namespace Cortex.Host.Unity.Runtime
{
    public sealed class UnityWorkbenchRuntimeFactory : IWorkbenchRuntimeFactory
    {
        public IWorkbenchRuntime Create()
        {
            return new UnityWorkbenchRuntime();
        }
    }
}
