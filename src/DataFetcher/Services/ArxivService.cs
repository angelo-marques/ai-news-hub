using System.Xml.Linq;
using DataFetcher.Models;
using Markdig;
using Shared.Models;

namespace DataFetcher.Services;

public class ArxivService(HttpClient http)
{
    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";

    // cs.AI, cs.LG (Machine Learning), cs.CL (NLP), cs.CV (Computer Vision)
    private const string Categories = "cat:cs.AI+OR+cat:cs.LG+OR+cat:cs.CL+OR+cat:cs.CV";

    public async Task<List<ArxivPaper>> FetchRecentAsync(int maxResults = 25)
    {
        var url = $"https://export.arxiv.org/api/query?" +
                  $"search_query={Categories}" +
                  $"&sortBy=submittedDate&sortOrder=descending" +
                  $"&max_results={maxResults}";

        return await FetchUrl(url);
    }

    public async Task<List<ArxivPaper>> FetchFromDateAsync(DateTime startDate, int maxResults = 100)
    {
        var from = startDate.ToString("yyyyMMdd") + "000000";
        var to   = DateTime.UtcNow.ToString("yyyyMMdd") + "235959";

        var url = $"https://export.arxiv.org/api/query?" +
                  $"search_query={Categories}+AND+submittedDate:[{from}+TO+{to}]" +
                  $"&sortBy=submittedDate&sortOrder=descending" +
                  $"&max_results={maxResults}";

        return await FetchUrl(url);
    }

    private async Task<List<ArxivPaper>> FetchUrl(string url)
    {
        try
        {
            Console.WriteLine($"    GET {url[..Math.Min(100, url.Length)]}...");
            var xml = await http.GetStringAsync(url);
            var doc = XDocument.Parse(xml);

            var papers = doc.Root!
                .Elements(Atom + "entry")
                .Select(ParseEntry)
                .Where(p => !string.IsNullOrWhiteSpace(p.Abstract) && p.Abstract.Length > 100)
                .OrderByDescending(p => p.PublishedAt)
                .ToList();

            Console.WriteLine($"    Parsed {papers.Count} papers do arXiv");
            return papers;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    [ERRO] arXiv API: {ex.Message}");
            return [];
        }
    }

    private static ArxivPaper ParseEntry(XElement e)
    {
        var idRaw = e.Element(Atom + "id")?.Value ?? "";
        var id = ParseId(idRaw);

        var authors = e.Elements(Atom + "author")
            .Select(a => a.Element(Atom + "name")?.Value?.Trim() ?? "")
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToArray();

        var categories = e.Elements(Atom + "category")
            .Select(c => c.Attribute("term")?.Value ?? "")
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToArray();

        var publishedStr = e.Element(Atom + "published")?.Value;
        var published = publishedStr is not null
            ? DateTime.Parse(publishedStr, null, System.Globalization.DateTimeStyles.RoundtripKind)
            : DateTime.UtcNow;

        var title = e.Element(Atom + "title")?.Value
            .Replace("\n", " ").Replace("  ", " ").Trim() ?? "";

        var abstract_ = e.Element(Atom + "summary")?.Value
            .Replace("\n", " ").Replace("  ", " ").Trim() ?? "";

        return new ArxivPaper
        {
            Id         = id,
            Title      = title,
            Abstract   = abstract_,
            Authors    = authors,
            PublishedAt = published,
            PaperUrl   = $"https://arxiv.org/abs/{id}",
            PdfUrl     = $"https://arxiv.org/pdf/{id}",
            Categories = categories
        };
    }

    private static string ParseId(string idUrl)
    {
        var id = idUrl
            .Replace("http://arxiv.org/abs/", "")
            .Replace("https://arxiv.org/abs/", "")
            .Trim();

        var vIndex = id.LastIndexOf('v');
        if (vIndex > 0 && int.TryParse(id[(vIndex + 1)..], out _))
            id = id[..vIndex];

        return id;
    }
}
