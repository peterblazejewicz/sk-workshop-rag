#pragma warning disable SKEXP0001, SKEXP0010, SKEXP0011, SKEXP0020, SKEXP0028, SKEXP0052

static string Env(string name, string fallback)
{
    var v = Environment.GetEnvironmentVariable(name);
    return string.IsNullOrWhiteSpace(v) ? fallback : v;
}

// Defaults and env overrides
var llmBaseUrl = Env("RAG__LLM_BASE_URL", "http://127.0.0.1:1234/v1");
var chatModel = Env("RAG__CHAT_MODEL", "meta-llama-3.1-8b-instruct");
var embedModel = Env("RAG__EMBED_MODEL", "text-embedding-nomic-embed-text-v2");
var apiKey = Env("RAG__LLM_API_KEY", "nokey");
var chromaUrl = Env("RAG__CHROMA_URL", "http://localhost:8000");
var collection = Env("RAG__COLLECTION", "my-document-collection");
var embedBaseUrl = Env("RAG__EMBED_BASE_URL", llmBaseUrl);

// Build kernel with local OpenAI-compatible endpoints
var builder = Kernel.CreateBuilder();
var http = new HttpClient { BaseAddress = new Uri(llmBaseUrl) };
var httpEmb = ReferenceEquals(embedBaseUrl, llmBaseUrl) ? http : new HttpClient { BaseAddress = new Uri(embedBaseUrl) };

builder.AddOpenAIChatCompletion(
    modelId: chatModel,
    apiKey: apiKey,
    httpClient: http);

builder.AddOpenAIEmbeddingGenerator(
    modelId: embedModel,
    apiKey: apiKey,
    httpClient: httpEmb);
// Legacy embedding generator (fallback)
builder.AddOpenAITextEmbeddingGeneration(
    modelId: embedModel,
    apiKey: apiKey,
    httpClient: httpEmb);

var kernel = builder.Build();

// Configure Chroma memory store and semantic memory
var chroma = new Microsoft.SemanticKernel.Connectors.Chroma.ChromaMemoryStore(chromaUrl);
ISemanticTextMemory memory;
var useLegacy = Env("RAG__EMBED_USE_LEGACY", "false").Equals("true", StringComparison.OrdinalIgnoreCase);
var useCustom = Env("RAG__EMBED_USE_CUSTOM", "true").Equals("true", StringComparison.OrdinalIgnoreCase);
if (useCustom)
{
    var custom = new RAGWorkshop.LmStudioEmbeddingGenerator(httpEmb, embedModel);
    memory = new SemanticTextMemory(chroma, embeddingGenerator: custom);
}
else if (useLegacy)
{
    var legacy = kernel.GetRequiredService<ITextEmbeddingGenerationService>();
    memory = new SemanticTextMemory(chroma, legacy);
}
else
{
    var embedder = kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
    memory = new SemanticTextMemory(chroma, embeddingGenerator: embedder);
}

Console.WriteLine("Semantic Kernel configured.");
Console.WriteLine($"- LLM base: {llmBaseUrl}");
Console.WriteLine($"- Chat model: {chatModel}");
Console.WriteLine($"- Embed model: {embedModel}");
Console.WriteLine($"- Chroma: {chromaUrl}");
Console.WriteLine($"- Collection: {collection}");

// CLI flags
static string? GetArg(string[] args, string key)
{
    for (var i = 0; i < args.Length; i++)
    {
        var a = args[i];
        if (a.Equals(key, StringComparison.OrdinalIgnoreCase))
        {
            return i + 1 < args.Length ? args[i + 1] : null;
        }

        if (a.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
        {
            return a[(key.Length + 1)..];
        }
    }

    return null;
}

static void PrintUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run -- [--ingest <path>] [--collection <name>] [--help]");
    Console.WriteLine("    --ingest: Path to a PDF file or a directory containing PDFs (recursive).");
    Console.WriteLine("    --collection: Override collection name (default: my-document-collection).");
}

if (args.Any(a => a.Equals("--help", StringComparison.OrdinalIgnoreCase)))
{
    PrintUsage();
    return;
}

var ingestPath = GetArg(args, "--ingest");
var overrideCollection = GetArg(args, "--collection");
if (!string.IsNullOrWhiteSpace(overrideCollection))
{
    collection = overrideCollection!;
}

var ingestOnly = args.Any(a => a.Equals("--ingest-only", StringComparison.OrdinalIgnoreCase));

if (!string.IsNullOrWhiteSpace(ingestPath))
{
    // Ingest either a single PDF or all PDFs from a directory (recursive)
    IEnumerable<string> pdfs;
    if (Directory.Exists(ingestPath))
    {
        pdfs = Directory.EnumerateFiles(ingestPath, "*.pdf", SearchOption.AllDirectories);
    }
    else if (File.Exists(ingestPath) && Path.GetExtension(ingestPath).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
    {
        pdfs = [ingestPath];
    }
    else
    {
        Console.WriteLine($"Invalid --ingest path: {ingestPath}");
        PrintUsage();
        return;
    }

    Console.WriteLine($"Ingesting {pdfs.Count()} file(s) into collection '{collection}'...");
    foreach (var pdf in pdfs)
    {
        await RAGWorkshop.DataIngestion.IngestPdfAsync(pdf, memory, collection);
        Console.WriteLine($"  - Ingested: {pdf}");
    }

    if (ingestOnly)
    {
        Console.WriteLine("Ingestion complete. Exiting (--ingest-only).\n");
        return;
    }

    Console.WriteLine("Ingestion complete. Entering chat mode.\n");
}

// Chat loop (retrieval + augmentation + generation)
var chat = kernel.GetRequiredService<IChatCompletionService>();
var history = new ChatHistory();
history.AddSystemMessage("You are a helpful AI assistant answering questions based on the provided context.");

Console.WriteLine("Type your question (or just press Enter to exit).");
while (true)
{
    Console.Write("User > ");
    var userInput = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(userInput))
    {
        break;
    }

    var context = new StringBuilder();
    var results = memory.SearchAsync(collection, userInput, limit: 3, minRelevanceScore: 0.75);
    await foreach (var item in results)
    {
        _ = context.AppendLine(item.Metadata.Text);
    }

    var userMessage = $"""
Context:
---
{context}
---

Question: {userInput}
""";

    history.AddUserMessage(userMessage);

    var response = await chat.GetChatMessageContentAsync(history, kernel: kernel);
    Console.WriteLine($"AI > {response.Content}\n");

    history.AddMessage(response.Role, response.Content ?? string.Empty);
}
