using System.Text.RegularExpressions;

namespace Axiom.Core;

public static partial class DocumentIdCodec
{
    private const string Prefix = "<!-- axiom:documentId=";
    // Axiom writes its marker as the first line. Anchoring it prevents examples in
    // fenced code blocks (or ordinary prose) from being mistaken for document data.
    [GeneratedRegex(@"\A\uFEFF?<!--\s*axiom:documentId=(?<id>[0-9a-fA-F-]{36})\s*-->\r?\n?", RegexOptions.CultureInvariant)]
    private static partial Regex IdentityRegex();
    public static Guid? Find(string markdown)
    {
        var match = IdentityRegex().Match(markdown ?? string.Empty);
        return match.Success && Guid.TryParse(match.Groups["id"].Value, out var id) && id != Guid.Empty ? id : null;
    }

    public static string Enroll(string markdown
                              , Guid   id)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, Guid.Empty);
        if (Find(markdown) is not null)
        {
            return markdown;
        }

        var separator = string.IsNullOrEmpty(markdown) ? string.Empty : Environment.NewLine + Environment.NewLine;
        return $"{Prefix}{id:D} -->{separator}{markdown}";
    }

    public static string Reassign(string markdown
                                , Guid   id)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, Guid.Empty);
        var marker = $"{Prefix}{id:D} -->";
        return IdentityRegex().IsMatch(markdown) ? IdentityRegex().Replace(markdown, marker, 1) : Enroll(markdown
                                                                                                       , id);
    }

    public static string RemoveCanonicalMarkerForPreview(string markdown)
    {
        return IdentityRegex().Replace(markdown ?? string.Empty, string.Empty, 1);
    }
}
