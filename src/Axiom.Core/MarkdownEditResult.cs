using System.Text.RegularExpressions;
using Markdig;

namespace Axiom.Core;

public sealed record MarkdownEditResult(string Text
                                      , int    SelectionStart
                                      , int    SelectionLength
                                      , bool   Changed);
