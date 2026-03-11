namespace Cortex.Tabby.Server.Backends;

public sealed class CompletionBackendResult
{
    public string[] Choices { get; init; } = Array.Empty<string>();
}
