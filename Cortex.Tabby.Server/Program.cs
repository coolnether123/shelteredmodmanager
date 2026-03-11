using Cortex.Tabby.Server.Backends;
using Cortex.Tabby.Server.Configuration;
using Cortex.Tabby.Server.Endpoints;
using Cortex.Tabby.Server.Protocol;
using Cortex.Tabby.Server.Services;

var parseResult = TabbyServerArgumentParser.Parse(args);
if (!parseResult.Success || parseResult.Options == null)
{
    Console.Error.WriteLine(parseResult.ErrorMessage ?? "Failed to parse Cortex.Tabby.Server arguments.");
    return 1;
}

var options = parseResult.Options;
var builder = WebApplication.CreateSlimBuilder(args);
builder.WebHost.UseUrls(options.ListenUrl);
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(console =>
{
    console.SingleLine = true;
    console.TimestampFormat = "HH:mm:ss ";
});

builder.Services.AddSingleton(options);
builder.Services.AddHttpClient<ICompletionBackend, OllamaCompletionBackend>();
builder.Services.AddSingleton<TabbyRequestContextFormatter>();
builder.Services.AddSingleton<TabbyRequestDebugFormatter>();
builder.Services.AddSingleton<TabbyCompletionRequestHandler>();
builder.Services.AddSingleton<TabbyCompletionEndpoint>();

var app = builder.Build();

app.Logger.LogInformation(
    "Starting Cortex.Tabby.Server. ListenUrl={ListenUrl}, CompletionUrl={CompletionUrl}, HealthUrl={HealthUrl}, OllamaUrl={OllamaUrl}, Model={Model}, TimeoutMs={TimeoutMs}.",
    options.ListenUrl,
    options.CompletionUrl,
    options.HealthUrl,
    options.OllamaBaseUrl,
    options.OllamaModel,
    options.RequestTimeoutMs);

app.Lifetime.ApplicationStarted.Register(() =>
{
    _ = Task.Run(async () =>
    {
        var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("Cortex.Tabby.Server.Startup");
        try
        {
            var backend = app.Services.GetRequiredService<ICompletionBackend>();
            var backendHealth = await backend.CheckHealthAsync(CancellationToken.None).ConfigureAwait(false);
            logger.LogInformation(
                "Startup backend check completed. Backend={Backend}, BackendHealthy={BackendHealthy}, ModelAvailable={ModelAvailable}, Endpoint={Endpoint}, Message={Message}",
                backend.BackendId,
                backendHealth.IsHealthy,
                backendHealth.ModelAvailable,
                backendHealth.Endpoint,
                backendHealth.Message);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Startup backend check failed.");
        }
    });
});

app.MapGet("/health", (ILoggerFactory loggerFactory) =>
{
    loggerFactory.CreateLogger("Cortex.Tabby.Server.Health")
        .LogInformation("Health probe served.");
    return Results.Json(new
    {
        status = "ok",
        provider = "ollama",
        model = options.OllamaModel,
        ollamaUrl = options.OllamaBaseUrl
    });
});

app.MapGet("/health/backend", async (ICompletionBackend backend, ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
{
    var logger = loggerFactory.CreateLogger("Cortex.Tabby.Server.BackendHealth");
    var backendHealth = await backend.CheckHealthAsync(cancellationToken).ConfigureAwait(false);
    logger.LogInformation(
        "Backend health probe served. Backend={Backend}, BackendHealthy={BackendHealthy}, ModelAvailable={ModelAvailable}, Endpoint={Endpoint}, Message={Message}",
        backend.BackendId,
        backendHealth.IsHealthy,
        backendHealth.ModelAvailable,
        backendHealth.Endpoint,
        backendHealth.Message);
    return Results.Json(new
    {
        status = backendHealth.IsHealthy ? "ok" : "degraded",
        provider = backend.BackendId,
        model = options.OllamaModel,
        ollamaUrl = options.OllamaBaseUrl,
        backendHealthy = backendHealth.IsHealthy,
        modelAvailable = backendHealth.ModelAvailable,
        backendEndpoint = backendHealth.Endpoint,
        backendMessage = backendHealth.Message
    });
});

app.MapPost("/api/completion", async (TabbyCompletionRequest request, TabbyCompletionEndpoint endpoint, CancellationToken cancellationToken) =>
    await endpoint.HandleAsync(request, cancellationToken));

app.Run();
return 0;
