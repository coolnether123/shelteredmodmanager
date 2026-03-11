using Cortex.Tabby.Server.Protocol;

namespace Cortex.Tabby.Server.Backends;

public interface ICompletionBackend
{
    string BackendId { get; }

    Task<CompletionBackendHealthStatus> CheckHealthAsync(CancellationToken cancellationToken);

    Task<CompletionBackendResult> CompleteAsync(
        TabbyCompletionRequest request,
        CancellationToken cancellationToken);
}
