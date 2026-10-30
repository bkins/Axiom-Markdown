using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace Axiom.Core;

public sealed record SanitizedPreview(string Html, IReadOnlyList<string> Changes, IReadOnlyList<string> RemoteImages);

/// <summary>DOM allowlist before any untrusted HTML reaches the webview. No loader or scripting engine is attached.</summary>
public sealed class PreviewSanitizer
{
    private static readonly HashSet<string> AllowedTags = new("a abbr b blockquote br caption code col colgroup dd del details div dl dt em figcaption figure h1 h2 h3 h4 h5 h6 hr i img input kbd li mark ol p pre s samp small span strong sub summary sup table tbody td th thead tfoot tr u ul var".Split(' '));
    private static readonly HashSet<string> DropSubtrees = new("script style iframe frame frameset object embed template noscript svg math form button textarea select option audio video source link meta base".Split(' '));
    private static readonly HashSet<string> AllowedAttributes = new("alt title class id width height colspan rowspan start reversed open role aria-label aria-checked tabindex type checked disabled data-source-start data-source-line data-source-end data-task-offset".Split(' '));

    public SanitizedPreview Sanitize(string html, IReadOnlyDictionary<string, string>? trustedImages = null, string? linkToken = null)
    {
        using var document = new HtmlParser().ParseDocument("<!doctype html><html><body></body></html>");
        var body = document.Body!;
        foreach (var node in new HtmlParser().ParseFragment(html, body).ToArray()) body.AppendChild(node);
        var changes = new List<string>();
        var remote = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in body.QuerySelectorAll("*").ToArray())
        {
            if (!body.Contains(element)) continue;
            var tag = element.LocalName;
            if (DropSubtrees.Contains(tag))
            {
                changes.Add($"Removed <{tag}> and its embedded content.");
                element.Remove(); continue;
            }
            if (!AllowedTags.Contains(tag))
            {
                changes.Add($"Removed unsupported <{tag}> markup; retained its text.");
                element.Replace(document.CreateTextNode(element.TextContent)); continue;
            }
            foreach (var attribute in element.Attributes.ToArray())
            {
                var name = attribute.Name.ToLowerInvariant();
                if ((tag == "a" && name == "href") || (tag == "img" && name == "src")) continue;
                if (!AllowedAttributes.Contains(name) || name.Contains(':'))
                {
                    element.RemoveAttribute(attribute.Name);
                    changes.Add($"Removed {name} from <{tag}>.");
                }
            }
            if (tag == "input")
            {
                if (element.GetAttribute("type") != "checkbox")
                { changes.Add("Removed interactive input."); element.Remove(); continue; }
                element.SetAttribute("disabled", "disabled");
            }
            if (tag == "a" && element.GetAttribute("href") is { } href && !SafeLink(href))
            { element.RemoveAttribute("href"); changes.Add("Removed an unsafe link destination."); }
            else if (tag == "a" && element.GetAttribute("href") is { } destination && linkToken is not null && !destination.StartsWith("axiom-", StringComparison.Ordinal))
                element.SetAttribute("href", $"axiom-destination://follow/{linkToken}/{Uri.EscapeDataString(destination)}");
            if (tag != "img" || element.GetAttribute("src") is not { } source) continue;
            if (Uri.TryCreate(source, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" && string.IsNullOrEmpty(uri.UserInfo))
            {
                remote.Add(uri.AbsoluteUri);
                if (trustedImages is not null && trustedImages.TryGetValue(uri.AbsoluteUri, out var data) && SafeRasterData(data)) element.SetAttribute("src", data);
                else element.RemoveAttribute("src");
            }
            else if (!SafeRasterData(source))
            {
                element.RemoveAttribute("src");
                changes.Add("Blocked an unsupported, linked, or out-of-folder image source.");
            }
        }
        return new(body.InnerHtml, changes.Distinct().ToArray(), remote.Order(StringComparer.Ordinal).ToArray());
    }

    public static bool SafeRasterData(string source)
    {
        if (source.Length > 28 * 1024 * 1024) return false;
        var comma = source.IndexOf(',');
        if (comma < 0) return false;
        return new[] { "data:image/png;base64", "data:image/jpeg;base64", "data:image/gif;base64", "data:image/webp;base64", "data:image/bmp;base64" }
            .Contains(source[..comma], StringComparer.OrdinalIgnoreCase) && source[(comma + 1)..].All(character => char.IsAsciiLetterOrDigit(character) || character is '+' or '/' or '=' or '\r' or '\n');
    }

    public static bool SafeLink(string href)
    {
        if (string.IsNullOrWhiteSpace(href) || href.Any(char.IsControl) || href.StartsWith("//") || href.Contains('\\')) return false;
        if (!Uri.TryCreate(href, UriKind.Absolute, out var uri)) return true;
        return uri.Scheme is "https" or "http" or "mailto" or "axiom-link" or "axiom-task" or "axiom-source" or "axiom-wiki";
    }
}
