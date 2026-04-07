# ◆ Hub IA Brasil

> Curadoria automática de pesquisas em inteligência artificial, traduzidas para o **português do Brasil**, publicadas como site estático no GitHub Pages via GitHub Actions.

**Fontes:** [arXiv cs.AI/LG/CL/CV](https://arxiv.org/list/cs.AI/recent) · [Papers with Code](https://paperswithcode.com)  
**Redação:** Angelo Marques de Oliveira Silva  
**Stack:** .NET 10 · Blazor WebAssembly · GitHub Actions · Anthropic Claude API

---

## Arquitetura

```
ai-news-hub/
├── .github/workflows/
│   └── publish.yml          # Workflow: coleta + tradução + deploy (3×/dia)
├── data/
│   ├── articles.json        # Artigos persistidos (últimos 30 dias)
│   └── seen-ids.json        # IDs processados (deduplicação)
├── src/
│   ├── Shared/              # Modelos compartilhados (Article, SeenIds)
│   ├── DataFetcher/         # Console app .NET 10 — pipeline de coleta
│   │   ├── Services/
│   │   │   ├── ArxivService.cs          # Lê Atom feed do arXiv
│   │   │   ├── PapersWithCodeService.cs # Lê API REST do PwC
│   │   │   ├── ClaudeTranslatorService.cs # Traduz via Anthropic API
│   │   │   └── ArticleStorageService.cs   # Lê/salva JSON
│   │   └── Program.cs
│   └── WebApp/              # Blazor WASM .NET 10 — site estático
│       ├── Pages/Index.razor
│       ├── Services/ArticleService.cs
│       └── wwwroot/
│           ├── css/app.css  # Tema editorial escuro
│           └── data/        # articles.json gerado no CI
└── README.md
```

### Fluxo por execução

```
GitHub Actions (cron 3×/dia)
  │
  ├─ DataFetcher executa
  │   ├─ arXiv API → 30 papers recentes (cs.AI/LG/CL/CV)
  │   │   └─ filtra já vistos → até 3 novos → Claude API → traduz → salva
  │   └─ Papers with Code API → 20 papers recentes
  │       └─ filtra já vistos → até 3 novos → Claude API → traduz → salva
  │
  ├─ data/articles.json atualizado e commitado no repo
  │
  └─ Blazor WASM buildado e publicado no GitHub Pages
```

---

## Setup em 5 passos

### 1. Fork / Clone

```bash
git clone https://github.com/SEU_USUARIO/ai-news-hub.git
cd ai-news-hub
```

### 2. Adicionar o secret da API

No GitHub: **Settings → Secrets and variables → Actions → New repository secret**

| Nome | Valor |
|------|-------|
| `ANTHROPIC_API_KEY` | `sk-ant-...` |

> Gere sua chave em [console.anthropic.com](https://console.anthropic.com)

### 3. Habilitar GitHub Pages

**Settings → Pages → Source → GitHub Actions**

### 4. Primeiro deploy manual

**Actions → 🤖 Coletar Notícias IA & Publicar → Run workflow**

O site estará disponível em: `https://SEU_USUARIO.github.io/ai-news-hub/`

### 5. Agendamento automático

O workflow já está configurado para rodar automaticamente:

| Horário BRT | Cron (UTC) |
|-------------|------------|
| 07:00 manhã | `0 10 * * *` |
| 14:00 tarde | `0 17 * * *` |
| 21:00 noite | `0 0 * * *`  |

---

## Desenvolvimento local

### DataFetcher

```bash
# Rodar a partir da raiz do repositório
export ANTHROPIC_API_KEY="sk-ant-..."
dotnet run --project src/DataFetcher/DataFetcher.csproj

# Os artigos serão salvos em data/articles.json
```

### WebApp (Blazor WASM)

```bash
# Copiar artigos para wwwroot antes de rodar
cp data/articles.json src/WebApp/wwwroot/data/articles.json

# Iniciar dev server
dotnet run --project src/WebApp/WebApp.csproj
# → http://localhost:5000
```

---

## Configurações

### Limites por execução

Edite `src/DataFetcher/Program.cs`:

```csharp
const int MaxPerSource = 3; // artigos novos por fonte (3 arXiv + 3 PwC = 6/run)
```

### Retenção de artigos

Por padrão, artigos são mantidos por **30 dias**:

```csharp
var cutoff = DateTime.UtcNow.AddDays(-30);
```

### Fontes do arXiv

Edite `src/DataFetcher/Services/ArxivService.cs`:

```csharp
private const string Categories = "cat:cs.AI+OR+cat:cs.LG+OR+cat:cs.CL+OR+cat:cs.CV";
```

| Código | Área |
|--------|------|
| `cs.AI` | Artificial Intelligence |
| `cs.LG` | Machine Learning |
| `cs.CL` | Computation and Language (NLP) |
| `cs.CV` | Computer Vision |
| `cs.RO` | Robotics |
| `cs.IR` | Information Retrieval |

---

## Custo estimado

Com `MaxPerSource = 3` e 3 execuções/dia → **até 18 artigos/dia** (se todos forem novos)

| Modelo | Tokens/artigo (est.) | Custo/artigo | Custo/dia (máx) |
|--------|---------------------|--------------|-----------------|
| claude-sonnet-4-6 | ~2.000 input + ~1.000 output | ~$0.006 | ~$0.11 |

---

## Tech Stack

| Camada | Tecnologia |
|--------|-----------|
| Coleta de dados | .NET 10 Console App |
| Feed arXiv | Atom XML via `System.Xml.Linq` |
| API Papers with Code | REST JSON |
| Tradução | Anthropic Claude API (`claude-sonnet-4-6`) |
| Markdown → HTML | [Markdig](https://github.com/xoofx/markdig) |
| Frontend | Blazor WebAssembly .NET 10 |
| Deploy | GitHub Actions + GitHub Pages |
| Deduplicação | HashSet persistido em `seen-ids.json` |

---

## Licença

MIT — fique à vontade para adaptar e distribuir.

---

*Hub IA Brasil · Redação: Angelo Marques de Oliveira Silva*
