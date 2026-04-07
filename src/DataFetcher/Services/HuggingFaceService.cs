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

    /// Fetch today's papers only
    public async Task<List<HuggingFacePaper>> FetchRecentAsync(int maxResults = 20)
    {
        var papers = await FetchDayAsync(DateTime.UtcNow.Date);
        return papers.Take(maxResults).ToList();
    }

    /// Fetch ALL papers from startDate to today — sem limite por dia, seenIds faz o dedup
    public async Task<List<HuggingFacePaper>> FetchFromDateAsync(DateTime startDate)
    {
        var all = new List<HuggingFacePaper>();
        var today = DateTime.UtcNow.Date;

        for (var date = startDate.Date; date <= today; date = date.AddDays(1))
        {
            Console.WriteLine($"    HuggingFace: buscando {date:dd/MM/yyyy}...");
            var papers = await FetchDayAsync(date);
            Console.WriteLine($"      → {papers.Count} papers disponíveis neste dia");
            all.AddRange(papers);
            if (date < today)
                await Task.Delay(400);
        }

        // Ordena por upvotes para priorizar os mais relevantes
        return all.OrderByDescending(p => p.Upvotes).ToList();
    }

    private async Task<List<HuggingFacePaper>> FetchDayAsync(DateTime date)
    {
        try
        {
            var url = $"{ApiUrl}?date={date:yyyy-MM-dd}";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("Accept", "application/json");

            using var resp = await http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                Console.WriteLine($"    [WARN] HuggingFace {date:dd/MM}: {resp.StatusCode}");
                return [];
            }

            var json = await resp.Content.ReadAsStringAsync();
            var items = JsonSerializer.Deserialize<List<HfDailyResponse>>(json, JsonOptions) ?? [];

            // Retorna TODOS os papers do dia — sem Take() — deixa o Program.cs controlar o limite
            return items
                .Select(i => i.Paper)
                .Where(p => p is not null && !string.IsNullOrWhiteSpace(p.Summary) && p.Summary.Length > 60)
                .Cast<HuggingFacePaper>()
                .OrderByDescending(p => p.Upvotes)
                .ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    [ERRO] HuggingFace {date:dd/MM}: {ex.Message}");
            return [];
        }
    }
}
