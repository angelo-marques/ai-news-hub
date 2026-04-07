using System.Text.Json;
using DataFetcher.Models;

namespace DataFetcher.Services;

public class PapersWithCodeService(HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<List<PwcPaper>> FetchRecentAsync(int pageSize = 15)
    {
        var url = $"https://paperswithcode.com/api/v1/papers/?ordering=-published&page_size={pageSize}";

        try
        {
            Console.WriteLine($"    GET {url}");
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("Accept", "application/json");

            using var resp = await http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                Console.WriteLine($"    [WARN] PwC API retornou {resp.StatusCode}");
                return [];
            }

            var json = await resp.Content.ReadAsStringAsync();
            var response = JsonSerializer.Deserialize<PwcResponse>(json, JsonOptions);
            var papers = response?.Results?
                .Where(p => !string.IsNullOrWhiteSpace(p.Abstract) && p.Abstract.Length > 80)
                .ToList() ?? [];

            Console.WriteLine($"    Parsed {papers.Count} papers do Papers with Code");
            return papers;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    [ERRO] PwC API: {ex.Message}");
            return [];
        }
    }
}
