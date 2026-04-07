using DataFetcher.Models;
using DataFetcher.Services;
using Shared.Models;

Console.WriteLine($"╔══════════════════════════════════════════════════════╗");
Console.WriteLine($"║         Hub IA Brasil — Coletor de Notícias          ║");
Console.WriteLine($"╚══════════════════════════════════════════════════════╝");
Console.WriteLine($"  Horário: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
Console.WriteLine();

// --- Validate API Key ---
var apiKey = Environment.GetEnvironmentVariable("GROQ_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.WriteLine("❌ GROQ_API_KEY não definido. Abortando.");
    Environment.Exit(1);
}

// --- Setup HTTP Client ---
using var httpClient = new HttpClient(new HttpClientHandler
{
    AutomaticDecompression = System.Net.DecompressionMethods.All
});
httpClient.DefaultRequestHeaders.Add("User-Agent",
    "HubIABrasil/1.0 (github.com/angelo-marques/ai-news-hub; contact@hubiasbrasil.dev)");
httpClient.Timeout = TimeSpan.FromSeconds(60);

// --- Initialize Services ---
var arxivService    = new ArxivService(httpClient);
var pwcService      = new PapersWithCodeService(httpClient);
var translator      = new GroqTranslatorService(httpClient, apiKey);
var storage         = new ArticleStorageService();

// --- Load State ---
Console.WriteLine("📂 Carregando estado anterior...");
var seenIds          = await storage.LoadSeenIdsAsync();
var existingArticles = await storage.LoadArticlesAsync();

Console.WriteLine($"  ↳ IDs já processados: {seenIds.Ids.Count}");
Console.WriteLine($"  ↳ Artigos existentes: {existingArticles.Count}");
Console.WriteLine();

var newArticles = new List<Article>();
const int MaxPerSource = 3; // 3 de cada fonte = até 6 por execução

// ═══════════════════════════════════════════════
// FONTE 1: arXiv
// ═══════════════════════════════════════════════
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("📡 [1/2] Buscando papers no arXiv...");

try
{
    var arxivPapers = await arxivService.FetchRecentAsync(maxResults: 30);
    int arxivCount = 0;

    foreach (var paper in arxivPapers)
    {
        if (arxivCount >= MaxPerSource) break;

        var fullId = $"arxiv:{paper.Id}";
        if (seenIds.Ids.Contains(fullId))
        {
            Console.WriteLine($"  ⊘ [{paper.Id}] já processado");
            continue;
        }

        Console.WriteLine($"\n  ✎ Traduzindo: {Truncate(paper.Title, 72)}");
        Console.WriteLine($"    Autores: {Truncate(string.Join(", ", paper.Authors.Take(2)), 60)}");
        Console.WriteLine($"    Data: {paper.PublishedAt:dd/MM/yyyy}");

        var article = await translator.TranslateArxivAsync(paper);
        if (article is not null)
        {
            newArticles.Add(article);
            seenIds.Ids.Add(fullId);
            arxivCount++;
            Console.WriteLine($"    ✅ → {Truncate(article.TitlePt, 72)}");
            Console.WriteLine($"       Tags: {string.Join(", ", article.Tags.Take(4))}");
        }
        else
        {
            Console.WriteLine($"    ⚠️ Falha na tradução — pulando");
        }

        await Task.Delay(2000); // Respeitar rate limit da API
    }

    Console.WriteLine($"\n  arXiv: {arxivCount} novo(s) artigo(s)");
}
catch (Exception ex)
{
    Console.WriteLine($"  ❌ Erro no arXiv: {ex.Message}");
}

// ═══════════════════════════════════════════════
// FONTE 2: Papers with Code
// ═══════════════════════════════════════════════
Console.WriteLine();
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("📡 [2/2] Buscando papers no Papers with Code...");

try
{
    var pwcPapers = await pwcService.FetchRecentAsync(pageSize: 20);
    int pwcCount = 0;

    foreach (var paper in pwcPapers)
    {
        if (pwcCount >= MaxPerSource) break;

        var fullId   = $"pwc:{paper.Id}";
        var arxivRef = !string.IsNullOrEmpty(paper.ArxivId) ? $"arxiv:{paper.ArxivId}" : null;

        // Skip se já visto via qualquer ID
        if (seenIds.Ids.Contains(fullId) || (arxivRef is not null && seenIds.Ids.Contains(arxivRef)))
        {
            Console.WriteLine($"  ⊘ [{paper.Id}] já processado");
            continue;
        }

        Console.WriteLine($"\n  ✎ Traduzindo: {Truncate(paper.Title, 72)}");
        Console.WriteLine($"    Autores: {Truncate(string.Join(", ", paper.Authors.Take(2)), 60)}");
        if (!string.IsNullOrEmpty(paper.Proceeding))
            Console.WriteLine($"    Conf: {paper.Proceeding}");

        var article = await translator.TranslatePwcAsync(paper);
        if (article is not null)
        {
            newArticles.Add(article);
            seenIds.Ids.Add(fullId);
            if (arxivRef is not null) seenIds.Ids.Add(arxivRef); // cross-dedup
            pwcCount++;
            Console.WriteLine($"    ✅ → {Truncate(article.TitlePt, 72)}");
            Console.WriteLine($"       Tags: {string.Join(", ", article.Tags.Take(4))}");
        }
        else
        {
            Console.WriteLine($"    ⚠️ Falha na tradução — pulando");
        }

        await Task.Delay(2000);
    }

    Console.WriteLine($"\n  Papers with Code: {pwcCount} novo(s) artigo(s)");
}
catch (Exception ex)
{
    Console.WriteLine($"  ❌ Erro no Papers with Code: {ex.Message}");
}

// ═══════════════════════════════════════════════
// PERSISTÊNCIA
// ═══════════════════════════════════════════════
Console.WriteLine();
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("💾 Salvando dados...");

// Prune artigos com mais de 30 dias
var cutoff = DateTime.UtcNow.AddDays(-30);
seenIds.LastUpdated = DateTime.UtcNow;

var allArticles = newArticles
    .Concat(existingArticles.Where(a => a.FetchedAt >= cutoff))
    .OrderByDescending(a => a.PublishedAt)
    .DistinctBy(a => a.Id)
    .ToList();

await storage.SaveArticlesAsync(allArticles);
await storage.SaveSeenIdsAsync(seenIds);

Console.WriteLine();
Console.WriteLine("╔══════════════════════════════════════════════════════╗");
Console.WriteLine($"║  ✅ Concluído com sucesso!                           ║");
Console.WriteLine($"║     Novos artigos:    {newArticles.Count,-4}                         ║");
Console.WriteLine($"║     Total de artigos: {allArticles.Count,-4} (últimos 30 dias)     ║");
Console.WriteLine($"║     IDs rastreados:   {seenIds.Ids.Count,-5}                        ║");
Console.WriteLine("╚══════════════════════════════════════════════════════╝");

static string Truncate(string s, int max) =>
    s.Length <= max ? s : s[..max] + "…";
