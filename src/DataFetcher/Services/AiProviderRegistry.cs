namespace DataFetcher.Services;

/// <summary>
/// Representa um provedor de API de IA compatível com o formato OpenAI Chat Completions.
/// </summary>
public readonly record struct ApiProvider(string Name, string Url, string Key, string Model);

/// <summary>
/// Registra e configura os provedores de IA disponíveis a partir de variáveis de ambiente.
/// Ordem de prioridade:
///   Groq → Cerebras → Mistral → OpenRouter → DeepSeek → TogetherAI → Gemini → OpenAI → CustomFallback
///
/// Novos provedores gratuitos adicionados:
///   - Cerebras  (cloud.cerebras.ai)   : inferência ultra-rápida, Llama 3.3 70B grátis
///   - Mistral   (api.mistral.ai)      : tier "Experiment" gratuito, modelos europeus de alta qualidade
///   - Gemini    (AI Studio)           : tier mais generoso do mercado, Gemini Flash gratuito
/// </summary>
public static class AiProviderRegistry
{
    public static List<ApiProvider> Build(string groqApiKey)
    {
        var providers = new List<ApiProvider>();

        // 1. Groq — Velocidade extrema e Llama 3.3 70B
        if (!string.IsNullOrWhiteSpace(groqApiKey))
            providers.Add(new ApiProvider(
                Name:  "Groq",
                Url:   "https://api.groq.com/openai/v1/chat/completions",
                Key:   groqApiKey,
                Model: "llama-3.3-70b-versatile"
            ));

        // 2. Cerebras — Inferência ultra-rápida
        TryAdd(providers,
            name:         "Cerebras",
            keyEnv:       "CEREBRAS_API_KEY",
            urlOrEnv:     "https://api.cerebras.ai/v1/chat/completions",
            modelEnv:     "CEREBRAS_MODEL",
            defaultModel: "llama-3.3-70b"
        );

        // 3. Cloudflare Workers AI — (10k tokens/dia grátis)
        //    Url: https://api.cloudflare.com/client/v4/accounts/{ACCOUNT_ID}/ai/v1/chat/completions
        TryAdd(providers,
            name:         "Cloudflare",
            keyEnv:       "CLOUDFLARE_API_TOKEN",
            urlOrEnv:     "CLOUDFLARE_API_URL", // lido do secret
            modelEnv:     "CLOUDFLARE_MODEL",
            defaultModel: "@cf/meta/llama-3.1-8b-instruct"
        );

        // 4. SiliconFlow — Ótimo para DeepSeek e Qwen (Cadastro: siliconflow.cn)
        TryAdd(providers,
            name:         "SiliconFlow",
            keyEnv:       "SILICONFLOW_API_KEY",
            urlOrEnv:     "https://api.siliconflow.cn/v1/chat/completions",
            modelEnv:     "SILICONFLOW_MODEL",
            defaultModel: "deepseek-ai/DeepSeek-V3"
        );

        // 5. Mistral AI — Tier "Experiment"
        TryAdd(providers,
            name:         "Mistral",
            keyEnv:       "MISTRAL_API_KEY",
            urlOrEnv:     "https://api.mistral.ai/v1/chat/completions",
            modelEnv:     "MISTRAL_MODEL",
            defaultModel: "mistral-small-latest"
        );

        // 6. OpenRouter — Agregador
        TryAdd(providers,
            name:         "OpenRouter",
            keyEnv:       "OPENROUTER_API_KEY",
            urlOrEnv:     "https://openrouter.ai/api/v1/chat/completions",
            modelEnv:     "OPENROUTER_MODEL",
            defaultModel: "google/gemini-flash-1.5"
        );

        // 7. DeepSeek
        TryAdd(providers,
            name:         "DeepSeek",
            keyEnv:       "DEEPSEEK_API_KEY",
            urlOrEnv:     "https://api.deepseek.com/chat/completions",
            modelEnv:     "DEEPSEEK_MODEL",
            defaultModel: "deepseek-chat"
        );

        // 8. TogetherAI
        TryAdd(providers,
            name:         "TogetherAI",
            keyEnv:       "TOGETHER_API_KEY",
            urlOrEnv:     "https://api.together.xyz/v1/chat/completions",
            modelEnv:     "TOGETHER_MODEL",
            defaultModel: "meta-llama/Meta-Llama-3.1-8B-Instruct-Turbo"
        );

        // 9. Google Gemini — via AI Studio
        TryAdd(providers,
            name:         "Gemini",
            keyEnv:       "GEMINI_API_KEY",
            urlOrEnv:     "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions",
            modelEnv:     "GEMINI_MODEL",
            defaultModel: "gemini-2.0-flash"
        );

        // 10. OpenAI
        TryAdd(providers,
            name:         "OpenAI",
            keyEnv:       "OPENAI_API_KEY",
            urlOrEnv:     "https://api.openai.com/v1/chat/completions",
            modelEnv:     "OPENAI_MODEL",
            defaultModel: "gpt-4o-mini"
        );

        // 11. CustomFallback
        TryAdd(providers,
            name:         "CustomFallback",
            keyEnv:       "FALLBACK_API_KEY",
            urlOrEnv:     "FALLBACK_API_URL",
            modelEnv:     "FALLBACK_MODEL",
            defaultModel: "google/gemini-flash-1.5"
        );

        return providers;
    }

    private static void TryAdd(List<ApiProvider> providers, string name, string keyEnv, string urlOrEnv, string modelEnv, string defaultModel)
    {
        var key = Environment.GetEnvironmentVariable(keyEnv);
        if (string.IsNullOrWhiteSpace(key)) return;

        var url = urlOrEnv.StartsWith("http")
            ? urlOrEnv
            : Environment.GetEnvironmentVariable(urlOrEnv);
        if (string.IsNullOrWhiteSpace(url)) return;

        var model = Environment.GetEnvironmentVariable(modelEnv);
        if (string.IsNullOrWhiteSpace(model)) model = defaultModel;

        providers.Add(new ApiProvider(name, url, key, model));
    }
}
