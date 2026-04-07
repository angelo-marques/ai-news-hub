using System.Text.Json;
using DataFetcher.Models;

namespace DataFetcher.Services;

public class HuggingFaceService(HttpClient http)
{
    private const string ApiUrl = "https://huggingface.co/api/daily_papers";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<List<HuggingFacePaper>> FetchRecentAsync(int maxResults = 20)
    {
        try
        {
            Console.WriteLine($"    GET {ApiUrl}");
            using var req = new HttpRequestMessage(HttpMethod.Get, ApiUrl);
            req.Headers.Add("Accept", "application/json");

            using var resp = await http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                Console.WriteLine($"    [WARN] HuggingFace API retornou {resp.StatusCode}");
                return [];
            }

            var json = await resp.Content.ReadAsStringAsync();
            var items = JsonSerializer.Deserialize<List<HfDailyResponse>>(json, JsonOptions) ?? [];

            var papers = items
                .Select(i => i.Paper)
                .Where(p => p is not null && !string.IsNullOrWhiteSpace(p.Summary) && p.Summary.Length > 80)
                .Cast<HuggingFacePaper>()
                .OrderByDescending(p => p.Upvotes)
                .Take(maxResults)
                .ToList();

            Console.WriteLine($"    Parsed {papers.Count} papers do HuggingFace Daily");
            return papers;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    [ERRO] HuggingFace API: {ex.Message}");
            return [];
        }
    }
}
