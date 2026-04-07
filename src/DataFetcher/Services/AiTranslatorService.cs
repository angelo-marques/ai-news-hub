using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DataFetcher.Models;
using Markdig;
using Shared.Models;

namespace DataFetcher.Services;

public class AiTranslatorService
{
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    private readonly List<ApiProvider> _providers;
    private int _currentProviderIndex = 0;

    public AiTranslatorService(HttpClient http, string groqApiKey)
    {
        _http = http;
        _providers = AiProviderRegistry.Build(groqApiKey);
    }

    public async Task<Article?> TranslateArxivAsync(ArxivPaper paper)
    {
        var authorsStr = FormatAuthors(paper.Authors);

        var prompt = TranslationPromptBuilder.Build(
            title:     paper.Title,
            authors:   authorsStr,
            abstract_: paper.Abstract,
            source:    "arXiv",
            sourceUrl: paper.PaperUrl,
            extra:     $"Categorias arXiv: {string.Join(", ", paper.Categories.Take(4))}"
        );

        var result = await CallApiWithFailoverAsync(prompt);
        if (result is null) return null;

        return new Article
        {
            Id                 = $"arxiv:{paper.Id}",
            Title              = paper.Title,
            TitlePt            = result.TitlePt,
            NewsText           = result.NewsText,
            NewsHtml           = Markdown.ToHtml(result.NewsText, MarkdownPipeline),
            Authors            = paper.Authors,
            Source             = "arXiv",
            SourceUrl          = "https://arxiv.org/list/cs.AI/recent",
            PaperUrl           = paper.PaperUrl,
            PublishedAt        = paper.PublishedAt,
            FetchedAt          = DateTime.UtcNow,
            Tags               = result.Tags,
            TranslatorProvider = result.Provider
        };
    }

    public async Task<Article?> TranslateSemanticScholarAsync(SemanticScholarPaper paper)
    {
        if (string.IsNullOrWhiteSpace(paper.Abstract)) return null;

        var authors    = paper.Authors.Select(a => a.Name).ToArray();
        var paperUrl   = paper.Url ?? $"https://www.semanticscholar.org/paper/{paper.PaperId}";
        var publishedAt = ParseDate(paper.PublicationDate);

        var prompt = TranslationPromptBuilder.Build(
            title:     paper.Title,
            authors:   FormatAuthors(authors),
            abstract_: paper.Abstract,
            source:    "Semantic Scholar",
            sourceUrl: paperUrl,
            extra:     paper.CitationCount > 0 ? $"Citações: {paper.CitationCount}" : ""
        );

        var result = await CallApiWithFailoverAsync(prompt);
        if (result is null) return null;

        return new Article
        {
            Id                 = $"ss:{paper.PaperId}",
            Title              = paper.Title,
            TitlePt            = result.TitlePt,
            NewsText           = result.NewsText,
            NewsHtml           = Markdown.ToHtml(result.NewsText, MarkdownPipeline),
            Authors            = authors,
            Source             = "Semantic Scholar",
            SourceUrl          = "https://www.semanticscholar.org",
            PaperUrl           = paperUrl,
            PublishedAt        = publishedAt,
            FetchedAt          = DateTime.UtcNow,
            Tags               = result.Tags,
            TranslatorProvider = result.Provider
        };
    }

    public async Task<Article?> TranslateHfAsync(HuggingFacePaper paper)
    {
        if (string.IsNullOrWhiteSpace(paper.Summary)) return null;

        var authors  = paper.Authors.Select(a => a.Name).ToArray();
        var paperUrl = $"https://huggingface.co/papers/{paper.Id}";

        var prompt = TranslationPromptBuilder.Build(
            title:     paper.Title,
            authors:   FormatAuthors(authors),
            abstract_: paper.Summary,
            source:    "Hugging Face",
            sourceUrl: paperUrl,
            extra:     paper.Upvotes > 0 ? $"Upvotes na comunidade: {paper.Upvotes}" : ""
        );

        var result = await CallApiWithFailoverAsync(prompt);
        if (result is null) return null;

        return new Article
        {
            Id                 = $"hf:{paper.Id}",
            Title              = paper.Title,
            TitlePt            = result.TitlePt,
            NewsText           = result.NewsText,
            NewsHtml           = Markdown.ToHtml(result.NewsText, MarkdownPipeline),
            Authors            = authors,
            Source             = "Hugging Face",
            SourceUrl          = "https://huggingface.co/papers",
            PaperUrl           = paperUrl,
            PublishedAt        = ParseDate(paper.PublishedAt),
            FetchedAt          = DateTime.UtcNow,
            Tags               = result.Tags,
            TranslatorProvider = result.Provider
        };
    }

    public async Task<Article?> TranslatePwcAsync(PwcPaper paper)
    {
        if (string.IsNullOrWhiteSpace(paper.Abstract)) return null;

        var paperUrl = !string.IsNullOrEmpty(paper.UrlAbs) && paper.UrlAbs.StartsWith("http")
            ? paper.UrlAbs
            : $"https://paperswithcode.com/paper/{paper.Id}";

        var prompt = TranslationPromptBuilder.Build(
            title:     paper.Title,
            authors:   FormatAuthors(paper.Authors),
            abstract_: paper.Abstract,
            source:    "Papers with Code",
            sourceUrl: paperUrl,
            extra:     paper.Proceeding is not null ? $"Conferência/Workshop: {paper.Proceeding}" : ""
        );

        var result = await CallApiWithFailoverAsync(prompt);
        if (result is null) return null;

        return new Article
        {
            Id                 = $"pwc:{paper.Id}",
            Title              = paper.Title,
            TitlePt            = result.TitlePt,
            NewsText           = result.NewsText,
            NewsHtml           = Markdown.ToHtml(result.NewsText, MarkdownPipeline),
            Authors            = paper.Authors,
            Source             = "Papers with Code",
            SourceUrl          = "https://paperswithcode.com",
            PaperUrl           = paperUrl,
            CodeUrl            = $"https://paperswithcode.com/paper/{paper.Id}#code",
            PublishedAt        = ParseDate(paper.Published),
            FetchedAt          = DateTime.UtcNow,
            Tags               = result.Tags,
            TranslatorProvider = result.Provider
        };
    }

    // -------------------------------------------------------------------------
    // Failover engine
    // -------------------------------------------------------------------------

    private async Task<TranslationResult?> CallApiWithFailoverAsync(string prompt)
    {
        if (_providers.Count == 0)
        {
            Console.WriteLine("    [ERRO] Nenhum provedor de IA (API KEY) configurado. O artigo será pulado.");
            return null;
        }

        const int maxRetries  = 2;
        const int baseDelayMs = 2500;

        while (_currentProviderIndex < _providers.Count)
        {
            var provider = _providers[_currentProviderIndex];
            Console.WriteLine($"      ⚡ [{provider.Name}] Processando com modelo {provider.Model}...");

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                var result = await TryExecuteRequestAsync(prompt, provider);
                if (result != null)
                {
                    result.Provider = provider.Name;
                    return result;
                }

                if (attempt < maxRetries)
                {
                    int delay = baseDelayMs * attempt;
                    Console.WriteLine($"      ⏳ [{provider.Name}] Falha na tentativa {attempt}/{maxRetries}. Aguardando {delay}ms...");
                    await Task.Delay(delay);
                }
            }

            Console.WriteLine($"      ❌ [{provider.Name}] Limite esgotado. Acionando MODO FALLBACK (ignorado nesta rodada)...");
            _currentProviderIndex++;
        }

        Console.WriteLine("    [ERRO CRÍTICO] Todos os provedores tentados falharam.");
        return null;
    }

    private async Task<TranslationResult?> TryExecuteRequestAsync(string prompt, ApiProvider provider)
    {
        var requestBody = new
        {
            model      = provider.Model,
            max_tokens = 1800,
            messages   = new[] { new { role = "user", content = prompt } }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, provider.Url);
        req.Headers.Add("Authorization", $"Bearer {provider.Key}");
        req.Headers.Add("HTTP-Referer", "https://hubiasbrasil.dev");
        req.Headers.Add("X-Title", "Hub IA Brasil Crawler");
        req.Content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json"
        );

        try
        {
            using var resp = await _http.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                Console.WriteLine($"        [-ERRO-] Status {resp.StatusCode}: {body[..Math.Min(180, body.Length)]}");
                return null;
            }

            var apiResp = JsonSerializer.Deserialize<AiApiResponse>(body, JsonOptions);
            var text    = apiResp?.Choices?.FirstOrDefault()?.Message?.Content;

            if (string.IsNullOrWhiteSpace(text)) return null;

            var json = text.Trim();
            if (json.StartsWith("```"))
            {
                var lines = json.Split('\n');
                json = string.Join('\n', lines.Skip(1).SkipLast(1));
            }

            return JsonSerializer.Deserialize<TranslationResult>(json.Trim(), JsonOptions);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"        [-EX-] {ex.Message}");
            return null;
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static string FormatAuthors(string[] authors) =>
        authors.Length > 3
            ? $"{string.Join(", ", authors.Take(3))} et al."
            : string.Join(", ", authors);

    private static DateTime ParseDate(string? value) =>
        !string.IsNullOrEmpty(value) && DateTime.TryParse(value, out var dt) ? dt : DateTime.UtcNow;
}
