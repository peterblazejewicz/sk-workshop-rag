#pragma warning disable SKEXP0001
using System.Text;
using Microsoft.SemanticKernel.Memory;
using UglyToad.PdfPig;

namespace RAGWorkshop;

public static class DataIngestion
{
    public static string ExtractTextFromPdf(string filePath)
    {
        var sb = new StringBuilder();
        using var doc = PdfDocument.Open(filePath);
        foreach (var page in doc.GetPages())
        {
            sb.AppendLine(page.Text);
        }
        return sb.ToString();
    }

    public static IEnumerable<string> ChunkText(string text, int chunkSize = 2000, int overlap = 200)
    {
        if (string.IsNullOrWhiteSpace(text)) yield break;
        var len = text.Length;
        var start = 0;
        if (chunkSize <= 0) chunkSize = 2000;
        if (overlap < 0) overlap = 0;

        while (start < len)
        {
            var end = Math.Min(start + chunkSize, len);
            var chunk = text.Substring(start, end - start);
            yield return chunk;
            if (end == len) yield break;
            start = Math.Max(0, end - overlap);
        }
    }

    public static async Task IngestPdfAsync(string filePath, ISemanticTextMemory memory, string collection)
    {
        var text = ExtractTextFromPdf(filePath);
        var i = 0;
        foreach (var chunk in ChunkText(text))
        {
            var id = $"{Path.GetFileName(filePath)}-{i++}";
            await memory.SaveInformationAsync(collection, chunk, id);
        }
    }
}