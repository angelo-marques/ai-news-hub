using System.Text.Json;
using DataFetcher.Models;

namespace DataFetcher.Services;

public class SemanticScholarService(HttpClient http)
{
    private const string ApiBase = "https://api.semanticscholar.org/graph/v1/paper/search";
    private const string Fields  = "paperId,title,abstract,authors,publicationDate,year,citationCount,externalIds,url";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Topics de IA mais relevantes para busca
    private static readonly string[] Queries =
    [
        "large language models",
        "deep learning computer vision",
        "reinforcement learning agents",
        "multimodal AI",
        "neural network architecture"
    ];

    public async Task<List<SemanticScholarPaper>> FetchRecentAsync(DateTime startDate, int limitPerQuery = 8)
    {
        var all = new List<SemanticScholarPaper>();
        var dateFilter = startDate.ToString("yyyy-MM-dd");

        foreach (var query in Queries)
        {
            try
            {
                var url = $"{ApiBase}?query={Uri.EscapeDataString(query)}" +
                          $"&fields={Fields}" +
                          $"&publicationDateOrYear={dateFilter}:" +
                          $"&limit={limitPerQuery}" +
                          $"&sort=publicationDate:desc";

                Console.WriteLine($"    SemanticScholar: '{query}'...");
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Add("Accept", "application/json");

                using var resp = await http.SendAsync(req);
                if (!resp.IsSuccessStatusCode)
                {
                    Console.WriteLine($"    [WARN] SemanticScholar {resp.StatusCode} para '{query}'");
                    await Task.Delay(1000);
                    continue;
                }

                var json = await resp.Content.ReadAsStringAsync();
                var response = JsonSerializer.Deserialize<SemanticScholarResponse>(json, JsonOptions);

                var papers = response?.Data
                    .Where(p => !string.IsNullOrWhiteSpace(p.Abstract) && p.Abstract.Length > 100)
                    .ToList() ?? [];

                all.AddRange(papers);
                Console.WriteLine($"    → {papers.Count} papers");

                await Task.Delay(1100); // Semantic Scholar: ~1 req/s sem API key
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    [ERRO] SemanticScholar '{query}': {ex.Message}");
                await Task.Delay(2000);
            }
        }

        // Dedup por paperId, ordenar por data mais recente
        return all
            .DistinctBy(p => p.PaperId)
            .Where(p => !string.IsNullOrWhiteSpace(p.PublicationDate))
            .OrderByDescending(p => p.PublicationDate)
            .ToList();
    }
}
