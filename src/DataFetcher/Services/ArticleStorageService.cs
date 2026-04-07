using System.Text.Json;
using System.Text.Json.Serialization;
using Shared.Models;

namespace DataFetcher.Services;

public class ArticleStorageService
{
    private readonly string _dataDir;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ArticleStorageService()
    {
        _dataDir = ResolveDataDir();
        Directory.CreateDirectory(_dataDir);
        Console.WriteLine($"  DataDir: {_dataDir}");
    }

    public async Task<List<Article>> LoadArticlesAsync()
    {
        var path = Path.Combine(_dataDir, "articles.json");
        if (!File.Exists(path)) return [];
        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<List<Article>>(json, JsonOptions) ?? [];
    }

    public async Task SaveArticlesAsync(List<Article> articles)
    {
        var path = Path.Combine(_dataDir, "articles.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(articles, JsonOptions));
    }

    public async Task<SeenIds> LoadSeenIdsAsync()
    {
        var path = Path.Combine(_dataDir, "seen-ids.json");
        if (!File.Exists(path)) return new SeenIds();
        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<SeenIds>(json, JsonOptions) ?? new SeenIds();
    }

    public async Task SaveSeenIdsAsync(SeenIds seenIds)
    {
        var path = Path.Combine(_dataDir, "seen-ids.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(seenIds, JsonOptions));
    }

    private static string ResolveDataDir()
    {
        // 1. Explicit env var (used in GitHub Actions)
        var envDir = Environment.GetEnvironmentVariable("DATA_DIR");
        if (!string.IsNullOrEmpty(envDir))
            return Path.GetFullPath(envDir);

        // 2. Walk up from current directory looking for repo root (.git)
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            var gitPath = Path.Combine(current.FullName, ".git");
            if (Directory.Exists(gitPath) || File.Exists(gitPath))
            {
                var dataPath = Path.Combine(current.FullName, "data");
                Directory.CreateDirectory(dataPath);
                return dataPath;
            }

            var dataDir = Path.Combine(current.FullName, "data");
            if (Directory.Exists(dataDir)) return dataDir;

            current = current.Parent!;
        }

        // 3. Fallback: data/ relative to CWD
        return Path.GetFullPath("data");
    }
}
