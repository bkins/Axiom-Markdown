using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace Axiom.Core;

public sealed record SanitizedPreview(string                Html
                                    , IReadOnlyList<string> Changes
                                    , IReadOnlyList<string> RemoteImages);
