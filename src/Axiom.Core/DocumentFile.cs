using System.Text.RegularExpressions;

namespace Axiom.Core;

public sealed record DocumentFile(string         Path
                                , string         Text
                                , DateTimeOffset LastWriteUtc
                                , long           Length)
{
    public string ObservedEncoding { get; init; } = "Unknown encoding";
}
