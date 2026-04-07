using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DataFetcher.Models;
using Markdig;
using Shared.Models;

namespace DataFetcher.Services;

public class GroqTranslatorService(HttpClient http, string apiKey)
{
    private const string ApiUrl = "https://api.groq.com/openai/v1/chat/completions";
    private const string Model = "llama-3.1-8b-instant";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public async Task<Article?> TranslateArxivAsync(ArxivPaper paper)
    {
        var authorsStr = paper.Authors.Length > 3
            ? $"{string.Join(", ", paper.Authors.Take(3))} et al."
            : string.Join(", ", paper.Authors);

        var prompt = BuildPrompt(
            title: paper.Title,
            authors: authorsStr,
            abstract_: paper.Abstract,
            source: "arXiv",
            sourceUrl: paper.PaperUrl,
            extra: $"Categorias arXiv: {string.Join(", ", paper.Categories.Take(4))}"
        );

        var result = await CallGroqAsync(prompt);
        if (result is null) return null;

        var newsHtml = Markdown.ToHtml(result.NewsText, MarkdownPipeline);

        return new Article
        {
            Id = $"arxiv:{paper.Id}",
            Title = paper.Title,
            TitlePt = result.TitlePt,
            NewsText = result.NewsText,
            NewsHtml = newsHtml,
            Authors = paper.Authors,
            Source = "arXiv",
            SourceUrl = "https://arxiv.org/list/cs.AI/recent",
            PaperUrl = paper.PaperUrl,
            PublishedAt = paper.PublishedAt,
            FetchedAt = DateTime.UtcNow,
            Tags = result.Tags
        };
    }

    public async Task<Article?> TranslateSemanticScholarAsync(SemanticScholarPaper paper)
    {
        if (string.IsNullOrWhiteSpace(paper.Abstract)) return null;

        var authors = paper.Authors.Select(a => a.Name).ToArray();
        var authorsStr = authors.Length > 3
            ? $"{string.Join(", ", authors.Take(3))} et al."
            : string.Join(", ", authors);

        DateTime publishedAt = DateTime.UtcNow;
        if (!string.IsNullOrEmpty(paper.PublicationDate) && DateTime.TryParse(paper.PublicationDate, out var dt))
            publishedAt = dt;

        var paperUrl = paper.Url ?? $"https://www.semanticscholar.org/paper/{paper.PaperId}";
        var extra = paper.CitationCount > 0 ? $"Citações: {paper.CitationCount}" : "";

        var prompt = BuildPrompt(
            title: paper.Title,
            authors: authorsStr,
            abstract_: paper.Abstract,
            source: "Semantic Scholar",
            sourceUrl: paperUrl,
            extra: extra
        );

        var result = await CallGroqAsync(prompt);
        if (result is null) return null;

        var newsHtml = Markdown.ToHtml(result.NewsText, MarkdownPipeline);

        return new Article
        {
            Id = $"ss:{paper.PaperId}",
            Title = paper.Title,
            TitlePt = result.TitlePt,
            NewsText = result.NewsText,
            NewsHtml = newsHtml,
            Authors = authors,
            Source = "Semantic Scholar",
            SourceUrl = "https://www.semanticscholar.org",
            PaperUrl = paperUrl,
            PublishedAt = publishedAt,
            FetchedAt = DateTime.UtcNow,
            Tags = result.Tags
        };
    }

    public async Task<Article?> TranslateHfAsync(HuggingFacePaper paper)
    {
        if (string.IsNullOrWhiteSpace(paper.Summary)) return null;

        var authors = paper.Authors.Select(a => a.Name).ToArray();
        var authorsStr = authors.Length > 3
            ? $"{string.Join(", ", authors.Take(3))} et al."
            : string.Join(", ", authors);

        DateTime publishedAt = DateTime.UtcNow;
        if (!string.IsNullOrEmpty(paper.PublishedAt) && DateTime.TryParse(paper.PublishedAt, out var dt))
            publishedAt = dt;

        var paperUrl = $"https://huggingface.co/papers/{paper.Id}";

        var prompt = BuildPrompt(
            title: paper.Title,
            authors: authorsStr,
            abstract_: paper.Summary,
            source: "Hugging Face",
            sourceUrl: paperUrl,
            extra: paper.Upvotes > 0 ? $"Upvotes na comunidade: {paper.Upvotes}" : ""
        );

        var result = await CallGroqAsync(prompt);
        if (result is null) return null;

        var newsHtml = Markdown.ToHtml(result.NewsText, MarkdownPipeline);

        return new Article
        {
            Id = $"hf:{paper.Id}",
            Title = paper.Title,
            TitlePt = result.TitlePt,
            NewsText = result.NewsText,
            NewsHtml = newsHtml,
            Authors = authors,
            Source = "Hugging Face",
            SourceUrl = "https://huggingface.co/papers",
            PaperUrl = paperUrl,
            PublishedAt = publishedAt,
            FetchedAt = DateTime.UtcNow,
            Tags = result.Tags
        };
    }

    public async Task<Article?> TranslatePwcAsync(PwcPaper paper)
    {
        if (string.IsNullOrWhiteSpace(paper.Abstract)) return null;

        var authorsStr = paper.Authors.Length > 3
            ? $"{string.Join(", ", paper.Authors.Take(3))} et al."
            : string.Join(", ", paper.Authors);

        DateTime publishedAt = DateTime.UtcNow;
        if (!string.IsNullOrEmpty(paper.Published) && DateTime.TryParse(paper.Published, out var dt))
            publishedAt = dt;

        var extra = paper.Proceeding is not null ? $"Conferência/Workshop: {paper.Proceeding}" : "";

        var paperUrl = !string.IsNullOrEmpty(paper.UrlAbs) && paper.UrlAbs.StartsWith("http")
            ? paper.UrlAbs
            : $"https://paperswithcode.com/paper/{paper.Id}";

        var prompt = BuildPrompt(
            title: paper.Title,
            authors: authorsStr,
            abstract_: paper.Abstract,
            source: "Papers with Code",
            sourceUrl: paperUrl,
            extra: extra
        );

        var result = await CallGroqAsync(prompt);
        if (result is null) return null;

        var newsHtml = Markdown.ToHtml(result.NewsText, MarkdownPipeline);

        return new Article
        {
            Id = $"pwc:{paper.Id}",
            Title = paper.Title,
            TitlePt = result.TitlePt,
            NewsText = result.NewsText,
            NewsHtml = newsHtml,
            Authors = paper.Authors,
            Source = "Papers with Code",
            SourceUrl = "https://paperswithcode.com",
            PaperUrl = paperUrl,
            CodeUrl = $"https://paperswithcode.com/paper/{paper.Id}#code",
            PublishedAt = publishedAt,
            FetchedAt = DateTime.UtcNow,
            Tags = result.Tags
        };
    }

    private static string BuildPrompt(string title, string authors, string abstract_, string source, string sourceUrl, string extra)
    {
        return $$"""
Você é Angelo Marques de Oliveira Silva, redator especializado em tecnologia e inteligência artificial para o portal "Hub IA Brasil" (hubiasbrasil.dev).

Escreva uma notícia jornalística em **português do Brasil (PT-BR)** com base no paper científico abaixo.

## REGRAS OBRIGATÓRIAS
- Tom: profissional, técnico mas acessível ao público brasileiro de tecnologia
- Tamanho: entre 450 e 600 palavras
- Estrutura:
  1. **Lead** — 1 parágrafo forte e direto que revela a descoberta principal
  2. **Contexto e Metodologia** — 2 parágrafos sobre o problema abordado e como os pesquisadores o resolveram
  3. **Resultados** — 1 parágrafo com os números e benchmarks mais relevantes
  4. **Impacto e Aplicações** — 1 parágrafo sobre relevância prática para o ecossistema de IA e o mercado brasileiro
  5. **Perspectiva do Redator** — 1 parágrafo final com sua análise crítica (use primeira pessoa moderadamente)
- Traduza termos técnicos para PT-BR quando houver equivalente, mantendo o original em inglês entre parênteses na 1ª ocorrência
- Use **negrito** para destacar conceitos-chave na primeira aparição
- NÃO invente dados que não estejam no abstract
- Mencione os autores/instituições no texto
- Adicione ao final: `---\n*Fonte: {{source}} | [Acessar paper original]({{sourceUrl}})*`

## PAPER ORIGINAL
- **Título:** {{title}}
- **Autores:** {{authors}}
{{(string.IsNullOrEmpty(extra) ? "" : $"- **{extra}**\n")}}- **Abstract:**
{{abstract_}}

## RESPOSTA
Responda APENAS com JSON válido (sem markdown fence, sem texto antes ou depois):
{
  "titlePt": "título da notícia em português (máx 110 caracteres, impactante e jornalístico, sem aspas)",
  "newsText": "texto completo da notícia em markdown (450-600 palavras)",
  "tags": ["tag1", "tag2", "tag3", "tag4", "tag5"]
}

As tags devem ser palavras-chave em inglês ou PT-BR relevantes ao tema (ex: "deep learning", "transformers", "visão computacional", "NLP", "reinforcement learning", "multimodal", "benchmark", "fine-tuning", etc.).
""";
    }

    private readonly string? _fallbackApiUrl = Environment.GetEnvironmentVariable("FALLBACK_API_URL");
    private readonly string? _fallbackApiKey = Environment.GetEnvironmentVariable("FALLBACK_API_KEY");
    private readonly string _fallbackModel = Environment.GetEnvironmentVariable("FALLBACK_MODEL") ?? "google/gemini-flash-1.5";

    private async Task<TranslationResult?> CallGroqAsync(string prompt)
    {
        int maxRetries = 3;
        int baseDelayMs = 2000;

        // Tenta provedor primário com Exponential Backoff
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            var result = await TryCallApiAsync(prompt, ApiUrl, apiKey, Model);
            if (result != null) return result;

            if (attempt < maxRetries)
            {
                int delay = baseDelayMs * (int)Math.Pow(2, attempt - 1);
                Console.WriteLine($"    [AVISO] Primário falhou (Tentativa {attempt}/{maxRetries}). Aguardando {delay}ms...");
                await Task.Delay(delay);
            }
        }

        // Tenta Fallback se configurado
        if (!string.IsNullOrWhiteSpace(_fallbackApiKey) && !string.IsNullOrWhiteSpace(_fallbackApiUrl))
        {
            Console.WriteLine($"    [AVISO] Limite primário esgotado! Tentando transição para Fallback ({_fallbackApiUrl})...");
            var fallbackResult = await TryCallApiAsync(prompt, _fallbackApiUrl, _fallbackApiKey, _fallbackModel);
            if (fallbackResult != null) return fallbackResult;
        }

        return null;
    }

    private async Task<TranslationResult?> TryCallApiAsync(string prompt, string apiUrl, string key, string modelName)
    {
        var requestBody = new
        {
            model = modelName,
            max_tokens = 1800,
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, apiUrl);
        req.Headers.Add("Authorization", $"Bearer {key}");
        // OpenRouter optional headers support (won't affect Groq)
        req.Headers.Add("HTTP-Referer", "https://hubiasbrasil.dev");
        req.Headers.Add("X-Title", "Hub IA Brasil Crawler");
        req.Content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json"
        );

        try
        {
            using var resp = await http.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                Console.WriteLine($"    [ERRO API] [{new Uri(apiUrl).Host}] {resp.StatusCode}: {body[..Math.Min(300, body.Length)]}");
                return null;
            }

            var apiResp = JsonSerializer.Deserialize<GroqApiResponse>(body, JsonOptions);
            var text = apiResp?.Choices?.FirstOrDefault()?.Message?.Content;

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
            Console.WriteLine($"    [ERRO parse/network] {ex.Message}");
            return null;
        }
    }
}

// --- Internal DTOs ---

public record TranslationResult
{
    [JsonPropertyName("titlePt")]
    public string TitlePt { get; init; } = "";

    [JsonPropertyName("newsText")]
    public string NewsText { get; init; } = "";

    [JsonPropertyName("tags")]
    public string[] Tags { get; init; } = [];
}

public record GroqApiResponse
{
    [JsonPropertyName("choices")]
    public GroqChoice[] Choices { get; init; } = [];
}

public record GroqChoice
{
    [JsonPropertyName("message")]
    public GroqMessage? Message { get; init; }
}

public record GroqMessage
{
    [JsonPropertyName("content")]
    public string Content { get; init; } = "";
}
