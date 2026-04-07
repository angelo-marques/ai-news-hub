using System.Text.Json.Serialization;

namespace DataFetcher.Models;

public record SemanticScholarResponse
{
    [JsonPropertyName("data")]
    public List<SemanticScholarPaper> Data { get; init; } = [];
}

public record SemanticScholarPaper
{
    [JsonPropertyName("paperId")]
    public string PaperId { get; init; } = "";

    [JsonPropertyName("title")]
    public string Title { get; init; } = "";

    [JsonPropertyName("abstract")]
    public string? Abstract { get; init; }

    [JsonPropertyName("authors")]
    public SemanticScholarAuthor[] Authors { get; init; } = [];

    [JsonPropertyName("publicationDate")]
    public string? PublicationDate { get; init; }

    [JsonPropertyName("year")]
    public int? Year { get; init; }

    [JsonPropertyName("citationCount")]
    public int CitationCount { get; init; }

    [JsonPropertyName("externalIds")]
    public SemanticScholarExternalIds? ExternalIds { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; init; }
}

public record SemanticScholarAuthor
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";
}

public record SemanticScholarExternalIds
{
    [JsonPropertyName("ArXiv")]
    public string? ArXiv { get; init; }
}
