using System.Text.Json.Serialization;

namespace Cortex.Tabby.Server.Protocol;

public sealed class TabbyCompletionResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("choices")]
    public TabbyCompletionChoice[] Choices { get; set; } = Array.Empty<TabbyCompletionChoice>();
}

public sealed class TabbyCompletionChoice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}
