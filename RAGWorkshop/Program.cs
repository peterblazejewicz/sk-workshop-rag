#pragma warning disable SKEXP0001, SKEXP0010, SKEXP0011, SKEXP0020, SKEXP0028, SKEXP0052
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Chroma;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Memory;
using System.Net.Http;

static string Env(string name, string fallback)
{
    var v = Environment.GetEnvironmentVariable(name);
    return string.IsNullOrWhiteSpace(v) ? fallback : v;
}

// Defaults and env overrides
var llmBaseUrl = Env("RAG__LLM_BASE_URL", "http://localhost:1234");
var chatModel = Env("RAG__CHAT_MODEL", "local-llm");
var embedModel = Env("RAG__EMBED_MODEL", "local-embedding-model");
var apiKey = Env("RAG__LLM_API_KEY", "nokey");
var chromaUrl = Env("RAG__CHROMA_URL", "http://localhost:8000");
var collection = Env("RAG__COLLECTION", "my-document-collection");

// Build kernel with local OpenAI-compatible endpoints
var builder = Kernel.CreateBuilder();
var http = new HttpClient { BaseAddress = new Uri(llmBaseUrl) };

builder.AddOpenAIChatCompletion(
    modelId: chatModel,
    apiKey: apiKey,
    httpClient: http);

builder.AddOpenAITextEmbeddingGeneration(
    modelId: embedModel,
    apiKey: apiKey,
    httpClient: http);

var kernel = builder.Build();

// Configure Chroma memory store and semantic memory
var chroma = new ChromaMemoryStore(chromaUrl);
var embedder = kernel.GetRequiredService<ITextEmbeddingGenerationService>();
var memory = new SemanticTextMemory(chroma, embedder);

Console.WriteLine("Semantic Kernel configured.");
Console.WriteLine($"- LLM base: {llmBaseUrl}");
Console.WriteLine($"- Chat model: {chatModel}");
Console.WriteLine($"- Embed model: {embedModel}");
Console.WriteLine($"- Chroma: {chromaUrl}");
Console.WriteLine($"- Collection: {collection}");

// Placeholder until ingestion/chat are implemented
Console.WriteLine("RAG Workshop scaffold ready (kernel + memory wired).");
