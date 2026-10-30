using System.Xml;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;

namespace Axiom.App;

internal static class MarkdownHighlighting
{
    internal static IHighlightingDefinition Create(bool dark)
    {
        var definition = $$"""
            <SyntaxDefinition name="Markdown" xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
              <Color name="Heading" foreground="{{(dark ? "#93C5FD" : "#1D4ED8")}}" fontWeight="bold" />
              <Color name="Bold" foreground="{{(dark ? "#C4B5FD" : "#6D28D9")}}" fontWeight="bold" />
              <Color name="Code" foreground="{{(dark ? "#86EFAC" : "#166534")}}" />
              <Color name="Link" foreground="{{(dark ? "#67E8F9" : "#0E7490")}}" />
              <RuleSet>
                <Rule color="Heading">^\s{0,3}\#{1,6}.*$</Rule>
                <Rule color="Bold">\*\*[^*]+\*\*</Rule>
                <Span color="Code" begin="`" end="`" />
                <Rule color="Link">\[[^\]]+\]\([^\)]+\)</Rule>
              </RuleSet>
            </SyntaxDefinition>
            """;
        using var reader = XmlReader.Create(new StringReader(definition));
        return HighlightingLoader.Load(reader, HighlightingManager.Instance);
    }
}
