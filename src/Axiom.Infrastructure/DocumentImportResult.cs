using System.IO.Compression;
using System.Text;
using AngleSharp.Html.Parser;

namespace Axiom.Infrastructure;

public sealed record DocumentImportResult(string SourcePath, string Markdown, IReadOnlyList<string> Warnings);
