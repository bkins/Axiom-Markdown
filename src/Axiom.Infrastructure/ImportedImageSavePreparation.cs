using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Syntax;

namespace Axiom.Infrastructure;

/// <summary>Self-contained import drafts survive recovery; only explicit Save externalizes their image definitions.</summary>
internal sealed partial class ImportedImageSavePreparation : IDisposable
{
    private readonly List<string> _ownedFiles = [];
    private string? _ownedDirectory;
    public string Text { get; private set; }
    public bool Committed { get; set; }

    private ImportedImageSavePreparation(string text) => Text = text;

    public static async Task<ImportedImageSavePreparation> CreateAsync(string path, string text, CancellationToken cancellation)
    {
        var result = new ImportedImageSavePreparation(text);
        try
        {
            if (!text.Contains("[axiom-import-image-", StringComparison.Ordinal)) return result;
            // Only genuine top-level reference definitions qualify; fenced/indented examples remain untouched.
            var document = Markdown.Parse(text);
            var definitions = document.OfType<LinkReferenceDefinitionGroup>().SelectMany(group => group.Links.Values).ToArray();
            var replacements = new List<(int Start, int Length, string Value)>();
            foreach (var definition in definitions)
            {
                cancellation.ThrowIfCancellationRequested();
                if (definition.Span.Start < 0 || definition.Span.End >= text.Length) continue;
                var original = text.Substring(definition.Span.Start, definition.Span.Length).TrimEnd('\r', '\n');
                var match = ImportDefinition().Match(original);
                if (!match.Success) continue;
                var bytes = Convert.FromBase64String(match.Groups["bytes"].Value);
                if (bytes.Length > DocumentImportService.MaximumImageBytes) throw new IOException("Imported image exceeds the 2 MiB save limit.");
                var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
                if (hash != match.Groups["hash"].Value) throw new IOException("Imported image integrity check failed. Retain the unsaved draft and import the original again.");
                var mediaType = "image/" + match.Groups["type"].Value;
                DocumentImportService.ValidateImage(bytes, mediaType);
                if (result._ownedDirectory is null)
                {
                    var parent = Path.GetDirectoryName(Path.GetFullPath(path))!;
                    WorkspaceFileOperations.ValidateAncestors(Path.GetPathRoot(parent)!, parent);
                    var folder = Path.Combine(parent, "imported-assets-" + Guid.NewGuid().ToString("N"));
                    if (Directory.Exists(folder) || File.Exists(folder)) throw new IOException("Import asset destination already exists.");
                    Directory.CreateDirectory(folder);
                    result._ownedDirectory = folder;
                }
                var extension = match.Groups["type"].Value == "jpeg" ? "jpg" : match.Groups["type"].Value;
                var imagePath = Path.Combine(result._ownedDirectory, hash + "." + extension);
                if (!result._ownedFiles.Contains(imagePath, StringComparer.Ordinal))
                {
                    await using var output = new FileStream(imagePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    result._ownedFiles.Add(imagePath);
                    await output.WriteAsync(bytes, cancellation);
                    await output.FlushAsync(cancellation);
                }
                var relative = Path.GetFileName(result._ownedDirectory) + "/" + Path.GetFileName(imagePath);
                replacements.Add((definition.Span.Start, definition.Span.Length, $"[axiom-import-image-{hash}]: {relative}" + (text[definition.Span.End] == '\n' ? "\n" : "")));
            }
            foreach (var replacement in replacements.OrderByDescending(item => item.Start))
                result.Text = result.Text.Remove(replacement.Start, replacement.Length).Insert(replacement.Start, replacement.Value);
            return result;
        }
        catch { result.Dispose(); throw; }
    }

    public void Dispose()
    {
        if (Committed) return;
        foreach (var file in _ownedFiles) if (File.Exists(file)) File.Delete(file);
        if (_ownedDirectory is not null && Directory.Exists(_ownedDirectory) && !Directory.EnumerateFileSystemEntries(_ownedDirectory).Any()) Directory.Delete(_ownedDirectory);
    }

    [GeneratedRegex(@"\A\[axiom-import-image-(?<hash>[0-9a-f]{64})\]: data:image/(?<type>png|jpeg|gif|bmp);base64,(?<bytes>[A-Za-z0-9+/=]+)\z")]
    private static partial Regex ImportDefinition();
}
