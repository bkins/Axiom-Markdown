using Markdig.Extensions.TaskLists;
using Markdig.Syntax;

namespace Axiom.Core;

public sealed record PreviewTaskMarker(int Offset, bool Checked);
