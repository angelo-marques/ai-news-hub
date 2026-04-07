using System.Text.Json.Serialization;

namespace Shared.Models;

public record Article
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("title")]
    public string Title { get; init; } = "";

    [JsonPropertyName("titlePt")]
    public string TitlePt { get; init; } = "";

    [JsonPropertyName("newsText")]
    public string NewsText { get; init; } = "";

    [JsonPropertyName("newsHtml")]
    public string NewsHtml { get; init; } = "";

    [JsonPropertyName("authors")]
    public string[] Authors { get; init; } = [];

    [JsonPropertyName("source")]
    public string Source { get; init; } = "";

    [JsonPropertyName("sourceUrl")]
    public string SourceUrl { get; init; } = "";

    [JsonPropertyName("paperUrl")]
    public string PaperUrl { get; init; } = "";

    [JsonPropertyName("codeUrl")]
    public string? CodeUrl { get; init; }

    [JsonPropertyName("publishedAt")]
    public DateTime PublishedAt { get; init; }

    [JsonPropertyName("fetchedAt")]
    public DateTime FetchedAt { get; init; }

    [JsonPropertyName("tags")]
    public string[] Tags { get; init; } = [];

    [JsonPropertyName("translatorProvider")]
    public string TranslatorProvider { get; init; } = "";
}
