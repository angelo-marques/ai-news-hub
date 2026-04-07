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
var arxivService   = new ArxivService(httpClient);
var pwcService     = new PapersWithCodeService(httpClient);
var hfService      = new HuggingFaceService(httpClient);
var ssService      = new SemanticScholarService(httpClient);
var translator     = new AiTranslatorService(httpClient, apiKey);
var storage        = new ArticleStorageService();

// --- Load State ---
Console.WriteLine("📂 Carregando estado anterior...");
var seenIds          = await storage.LoadSeenIdsAsync();
var existingArticles = await storage.LoadArticlesAsync();

Console.WriteLine($"  ↳ IDs já processados: {seenIds.Ids.Count}");
Console.WriteLine($"  ↳ Artigos existentes: {existingArticles.Count}");
Console.WriteLine();

// --- Fetch window: sempre desde 01/04/2026, seenIds evita reprocessamento ---
bool isBackfill = true;
var backfillStart  = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
int maxPerSource   = 100;   // máximo de traduções por fonte por execução

Console.WriteLine($"  📅 Janela de busca: {backfillStart:dd/MM/yyyy} → hoje");
Console.WriteLine($"  🔢 Máximo por fonte: {maxPerSource}");
Console.WriteLine();

var newArticles = new List<Article>();

// ═══════════════════════════════════════════════
// FONTE 1: arXiv
// ═══════════════════════════════════════════════
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("📡 [1/4] Buscando papers no arXiv...");

try
{
    var arxivPapers = isBackfill
        ? await arxivService.FetchFromDateAsync(backfillStart, maxResults: 300)
        : await arxivService.FetchRecentAsync(maxResults: 150);

    int arxivCount = 0;
    foreach (var paper in arxivPapers)
    {
        if (arxivCount >= maxPerSource) break;

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
        else Console.WriteLine($"    ⚠️ Falha na tradução — pulando");

        await Task.Delay(2000);
    }
    Console.WriteLine($"\n  arXiv: {arxivCount} novo(s) artigo(s)");
}
catch (Exception ex) { Console.WriteLine($"  ❌ Erro no arXiv: {ex.Message}"); }

// ═══════════════════════════════════════════════
// FONTE 2: Papers with Code
// ═══════════════════════════════════════════════
Console.WriteLine();
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("📡 [2/4] Buscando papers no Papers with Code...");

try
{
    var pwcPapers = isBackfill
        ? await pwcService.FetchFromDateAsync(backfillStart, pageSize: 250)
        : await pwcService.FetchRecentAsync(pageSize: 150);

    int pwcCount = 0;
    foreach (var paper in pwcPapers)
    {
        if (pwcCount >= maxPerSource) break;

        var fullId   = $"pwc:{paper.Id}";
        var arxivRef = !string.IsNullOrEmpty(paper.ArxivId) ? $"arxiv:{paper.ArxivId}" : null;

        if (seenIds.Ids.Contains(fullId) || (arxivRef is not null && seenIds.Ids.Contains(arxivRef)))
        {
            Console.WriteLine($"  ⊘ [{paper.Id}] já processado");
            continue;
        }

        Console.WriteLine($"\n  ✎ Traduzindo: {Truncate(paper.Title, 72)}");
        Console.WriteLine($"    Autores: {Truncate(string.Join(", ", paper.Authors.Take(2)), 60)}");

        var article = await translator.TranslatePwcAsync(paper);
        if (article is not null)
        {
            newArticles.Add(article);
            seenIds.Ids.Add(fullId);
            if (arxivRef is not null) seenIds.Ids.Add(arxivRef);
            pwcCount++;
            Console.WriteLine($"    ✅ → {Truncate(article.TitlePt, 72)}");
            Console.WriteLine($"       Tags: {string.Join(", ", article.Tags.Take(4))}");
        }
        else Console.WriteLine($"    ⚠️ Falha na tradução — pulando");

        await Task.Delay(2000);
    }
    Console.WriteLine($"\n  Papers with Code: {pwcCount} novo(s) artigo(s)");
}
catch (Exception ex) { Console.WriteLine($"  ❌ Erro no Papers with Code: {ex.Message}"); }

// ═══════════════════════════════════════════════
// FONTE 3: HuggingFace Daily Papers
// ═══════════════════════════════════════════════
Console.WriteLine();
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("📡 [3/4] Buscando papers no HuggingFace Daily...");

try
{
    var hfPapers = isBackfill
        ? await hfService.FetchFromDateAsync(backfillStart)
        : await hfService.FetchRecentAsync(maxResults: 150);

    int hfCount = 0;
    foreach (var paper in hfPapers)
    {
        if (hfCount >= maxPerSource) break;

        var fullId   = $"hf:{paper.Id}";
        var arxivRef = $"arxiv:{paper.Id}";

        if (seenIds.Ids.Contains(fullId) || seenIds.Ids.Contains(arxivRef))
        {
            Console.WriteLine($"  ⊘ [{paper.Id}] já processado");
            continue;
        }

        Console.WriteLine($"\n  ✎ Traduzindo: {Truncate(paper.Title, 72)}");
        Console.WriteLine($"    Upvotes: {paper.Upvotes}");

        var article = await translator.TranslateHfAsync(paper);
        if (article is not null)
        {
            newArticles.Add(article);
            seenIds.Ids.Add(fullId);
            seenIds.Ids.Add(arxivRef);
            hfCount++;
            Console.WriteLine($"    ✅ → {Truncate(article.TitlePt, 72)}");
            Console.WriteLine($"       Tags: {string.Join(", ", article.Tags.Take(4))}");
        }
        else Console.WriteLine($"    ⚠️ Falha na tradução — pulando");

        await Task.Delay(2000);
    }
    Console.WriteLine($"\n  HuggingFace: {hfCount} novo(s) artigo(s)");
}
catch (Exception ex) { Console.WriteLine($"  ❌ Erro no HuggingFace: {ex.Message}"); }

// ═══════════════════════════════════════════════
// FONTE 4: Semantic Scholar
// ═══════════════════════════════════════════════
Console.WriteLine();
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("📡 [4/4] Buscando papers no Semantic Scholar...");

try
{
    var ssPapers = await ssService.FetchRecentAsync(backfillStart, limitPerQuery: 50);
    int ssCount = 0;

    foreach (var paper in ssPapers)
    {
        if (ssCount >= maxPerSource) break;

        var fullId   = $"ss:{paper.PaperId}";
        var arxivRef = paper.ExternalIds?.ArXiv is not null ? $"arxiv:{paper.ExternalIds.ArXiv}" : null;

        if (seenIds.Ids.Contains(fullId) || (arxivRef is not null && seenIds.Ids.Contains(arxivRef)))
        {
            Console.WriteLine($"  ⊘ [{paper.PaperId[..Math.Min(12, paper.PaperId.Length)]}] já processado");
            continue;
        }

        Console.WriteLine($"\n  ✎ Traduzindo: {Truncate(paper.Title, 72)}");
        Console.WriteLine($"    Citações: {paper.CitationCount}");

        var article = await translator.TranslateSemanticScholarAsync(paper);
        if (article is not null)
        {
            newArticles.Add(article);
            seenIds.Ids.Add(fullId);
            if (arxivRef is not null) seenIds.Ids.Add(arxivRef);
            ssCount++;
            Console.WriteLine($"    ✅ → {Truncate(article.TitlePt, 72)}");
            Console.WriteLine($"       Tags: {string.Join(", ", article.Tags.Take(4))}");
        }
        else Console.WriteLine($"    ⚠️ Falha na tradução — pulando");

        await Task.Delay(2000);
    }
    Console.WriteLine($"\n  Semantic Scholar: {ssCount} novo(s) artigo(s)");
}
catch (Exception ex) { Console.WriteLine($"  ❌ Erro no Semantic Scholar: {ex.Message}"); }

// ═══════════════════════════════════════════════
// PERSISTÊNCIA
// ═══════════════════════════════════════════════
Console.WriteLine();
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("💾 Salvando dados...");

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
Console.WriteLine($"║     Modo:             {(isBackfill ? "BACKFILL" : "Diário  "),-8}                     ║");
Console.WriteLine($"║     Novos artigos:    {newArticles.Count,-4}                         ║");
Console.WriteLine($"║     Total de artigos: {allArticles.Count,-4} (últimos 30 dias)     ║");
Console.WriteLine($"║     IDs rastreados:   {seenIds.Ids.Count,-5}                        ║");
Console.WriteLine("╚══════════════════════════════════════════════════════╝");

static string Truncate(string s, int max) =>
    s.Length <= max ? s : s[..max] + "…";
