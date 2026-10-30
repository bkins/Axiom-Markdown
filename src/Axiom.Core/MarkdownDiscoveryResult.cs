using System.Text.RegularExpressions;

namespace Axiom.Core;

public sealed record MarkdownDiscoveryResult(IReadOnlyList<string>  Files
                                           , IReadOnlyList<string>  SkippedDirectories
                                           , IReadOnlyList<string>? Folders = null);
