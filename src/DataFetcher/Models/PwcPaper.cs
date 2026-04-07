using System.Text.Json.Serialization;

namespace DataFetcher.Models;

public record PwcResponse
{
    [JsonPropertyName("count")]
    public int Count { get; init; }

    [JsonPropertyName("results")]
    public List<PwcPaper> Results { get; init; } = [];
}

public record PwcPaper
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("arxiv_id")]
    public string? ArxivId { get; init; }

    [JsonPropertyName("url_abs")]
    public string UrlAbs { get; init; } = "";

    [JsonPropertyName("url_pdf")]
    public string? UrlPdf { get; init; }

    [JsonPropertyName("title")]
    public string Title { get; init; } = "";

    [JsonPropertyName("abstract")]
    public string? Abstract { get; init; }

    [JsonPropertyName("authors")]
    public string[] Authors { get; init; } = [];

    [JsonPropertyName("published")]
    public string? Published { get; init; }

    [JsonPropertyName("proceeding")]
    public string? Proceeding { get; init; }
}
