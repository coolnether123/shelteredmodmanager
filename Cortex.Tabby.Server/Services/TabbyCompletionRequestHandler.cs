using Cortex.Tabby.Server.Backends;
using Cortex.Tabby.Server.Protocol;

namespace Cortex.Tabby.Server.Services;

public sealed class TabbyCompletionRequestHandler
{
    private readonly ICompletionBackend _backend;
    private readonly ILogger<TabbyCompletionRequestHandler> _logger;
    private readonly TabbyRequestDebugFormatter _debugFormatter;

    public TabbyCompletionRequestHandler(
        ICompletionBackend backend,
        ILogger<TabbyCompletionRequestHandler> logger,
        TabbyRequestDebugFormatter debugFormatter)
    {
        _backend = backend;
        _logger = logger;
        _debugFormatter = debugFormatter;
    }

    public async Task<TabbyCompletionResponse> HandleAsync(
        TabbyCompletionRequest? request,
        CancellationToken cancellationToken)
    {
        Validate(request);
        var validatedRequest = request!;

        var result = await _backend.CompleteAsync(validatedRequest, cancellationToken).ConfigureAwait(false);
        var choices = result.Choices ?? Array.Empty<string>();
        _logger.LogInformation(
            "Backend completion returned. Backend={Backend}, {Summary}",
            _backend.BackendId,
            _debugFormatter.BuildChoiceSummary(choices));
        return new TabbyCompletionResponse
        {
            Id = Guid.NewGuid().ToString("N"),
            Choices = choices
                .Where(choice => !string.IsNullOrWhiteSpace(choice))
                .Select((choice, index) => new TabbyCompletionChoice
                {
                    Index = index,
                    Text = choice
                })
                .ToArray()
        };
    }

    private static void Validate(TabbyCompletionRequest? request)
    {
        if (request == null)
        {
            throw new TabbyRequestValidationException("Request body was required.");
        }

        if (request.Segments == null)
        {
            throw new TabbyRequestValidationException("Request body was missing completion segments.");
        }
    }
}

public sealed class TabbyRequestValidationException : Exception
{
    public TabbyRequestValidationException(string message)
        : base(message)
    {
    }
}
