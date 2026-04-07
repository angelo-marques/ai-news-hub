using System.Text.Json.Serialization;

namespace DataFetcher.Models;

public record HfDailyResponse
{
    [JsonPropertyName("paper")]
    public HuggingFacePaper? Paper { get; init; }
}

public record HuggingFacePaper
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("title")]
    public string Title { get; init; } = "";

    [JsonPropertyName("summary")]
    public string Summary { get; init; } = "";

    [JsonPropertyName("authors")]
    public HfAuthor[] Authors { get; init; } = [];

    [JsonPropertyName("publishedAt")]
    public string? PublishedAt { get; init; }

    [JsonPropertyName("upvotes")]
    public int Upvotes { get; init; }
}

public record HfAuthor
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";
}
