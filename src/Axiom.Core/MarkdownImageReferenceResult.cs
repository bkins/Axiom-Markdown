using System.Text.RegularExpressions;

namespace Axiom.Core;

public sealed record MarkdownImageReferenceResult(MarkdownEditResult? Edit
                                                , string?             Error)
{
    public bool Success => Edit is not null;
}
