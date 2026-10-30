using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Axiom.Core;

namespace Axiom.Infrastructure;

internal sealed partial class ImportMarkdownWriter(string source, List<string> warnings, CancellationToken cancellation)
{
    private readonly Dictionary<string, string> _images = [];
    private int _nodes;
    private int _imageBytes;
    public Func<string, (byte[] Bytes, string MediaType)?>? ReadImage { get; set; }

    public string Write(IElement body)
    {
        var pending = new Stack<(INode Node, int Depth)>();
        pending.Push((body, 0));
        var count = 0;
        while (pending.TryPop(out var current))
        {
            cancellation.ThrowIfCancellationRequested();
            if (current.Depth > 128 || ++count > 100000) throw new IOException("Document structure exceeds the import depth/node limit. Split it into smaller files.");
            foreach (var child in current.Node.ChildNodes) pending.Push((child, current.Depth + 1));
        }
        // Word often splits one formatted word across several runs. Coalesce equal adjacent wrappers
        // before emitting delimiters so their boundary cannot become literal Markdown punctuation.
        var elements = new Stack<IElement>();
        elements.Push(body);
        while (elements.TryPop(out var element))
        {
            cancellation.ThrowIfCancellationRequested();
            foreach (var child in element.Children.ToArray())
            {
                if (child.LocalName is "strong" or "b" or "em" or "i" or "del" or "s" or "strike" && child.Attributes.Length == 0)
                {
                    while (child.NextSibling is IElement next && next.LocalName == child.LocalName && next.Attributes.Length == 0)
                    {
                        foreach (var node in next.ChildNodes.ToArray()) child.AppendChild(node);
                        next.Remove();
                    }
                }
            }
            foreach (var child in element.Children) elements.Push(child);
        }
        var text = CleanBlankLines(Render(body, 0)).Trim();
        return text + (_images.Count == 0 ? "\n" : "\n\n" + string.Join("\n", _images.Select(pair => $"[{pair.Key}]: {pair.Value}")) + "\n");
    }

    private string Render(INode node, int depth)
    {
        cancellation.ThrowIfCancellationRequested();
        if (depth > 128 || ++_nodes > 100000) throw new IOException("Document structure exceeds the import depth/node limit. Split it into smaller files.");
        if (node is IText text) return Escape(Whitespace().Replace(text.Data, " "));
        if (node is not IElement element) return "";
        var tag = element.LocalName;
        if (element.HasAttribute("id") || tag == "a" && element.HasAttribute("name")) warnings.Add("Source element anchors were not recreated; review internal links after conversion.");
        if (tag is "script" or "style" or "head" or "template" or "iframe" or "object" or "embed" or "noscript" || element.HasAttribute("hidden"))
        {
            warnings.Add("Scripts, styles, hidden content and embedded active objects were omitted.");
            return "";
        }
        if (tag == "img") return Image(element);
        if (tag == "pre") return "\n\n" + Fence(element.TextContent) + "\n\n";
        if (tag is "ul" or "ol") return List(element, depth);
        if (tag == "table") return Table(element, depth);
        var content = string.Concat(element.ChildNodes.Select(child => Render(child, depth + 1)));
        return tag switch
        {
            "h1" or "h2" or "h3" or "h4" or "h5" or "h6" => "\n\n" + new string('#', tag[1] - '0') + " " + content.Trim() + "\n\n",
            "p" or "div" or "section" or "article" or "header" or "footer" or "main" or "figure" or "figcaption" => "\n\n" + content.Trim() + "\n\n",
            "strong" or "b" => Wrap(content, "**"),
            "em" or "i" => Wrap(content, "*"),
            "del" or "s" or "strike" => Wrap(content, "~~"),
            "code" => Code(element.TextContent),
            "a" => Link(content, element.GetAttribute("href")),
            "br" => "  \n",
            "hr" => "\n\n---\n\n",
            "blockquote" => "\n\n" + string.Join("\n", content.Trim().Split('\n').Select(line => "> " + line)) + "\n\n",
            "input" or "button" or "select" or "textarea" or "canvas" or "svg" or "math" => Unsupported(tag, content),
            "sup" or "sub" or "u" => Unsupported(tag, content),
            _ => content
        };
    }

    private string Unsupported(string tag, string content)
    {
        warnings.Add($"Unsupported {tag} content or formatting was reduced to text; review the source for missing information.");
        return content;
    }

    private string List(IElement list, int depth)
    {
        if (list.HasAttribute("reversed") || list.HasAttribute("type") || list.Children.Any(child => child.HasAttribute("value"))) warnings.Add("Custom, reversed or per-item list numbering was simplified; review the sequence.");
        var ordered = list.LocalName == "ol";
        var number = int.TryParse(list.GetAttribute("start"), out var start) && start > 0 ? start : 1;
        var output = new StringBuilder("\n\n");
        foreach (var item in list.Children.Where(child => child.LocalName == "li"))
        {
            var marker = ordered ? $"{number++}. " : "- ";
            var content = string.Concat(item.ChildNodes.Select(child => Render(child, depth + 1))).Trim();
            var lines = content.Split('\n');
            output.Append(marker).Append(lines[0]).Append('\n');
            foreach (var line in lines.Skip(1)) output.Append(' ', marker.Length).Append(line).Append('\n');
        }
        return output.Append('\n').ToString();
    }

    private string Table(IElement table, int depth)
    {
        if (table.QuerySelector("table, [colspan], [rowspan]") is not null)
        {
            warnings.Add("A merged or nested table was flattened into paragraphs; cell relationships need review.");
            return "\n\n" + string.Join("\n\n", table.QuerySelectorAll("tr").Select(row => string.Join("; ", row.Children.Where(cell => cell.LocalName is "td" or "th").Select(cell => string.Concat(cell.ChildNodes.Select(child => Render(child, depth + 1))).Trim())))) + "\n\n";
        }
        var rows = table.QuerySelectorAll("tr").Select(row => row.Children.Where(cell => cell.LocalName is "td" or "th").ToArray()).Where(row => row.Length > 0).ToArray();
        if (rows.Length == 0) return "";
        var columns = rows.Max(row => row.Length);
        if (columns > 100) throw new IOException("Table exceeds the 100-column import limit.");
        var hasHeader = rows[0].Any(cell => cell.LocalName == "th");
        var output = new StringBuilder("\n\n");
        string Row(IEnumerable<IElement> cells) => "| " + string.Join(" | ", cells.Select(cell => UnescapedPipe().Replace(string.Concat(cell.ChildNodes.Select(child => Render(child, depth + 1))).Trim().Replace("\r", "").Replace("\n", " "), "\\|")).Concat(Enumerable.Repeat("", columns - cells.Count()))) + " |\n";
        output.Append(hasHeader ? Row(rows[0]) : "| " + string.Join(" | ", Enumerable.Repeat("", columns)) + " |\n");
        output.Append("| ").Append(string.Join(" | ", Enumerable.Repeat("---", columns))).Append(" |\n");
        foreach (var row in rows.Skip(hasHeader ? 1 : 0)) output.Append(Row(row));
        if (table.QuerySelector("p, br, ul, ol") is not null) warnings.Add("Paragraphs and line breaks inside table cells were flattened to spaces.");
        return output.Append('\n').ToString();
    }

    private string Link(string text, string? destination)
    {
        if (string.IsNullOrWhiteSpace(destination)) return text;
        if (destination.Any(char.IsControl)) { warnings.Add("A hyperlink containing control characters was omitted; its text was retained."); return text; }
        if (destination.StartsWith('#')) return $"[{text}](<{destination.Replace(">", "%3E").Replace("<", "%3C")}>)";
        if (!Uri.TryCreate(new Uri(source), destination, out var uri) || uri.Scheme is not ("http" or "https" or "mailto" or "file") || uri.IsUnc || !string.IsNullOrEmpty(uri.UserInfo))
        {
            warnings.Add("An unsafe, network-share or unsupported hyperlink was omitted; its text was retained.");
            return text;
        }
        if (uri.IsFile) warnings.Add("Local hyperlinks point to their original source location; review portability after saving.");
        return $"[{text}](<{uri.AbsoluteUri.Replace(">", "%3E").Replace("<", "%3C")}>)";
    }

    private string Image(IElement image)
    {
        var alt = Escape(image.GetAttribute("alt") ?? "image");
        var location = image.GetAttribute("src") ?? "";
        try
        {
            var asset = ReadImage is null ? ReadLocalImage(location) : ReadImage(location);
            if (asset is null) throw new IOException("Missing, remote or unsupported image.");
            if (asset.Value.Bytes.Length > DocumentImportService.MaximumImageBytes) throw new IOException("Image exceeds 2 MiB.");
            DocumentImportService.ValidateImage(asset.Value.Bytes, asset.Value.MediaType);
            var hash = Convert.ToHexStringLower(SHA256.HashData(asset.Value.Bytes));
            var id = "axiom-import-image-" + hash;
            if (!_images.ContainsKey(id))
            {
                _imageBytes += asset.Value.Bytes.Length;
                if (_imageBytes > 4 * 1024 * 1024) throw new IOException("Total imported images exceed 4 MiB.");
                _images[id] = $"data:{asset.Value.MediaType};base64,{Convert.ToBase64String(asset.Value.Bytes)}";
            }
            return $"![{alt}][{id}]";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or FormatException or NotSupportedException)
        {
            warnings.Add($"Image omitted ({alt}): {error.Message} Alt text retained.");
            return alt;
        }
    }

    private (byte[] Bytes, string MediaType)? ReadLocalImage(string location)
    {
        if (location.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var comma = location.IndexOf(',');
            if (comma < 0 || !location[..comma].EndsWith(";base64", StringComparison.OrdinalIgnoreCase)) return null;
            if (location.Length > DocumentImportService.MaximumImageBytes * 4 / 3 + 100) throw new IOException("Image exceeds 2 MiB.");
            var type = location[5..comma].Split(';')[0];
            if (type is not ("image/png" or "image/jpeg" or "image/gif" or "image/bmp")) return null;
            return (Convert.FromBase64String(location[(comma + 1)..]), type);
        }
        if (!Uri.TryCreate(new Uri(source), location, out var uri) || !uri.IsFile || uri.IsUnc) return null;
        var root = Path.GetDirectoryName(source)!;
        var path = uri.LocalPath;
        var relative = Path.GetRelativePath(root, path);
        if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar) || Path.IsPathRooted(relative)) throw new IOException("Image is outside the source folder.");
        WorkspaceFileOperations.ValidateAncestors(root, path);
        if (File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint)) throw new IOException("Linked image files are not imported.");
        var typeName = Path.GetExtension(path).ToLowerInvariant() switch { ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".gif" => "image/gif", ".bmp" => "image/bmp", _ => null };
        if (typeName is null) return null;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return (DocumentImportService.ReadBoundedAsync(stream, DocumentImportService.MaximumImageBytes, cancellation).GetAwaiter().GetResult(), typeName);
    }

    internal static string Escape(string value) => MarkdownPunctuation().Replace(value, "\\$0");
    private static string CleanBlankLines(string text)
    {
        var output = new StringBuilder();
        var fenceLength = 0;
        var blank = false;
        foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
        {
            var trimmed = line.TrimStart(' ', '\t', '>');
            var ticks = trimmed.TakeWhile(character => character == '`').Count();
            var wasFenced = fenceLength != 0;
            if (fenceLength == 0 && ticks >= 3) fenceLength = ticks;
            else if (fenceLength != 0 && ticks >= fenceLength && string.IsNullOrWhiteSpace(trimmed[ticks..])) fenceLength = 0;
            if (!wasFenced && fenceLength == 0 && string.IsNullOrWhiteSpace(line))
            {
                if (!blank) output.Append('\n');
                blank = true;
            }
            else { output.Append(line).Append('\n'); blank = false; }
        }
        return output.ToString();
    }
    private static string Wrap(string text, string marker) => string.IsNullOrWhiteSpace(text) ? text : (char.IsWhiteSpace(text[0]) ? " " : "") + marker + text.Trim() + marker + (char.IsWhiteSpace(text[^1]) ? " " : "");
    private static string Code(string text)
    {
        var ticks = new string('`', Math.Max(1, TickRuns().Matches(text).Select(match => match.Length + 1).DefaultIfEmpty(1).Max()));
        return ticks + " " + text.Replace('\n', ' ') + " " + ticks;
    }
    private static string Fence(string text)
    {
        var ticks = new string('`', Math.Max(3, TickRuns().Matches(text).Select(match => match.Length + 1).DefaultIfEmpty(3).Max()));
        return ticks + "\n" + text.Replace("\r\n", "\n").TrimEnd('\n') + "\n" + ticks;
    }
    [GeneratedRegex(@"\s+")] private static partial Regex Whitespace();
    [GeneratedRegex(@"[\\`*_{}\[\]()<>#!|~+\-.]")] private static partial Regex MarkdownPunctuation();
    [GeneratedRegex("`+")] private static partial Regex TickRuns();
    [GeneratedRegex(@"(?<!\\)\|")] private static partial Regex UnescapedPipe();
}
