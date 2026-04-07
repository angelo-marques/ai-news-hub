using System.Text.Json.Serialization;

namespace DataFetcher.Models;

public record TranslationResult
{
    [JsonPropertyName("titlePt")]
    public string TitlePt { get; init; } = "";

    [JsonPropertyName("newsText")]
    public string NewsText { get; init; } = "";

    [JsonPropertyName("tags")]
    public string[] Tags { get; init; } = [];

    [JsonIgnore]
    public string Provider { get; set; } = "";
}

public record AiApiResponse
{
    [JsonPropertyName("choices")]
    public AiApiChoice[] Choices { get; init; } = [];
}

public record AiApiChoice
{
    [JsonPropertyName("message")]
    public AiApiMessage? Message { get; init; }
}

public record AiApiMessage
{
    [JsonPropertyName("content")]
    public string Content { get; init; } = "";
}
