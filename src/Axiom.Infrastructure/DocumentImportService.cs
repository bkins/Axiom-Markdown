using System.IO.Compression;
using System.Text;
using AngleSharp.Html.Parser;

namespace Axiom.Infrastructure;

public sealed record DocumentImportResult(string SourcePath, string Markdown, IReadOnlyList<string> Warnings);

/// <summary>Reads a bounded snapshot. Does not execute HTML, fetch URLs, or write files.</summary>
public sealed class DocumentImportService
{
    public const int MaximumInputBytes = 16 * 1024 * 1024;
    public const int MaximumOutputBytes = 8 * 1024 * 1024;
    public const int MaximumImageBytes = 2 * 1024 * 1024;

    public static bool IsSupported(string path) => Path.GetExtension(path).ToLowerInvariant() is ".html" or ".htm" or ".docx";

    internal static void ValidateImage(byte[] bytes, string mediaType)
    {
        var dimensions = ExportImageDimensions.Read(bytes, mediaType);
        if (dimensions.Width > 8192 || dimensions.Height > 8192 || (long)dimensions.Width * dimensions.Height > 16000000)
            throw new IOException("Image exceeds the decoded-size limit (8,192 pixels per side / 16 million pixels).");
    }

    public async Task<DocumentImportResult> ConvertAsync(string path, CancellationToken cancellationToken = default)
    {
        Axiom.Core.FeatureAccess.Require(Axiom.Core.OptionalFeature.Import);
        if (!IsSupported(path)) throw new IOException("Choose an HTML (.html/.htm) or Word (.docx) file. PDF and scanned-document import are planned separately.");
        path = Path.GetFullPath(path);
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);
        var bytes = await ReadBoundedAsync(file, MaximumInputBytes, cancellationToken);
        return await Task.Run(async () =>
        {
            var warnings = new List<string> { "Conversion preserves content and basic structure. Page layout, fonts and exact spacing are not preserved; review before saving." };
            using var snapshot = new MemoryStream(bytes, false);
            var renderer = new ImportMarkdownWriter(path, warnings, cancellationToken);
            string html;
            if (Path.GetExtension(path).Equals(".docx", StringComparison.OrdinalIgnoreCase))
            {
                using var package = new ZipArchive(snapshot, ZipArchiveMode.Read);
                var word = new WordImportReader(package, warnings, cancellationToken);
                html = word.Read();
                renderer.ReadImage = word.ReadImage;
            }
            else
            {
                // The parser handles BOM and HTML charset declarations; no browsing context or resource loader is attached.
                using var document = await new HtmlParser().ParseDocumentAsync(snapshot, cancellationToken);
                html = document.DocumentElement.OuterHtml;
            }
            using var parsed = await new HtmlParser().ParseDocumentAsync(html, cancellationToken);
            if (parsed.All.SelectMany(element => element.Attributes).Any(attribute => attribute.Name.StartsWith("on", StringComparison.OrdinalIgnoreCase) || attribute.Name is "srcdoc" or "formaction" or "style" or "srcset"))
                warnings.Add("HTML sanitization: event handlers, inline styles or active/resource attributes were omitted from converted Markdown. The original source is unchanged.");
            var markdown = renderer.Write(parsed.Body!);
            if (string.IsNullOrWhiteSpace(markdown)) throw new IOException("No supported document content was found. The original is unchanged.");
            if (Encoding.UTF8.GetByteCount(markdown) > MaximumOutputBytes) throw new IOException("Converted Markdown exceeds the 8 MiB limit. Split the source document and try again.");
            Axiom.Core.FeatureAccess.Require(Axiom.Core.OptionalFeature.Import);
            return new DocumentImportResult(path, markdown, warnings.Distinct().ToArray());
        }, cancellationToken);
    }

    internal static async Task<byte[]> ReadBoundedAsync(Stream stream, int maximum, CancellationToken cancellation)
    {
        using var output = new MemoryStream();
        var buffer = new byte[65536];
        int count;
        while ((count = await stream.ReadAsync(buffer, cancellation)) > 0)
        {
            if (output.Length + count > maximum) throw new IOException($"Document or image exceeds its {maximum / (1024 * 1024)} MiB limit.");
            output.Write(buffer, 0, count);
        }
        cancellation.ThrowIfCancellationRequested();
        return output.ToArray();
    }
}
