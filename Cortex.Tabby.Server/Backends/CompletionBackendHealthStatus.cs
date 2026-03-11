namespace Cortex.Tabby.Server.Backends;

public sealed class CompletionBackendHealthStatus
{
    public bool IsHealthy { get; init; }

    public bool ModelAvailable { get; init; }

    public string Message { get; init; } = string.Empty;

    public string Endpoint { get; init; } = string.Empty;
}
