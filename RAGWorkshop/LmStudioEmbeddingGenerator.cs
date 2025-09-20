namespace RAGWorkshop;

public sealed class LmStudioEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    private readonly HttpClient _http;
    private readonly string _model;

    public LmStudioEmbeddingGenerator(HttpClient http, string model)
    {
        _http = http;
        _model = model;
    }

public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        var inputs = values.ToArray();
        var req = new EmbeddingsRequest { model = _model, input = inputs };
using var message = new HttpRequestMessage(HttpMethod.Post, "embeddings")
        {
            Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(req), System.Text.Encoding.UTF8, "application/json"),
        };

        using var resp = await _http.SendAsync(message, cancellationToken).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("data", out var data))
        {
            throw new InvalidOperationException($"Unexpected embeddings response (no 'data' property). First 500 chars: {json.Substring(0, Math.Min(json.Length, 500))}");
        }

        var list = new List<Embedding<float>>(data.GetArrayLength());
        foreach (var item in data.EnumerateArray())
        {
            var embArr = item.GetProperty("embedding");
            var floats = new float[embArr.GetArrayLength()];
            var idx = 0;
            foreach (var num in embArr.EnumerateArray())
            {
                floats[idx++] = (float)num.GetDouble();
            }
            list.Add(new Embedding<float>(floats));
        }

        return new GeneratedEmbeddings<Embedding<float>>(list);
    }

    private sealed class EmbeddingsRequest
    {
        public string model { get; set; } = string.Empty;
        public string[] input { get; set; } = Array.Empty<string>();
    }
    public object? GetService(Type serviceType, object? serviceKey) => null;

    public void Dispose()
    {
        // HttpClient lifecycle managed by caller; do not dispose.
    }
}
