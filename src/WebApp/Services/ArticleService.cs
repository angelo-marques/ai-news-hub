using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Shared.Models;

namespace WebApp.Services;

public class ArticleService(HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private List<Article>? _cache;

    public async Task<List<Article>> LoadArticlesAsync(bool refresh = false)
    {
        if (_cache is not null && !refresh) return _cache;

        try
        {
            var json = await http.GetStringAsync($"data/articles.json?t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}");
            _cache = JsonSerializer.Deserialize<List<Article>>(json, JsonOptions) ?? [];
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ArticleService] Erro ao carregar artigos: {ex.Message}");
            _cache = [];
        }

        return _cache;
    }
}
