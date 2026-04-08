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

    // Limite de caracteres do abstract para evitar erros 400 de context too long
    private const int MaxAbstractLength = 4000;

    public AiTranslatorService(HttpClient http, string groqApiKey)
    {
        _http = http;
        _providers = AiProviderRegistry.Build(groqApiKey);

        Console.WriteLine($"  🤖 Provedores de IA carregados ({_providers.Count}):");
        foreach (var p in _providers)
            Console.WriteLine($"     [{p.Name}] modelo: {p.Model}");
        Console.WriteLine();
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
                var (result, skipProvider) = await TryExecuteRequestAsync(prompt, provider);

                if (result != null)
                {
                    result.Provider = provider.Name;
                    return result;
                }

                // 429 Rate Limit ou erro permanente: pula direto pro próximo provider sem esperar
                if (skipProvider)
                {
                    Console.WriteLine($"      ⏭️  [{provider.Name}] Erro permanente/rate-limit — pulando para o próximo provider.");
                    break;
                }

                if (attempt < maxRetries)
                {
                    int delay = baseDelayMs * attempt;
                    Console.WriteLine($"      ⏳ [{provider.Name}] Falha na tentativa {attempt}/{maxRetries}. Aguardando {delay}ms...");
                    await Task.Delay(delay);
                }
            }

            Console.WriteLine($"      ❌ [{provider.Name}] Esgotado — passando para o próximo...");
            _currentProviderIndex++;
        }

        // Todos os providers foram esgotados — todos os artigos seguintes serão pulados nesta rodada.
        Console.WriteLine("    [ERRO CRÍTICO] Todos os provedores falharam. Os artigos restantes desta rodada serão ignorados.");
        return null;
    }

    /// <returns>(result, skipProvider) — skipProvider=true indica erro permanente/rate-limit; não vale retrying.</returns>
    private async Task<(TranslationResult? result, bool skipProvider)> TryExecuteRequestAsync(string prompt, ApiProvider provider)
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
                var preview = body.Length > 300 ? body[..300] + "…" : body;
                Console.WriteLine($"        [-ERRO HTTP {(int)resp.StatusCode}] {preview}");

                // 429 = rate limit, 401/403 = auth, 400 = bad request → não vale retrying
                bool permanent = resp.StatusCode is
                    System.Net.HttpStatusCode.TooManyRequests or
                    System.Net.HttpStatusCode.Unauthorized or
                    System.Net.HttpStatusCode.Forbidden or
                    System.Net.HttpStatusCode.BadRequest;

                return (null, permanent);
            }

            var apiResp = JsonSerializer.Deserialize<AiApiResponse>(body, JsonOptions);
            var text    = apiResp?.Choices?.FirstOrDefault()?.Message?.Content;

            if (string.IsNullOrWhiteSpace(text))
            {
                Console.WriteLine($"        [-ERRO] Resposta vazia ou sem 'choices'. Body: {body[..Math.Min(200, body.Length)]}");
                return (null, false);
            }

            var json = text.Trim();
            if (json.StartsWith("```"))
            {
                var lines = json.Split('\n');
                json = string.Join('\n', lines.Skip(1).SkipLast(1));
            }

            var parsed = JsonSerializer.Deserialize<TranslationResult>(json.Trim(), JsonOptions);
            if (parsed is null)
            {
                Console.WriteLine($"        [-ERRO] JSON inválido retornado pelo modelo. Conteúdo: {text[..Math.Min(300, text.Length)]}");
                return (null, false);
            }

            return (parsed, false);
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine($"        [-TIMEOUT] {provider.Name} não respondeu dentro do limite de {_http.Timeout.TotalSeconds}s");
            return (null, false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"        [-EX-] {ex.GetType().Name}: {ex.Message}");
            return (null, false);
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
