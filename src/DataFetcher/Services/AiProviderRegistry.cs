namespace DataFetcher.Services;

/// <summary>
/// Representa um provedor de API de IA compatível com o formato OpenAI Chat Completions.
/// </summary>
public readonly record struct ApiProvider(string Name, string Url, string Key, string Model);

/// <summary>
/// Registra e configura os provedores de IA disponíveis a partir de variáveis de ambiente.
/// Ordem de prioridade: Groq → OpenRouter → DeepSeek → TogetherAI → OpenAI → CustomFallback
/// </summary>
public static class AiProviderRegistry
{
    public static List<ApiProvider> Build(string groqApiKey)
    {
        var providers = new List<ApiProvider>();

        // 1. Groq (primário)
        if (!string.IsNullOrWhiteSpace(groqApiKey))
            providers.Add(new ApiProvider(
                Name:  "Groq",
                Url:   "https://api.groq.com/openai/v1/chat/completions",
                Key:   groqApiKey,
                Model: "llama-3.1-8b-instant"
            ));

        // 2. OpenRouter
        TryAdd(providers,
            name:         "OpenRouter",
            keyEnv:       "OPENROUTER_API_KEY",
            urlOrEnv:     "https://openrouter.ai/api/v1/chat/completions",
            modelEnv:     "OPENROUTER_MODEL",
            defaultModel: "google/gemini-flash-1.5"
        );

        // 3. DeepSeek
        TryAdd(providers,
            name:         "DeepSeek",
            keyEnv:       "DEEPSEEK_API_KEY",
            urlOrEnv:     "https://api.deepseek.com/chat/completions",
            modelEnv:     "DEEPSEEK_MODEL",
            defaultModel: "deepseek-chat"
        );

        // 4. TogetherAI
        TryAdd(providers,
            name:         "TogetherAI",
            keyEnv:       "TOGETHER_API_KEY",
            urlOrEnv:     "https://api.together.xyz/v1/chat/completions",
            modelEnv:     "TOGETHER_MODEL",
            defaultModel: "meta-llama/Meta-Llama-3.1-8B-Instruct-Turbo"
        );

        // 5. OpenAI
        TryAdd(providers,
            name:         "OpenAI",
            keyEnv:       "OPENAI_API_KEY",
            urlOrEnv:     "https://api.openai.com/v1/chat/completions",
            modelEnv:     "OPENAI_MODEL",
            defaultModel: "gpt-4o-mini"
        );

        // 6. CustomFallback — qualquer backend compatível (Ollama, SambaNova, etc.)
        TryAdd(providers,
            name:         "CustomFallback",
            keyEnv:       "FALLBACK_API_KEY",
            urlOrEnv:     "FALLBACK_API_URL",       // lido como variável de ambiente
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
