using Cortex.Tabby.Server.Protocol;
using Cortex.Tabby.Server.Services;

namespace Cortex.Tabby.Server.Endpoints;

public sealed class TabbyCompletionEndpoint
{
    private readonly ILogger<TabbyCompletionEndpoint> _logger;
    private readonly TabbyCompletionRequestHandler _handler;

    public TabbyCompletionEndpoint(
        ILogger<TabbyCompletionEndpoint> logger,
        TabbyCompletionRequestHandler handler)
    {
        _logger = logger;
        _handler = handler;
    }

    public async Task<IResult> HandleAsync(
        TabbyCompletionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Completion request received. File={File}, Language={Language}, PrefixLength={PrefixLength}, SuffixLength={SuffixLength}, Snippets={SnippetCount}.",
                request != null ? request.FilePath : string.Empty,
                request != null ? request.Language : string.Empty,
                request != null && request.Segments != null && request.Segments.Prefix != null ? request.Segments.Prefix.Length : 0,
                request != null && request.Segments != null && request.Segments.Suffix != null ? request.Segments.Suffix.Length : 0,
                request != null && request.RelevantSnippets != null ? request.RelevantSnippets.Length : 0);
            var response = await _handler.HandleAsync(request, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(
                "Completion request succeeded. Choices={ChoiceCount}, File={File}.",
                response != null && response.Choices != null ? response.Choices.Length : 0,
                request != null ? request.FilePath : string.Empty);
            return Results.Json(response);
        }
        catch (TabbyRequestValidationException ex)
        {
            _logger.LogWarning("Completion request rejected: {Message}", ex.Message);
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Completion request timed out.");
            return Results.StatusCode(StatusCodes.Status504GatewayTimeout);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tabby completion request failed.");
            return Results.Problem(
                title: "Tabby completion request failed",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
