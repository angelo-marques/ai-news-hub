namespace DataFetcher.Models;

public record ArxivPaper
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Abstract { get; init; }
    public required string[] Authors { get; init; }
    public required DateTime PublishedAt { get; init; }
    public required string PaperUrl { get; init; }
    public required string PdfUrl { get; init; }
    public required string[] Categories { get; init; }
}
