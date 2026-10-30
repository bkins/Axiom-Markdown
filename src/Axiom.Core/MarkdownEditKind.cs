using System.Text.RegularExpressions;
using Markdig;

namespace Axiom.Core;

public enum MarkdownEditKind
{
    Bold
  , Italic
  , InlineCode
  , Link
  , Paragraph
  , Heading1
  , Heading2
  , Heading3
  , UnorderedList
  , OrderedList
  , TaskList
  , FencedCode
  , HardBreak
  , Table
  , HorizontalRule
  , Callout
  , Strikethrough
}
