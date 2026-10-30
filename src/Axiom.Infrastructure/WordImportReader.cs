using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace Axiom.Infrastructure;





/// <summary>Reads only named XML/image parts; never extracts or activates a package.</summary>
internal sealed class WordImportReader
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private readonly ZipArchive _package;
    private readonly List<string> _warnings;
    private readonly CancellationToken _cancellation;
    private readonly Dictionary<string, (byte[] Bytes, string MediaType)> _images = [];
    private readonly Dictionary<string, (string Target, bool External)> _relationships = [];
    private readonly Dictionary<string, XElement> _styles = [];
    private readonly Dictionary<string, XElement> _numbers = [];
    private readonly Dictionary<string, XElement> _abstractNumbers = [];
    public WordImportReader(ZipArchive        package
                          , List<string>      warnings
                          , CancellationToken cancellation)
    {
        _package      = package;
        _warnings     = warnings;
        _cancellation = cancellation;
        long expanded = 0;
        if (package.Entries.Count > 2000)
        {
            throw new IOException("DOCX exceeds the 2,000-part limit.");
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in package.Entries)
        {
            cancellation.ThrowIfCancellationRequested();
            expanded += entry.Length;
            if (expanded > 64 * 1024 * 1024 || entry.Length > 32 * 1024 * 1024)
            {
                throw new IOException("DOCX expanded content exceeds the import limit (64 MiB total, 32 MiB per part).");
            }

            if (!names.Add(entry.FullName))
            {
                throw new IOException("DOCX contains duplicate package parts.");
            }
        }
    }

    public string Read()
    {
        var body = Xml("word/document.xml")?.Root?.Element(W + "body") ?? throw new IOException("This is not a supported DOCX document. Encrypted and legacy .doc files are unsupported.");
        foreach (var relation in Xml("word/_rels/document.xml.rels")?.Root?.Elements() ?? [])
        {
            _relationships[(string? )relation.Attribute("Id") ?? ""] = ((string? )relation.Attribute("Target") ?? "", (string? )relation.Attribute("TargetMode") == "External");
        }

        foreach (var style in Xml("word/styles.xml")?.Root?.Elements(W + "style") ?? [])
        {
            _styles[(string? )style.Attribute(W + "styleId") ?? ""] = style;
        }

        var numbering = Xml("word/numbering.xml");
        foreach (var number in numbering?.Root?.Elements(W + "num") ?? [])
        {
            _numbers[(string? )number.Attribute(W + "numId") ?? ""] = number;
        }

        foreach (var number in numbering?.Root?.Elements(W + "abstractNum") ?? [])
        {
            _abstractNumbers[(string? )number.Attribute(W + "abstractNumId") ?? ""] = number;
        }

        if (body.Descendants().Any(node => node.Name.LocalName is "ins" or "del" or "moveFrom" or "moveTo"))
        {
            _warnings.Add("Tracked changes use current text: insertions included, deletions excluded. Review the original change history separately.");
        }

        if (body.Descendants().Any(node => node.Name.LocalName is "footnoteReference" or "endnoteReference" or "commentReference" or "commentRangeStart") || _package.Entries.Any(entry => entry.FullName.StartsWith("word/header"
                                                                                                                                                                                                                   , StringComparison.Ordinal) || entry.FullName.StartsWith("word/footer"
                                                                                                                                                                                                                                                                          , StringComparison.Ordinal)))
        {
            _warnings.Add("Headers, footers, comments, footnotes and endnotes are not included; review the source for additional information.");
        }

        if (body.Descendants().Any(node => node.Name.LocalName is "oMath" or "pict" or "txbxContent" or "object" or "altChunk" or "fldSimple" or "fldChar"))
        {
            _warnings.Add("Equations, legacy drawings, text boxes, objects and field behavior may be omitted or reduced to displayed text.");
        }

        if (body.Descendants(W + "bookmarkStart").Any())
        {
            _warnings.Add("Word bookmarks are not recreated; internal bookmark links require review.");
        }

        var html = new XElement("body");
        Blocks(body.Elements(), html, 0);
        return html.ToString(SaveOptions.DisableFormatting);
    }

    public (byte[] Bytes, string MediaType)? ReadImage(string key) => _images.TryGetValue(key, out var image) ? image : null;
    private XDocument? Xml(string name)
    {
        var entry = _package.GetEntry(name);
        if (entry is null)
        {
            return null;
        }

        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream
                                          , new XmlReaderSettings
                                            {
                                                DtdProcessing           = DtdProcessing.Prohibit
                                              , XmlResolver             = null
                                              , MaxCharactersInDocument = 32 * 1024 * 1024
                                            });
        var document = XDocument.Load(reader);
        var count = 0;
        var nodes = new Stack<(XElement Node, int Depth)>();
        if (document.Root is { } root)
        {
            nodes.Push((root, 0));
        }

        while (nodes.TryPop(out var item))
        {
            _cancellation.ThrowIfCancellationRequested();
            if (item.Depth > 128 || ++count > 100000)
            {
                throw new IOException("DOCX XML exceeds the depth/node limit.");
            }

            foreach (var child in item.Node.Elements())
            {
                nodes.Push((child, item.Depth + 1));
            }
        }

        return document;
    }

    private void Blocks(IEnumerable<XElement> elements
                      , XElement              parent
                      , int                   depth)
    {
        var lists = new List<(string Id, XElement List)>();
        foreach (var element in elements)
        {
            _cancellation.ThrowIfCancellationRequested();
            if (depth > 128)
            {
                throw new IOException("DOCX nesting exceeds the import limit.");
            }

            if (element.Name == W + "p")
            {
                var properties = element.Element(W + "pPr");
                var styleId = (string? )properties?.Element(W + "pStyle")?.Attribute(W + "val");
                var style = StyleProperties(styleId);
                var levelText = (string? )(properties?.Element(W + "outlineLvl") ?? style?.Element(W + "outlineLvl"))?.Attribute(W + "val");
                var heading = int.TryParse(levelText, out var outline) && outline is >= 0 and <= 5 ? outline + 1 : HeadingFromStyle(styleId);
                var paragraph = new XElement(heading > 0 ? "h" + heading : "p");
                foreach (var child in element.Elements().Where(child => child.Name != W + "pPr"))
                {
                    paragraph.Add(Inline(child, 0));
                }

                if (styleId?.Contains("code", StringComparison.OrdinalIgnoreCase) == true || styleId?.Contains("preformatted"
                                                                                                             , StringComparison.OrdinalIgnoreCase) == true)
                {
                    paragraph = new XElement("pre", paragraph.Value);
                }

                var number = properties?.Element(W + "numPr") ?? style?.Element(W + "numPr");
                var id = (string? )number?.Element(W + "numId")?.Attribute(W + "val");
                if (id is not null && id != "0")
                {
                    var level = int.TryParse((string? )number?.Element(W + "ilvl")?.Attribute(W + "val")
                                           , out var parsed) ? Math.Clamp(parsed
                                                                        , 0
                                                                        , 8) : 0;
                    if (level > lists.Count)
                    {
                        _warnings.Add("A list skipped nesting levels; nesting was reduced for Markdown.");
                        level = lists.Count;
                    }

                    if (lists.Count > level + 1)
                    {
                        lists.RemoveRange(level + 1, lists.Count - level - 1);
                    }

                    if (lists.Count == level || lists[level].Id != id)
                    {
                        if (lists.Count > level)
                        {
                            lists.RemoveRange(level, lists.Count - level);
                        }

                        var list = MakeList(id, level);
                        if (level == 0)
                        {
                            parent.Add(list);
                        }
                        else
                        {
                            var previous = lists[level - 1].List.Elements("li").LastOrDefault();
                            if (previous is null)
                            {
                                previous = new XElement("li");
                                lists[level - 1].List.Add(previous);
                            }

                            previous.Add(list);
                        }

                        lists.Add((id, list));
                    }

                    lists[level].List.Add(new XElement("li", paragraph.Nodes()));
                }
                else
                {
                    lists.Clear();
                    parent.Add(paragraph);
                }
            }
            else if (element.Name == W + "tbl")
            {
                lists.Clear();
                var table = new XElement("table");
                foreach (var row in element.Elements(W + "tr"))
                {
                    var tr = new XElement("tr");
                    foreach (var cell in row.Elements(W + "tc"))
                    {
                        var td = new XElement(row.Element(W + "trPr")?.Element(W + "tblHeader") is null ? "td" : "th");
                        if (cell.Descendants(W + "gridSpan").Any() || cell.Descendants(W + "vMerge").Any())
                        {
                            td.SetAttributeValue("colspan", "1");
                        }

                        Blocks(cell.Elements(), td, depth + 1);
                        tr.Add(td);
                    }

                    table.Add(tr);
                }

                parent.Add(table);
            }
            else if (element.Name == W + "sectPr")
            {
            }
            else if (element.Name == W + "del" || element.Name == W + "moveFrom")
            {
            }
            else if (element.Name == W + "altChunk")
            {
                _warnings.Add("An embedded document section was omitted.");
            }
            else
            {
                Blocks(element.Elements(), parent, depth + 1);
            }
        }
    }

    private XElement? StyleProperties(string? id)
    {
        var seen = new HashSet<string>();
        while (id is not null && seen.Add(id) && _styles.TryGetValue(id, out var style))
        {
            if (style.Element(W + "pPr") is { } properties)
            {
                return properties;
            }

            id = (string? )style.Element(W + "basedOn")?.Attribute(W + "val");
        }

        return null;
    }

    private static int HeadingFromStyle(string? id)
    {
        if (id is not null && id.StartsWith("Heading", StringComparison.OrdinalIgnoreCase) && int.TryParse(id[7..].Trim()
                                                                                                         , out var level) && level is >= 1 and <= 6)
        {
            return level;
        }

        return 0;
    }

    private XElement MakeList(string id
                            , int    level)
    {
        XElement? definition = null;
        XElement? overrideLevel = null;
        if (_numbers.TryGetValue(id, out var num))
        {
            var abstractId = (string? )num.Element(W + "abstractNumId")?.Attribute(W + "val");
            if (abstractId is not null && _abstractNumbers.TryGetValue(abstractId, out var abstractNum))
            {
                definition = abstractNum.Elements(W + "lvl").FirstOrDefault(lvl => (string? )lvl.Attribute(W + "ilvl") == level.ToString());
            }

            overrideLevel = num.Elements(W + "lvlOverride").FirstOrDefault(lvl => (string? )lvl.Attribute(W + "ilvl") == level.ToString());
            definition    = overrideLevel?.Element(W + "lvl") ?? definition;
        }

        var format = (string? )definition?.Element(W + "numFmt")?.Attribute(W + "val");
        if (format is not ("bullet" or "decimal"))
        {
            _warnings.Add("Non-decimal, unknown or custom list numbering was simplified; review numbering and continuation.");
        }

        var list = new XElement(format == "bullet" ? "ul" : "ol");
        var start = (string? )(overrideLevel?.Element(W + "startOverride") ?? definition?.Element(W + "start"))?.Attribute(W + "val");
        if (start is not null)
        {
            list.SetAttributeValue("start", start);
        }

        if (format != "bullet")
        {
            _warnings.Add("Ordered list starts are retained where defined; numbering continuation across separate lists needs review.");
        }

        return list;
    }

    private object? Inline(XElement element
                         , int      depth)
    {
        _cancellation.ThrowIfCancellationRequested();
        if (depth > 128)
        {
            throw new IOException("DOCX inline nesting exceeds the import limit.");
        }

        if (element.Name == W + "del" || element.Name == W + "moveFrom" || element.Name == W + "delText" || element.Name == W + "instrText" || element.Name == W + "txbxContent")
        {
            return null;
        }

        if (element.Name == W + "t")
        {
            return element.Value;
        }

        if (element.Name == W + "tab")
        {
            return " ";
        }

        if (element.Name == W + "br" || element.Name == W + "cr")
        {
            return new XElement("br");
        }

        if (element.Name.LocalName == "AlternateContent")
        {
            _warnings.Add("Alternative drawing representations were reduced to one representation; review drawing fidelity.");
            var selected = element.Elements().FirstOrDefault(child => child.Name.LocalName == "Choice") ?? element.Elements().FirstOrDefault();
            return selected?.Elements().Select(child => Inline(child, depth + 1)).ToArray();
        }

        if (element.Name.LocalName == "blip")
        {
            var id = (string? )element.Attribute(R + "embed");
            var description = element.Ancestors().FirstOrDefault(ancestor => ancestor.Name.LocalName == "inline" || ancestor.Name.LocalName == "anchor")?.Descendants().FirstOrDefault(node => node.Name.LocalName == "docPr");
            var alt = (string? )description?.Attribute("descr") ?? (string? )description?.Attribute("name") ?? "image";
            if (id is not null && _relationships.TryGetValue(id, out var relationship) && !relationship.External)
            {
                var part = new Uri(new Uri("https://package.invalid/word/document.xml"), relationship.Target).AbsolutePath.TrimStart('/');
                var entry = _package.GetEntry(Uri.UnescapeDataString(part));
                var type = Path.GetExtension(part).ToLowerInvariant() switch
                {
                    ".png" => "image/png",
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".gif" => "image/gif",
                    ".bmp" => "image/bmp",
                    _ => null
                };
                if (entry is not null && type is not null && entry.Length <= DocumentImportService.MaximumImageBytes)
                {
                    using var stream = entry.Open();
                    _images[id] = (DocumentImportService.ReadBoundedAsync(stream
                                                                        , DocumentImportService.MaximumImageBytes
                                                                        , _cancellation)
                                                        .GetAwaiter()
                                                        .GetResult(), type);
                }
            }

            return new XElement("img", new XAttribute("src", id ?? "missing"), new XAttribute("alt", alt));
        }

        var children = element.Elements()
                              .Where(child => child.Name != W + "rPr")
                              .Select(child => Inline(child
                                                    , depth + 1))
                              .ToArray();
        if (element.Name == W + "hyperlink")
        {
            var id = (string? )element.Attribute(R + "id");
            var destination = id is not null && _relationships.TryGetValue(id, out var relationship) ? relationship.Target : "#" + (string? )element.Attribute(W + "anchor");
            return new XElement("a", new XAttribute("href", destination), children);
        }

        if (element.Name == W + "r")
        {
            object content = children;
            var properties = element.Element(W + "rPr");
            if (On(properties?.Element(W + "b")))
            {
                content = new XElement("strong", content);
            }

            if (On(properties?.Element(W + "i")))
            {
                content = new XElement("em", content);
            }

            if (On(properties?.Element(W + "strike")))
            {
                content = new XElement("del", content);
            }

            if (properties?.Element(W + "u") is not null || properties?.Element(W + "vertAlign") is not null)
            {
                _warnings.Add("Underline, superscript and subscript formatting was reduced to text.");
            }

            return content;
        }

        return children;
    }

    private static bool On(XElement? property)
    {
        return property is not null && (string? )property.Attribute(W + "val") is not ("0" or "false" or "off");
    }
}
