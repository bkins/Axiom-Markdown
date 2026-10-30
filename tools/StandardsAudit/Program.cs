using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Axiom.StandardsAudit;

internal static class Program
{
    private static int Main(string[] arguments)
    {
        var repository = Path.GetFullPath(arguments.FirstOrDefault() ?? ".");
        var files = new[]
        {
            "src"
          , "tests"
          , "tools"
        }.Select(folder => Path.Combine(repository, folder))
         .Where(Directory.Exists)
         .SelectMany(folder => Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
         .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj"))
         .Order(StringComparer.OrdinalIgnoreCase)
         .ToArray();
        var findings = new List<StandardsFinding>();
        foreach (var file in files)
        {
            using var stream = new FileStream(file
                                            , FileMode.Open
                                            , FileAccess.Read
                                            , FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            var tree = CSharpSyntaxTree.ParseText(reader.ReadToEnd()
                                                , new CSharpParseOptions(LanguageVersion.Preview));
            var root = tree.GetRoot();
            void Report(SyntaxNode node
                      , string     rule
                      , string     message)
            {
                findings.Add(new(Path.GetRelativePath(repository, file).Replace('\\'
                                                                              , '/')
                               , node.GetLocation().GetLineSpan().StartLinePosition.Line + 1
                               , rule
                               , message));
            }

            foreach (var diagnostic in tree.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
            {
                findings.Add(new(Path.GetRelativePath(repository
                                                    , file)
                               , diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1
                               , "Syntax"
                               , diagnostic.GetMessage()));
            }

            var types = root.DescendantNodes()
                            .OfType<BaseTypeDeclarationSyntax>()
                            .Where(type => type is not TypeDeclarationSyntax container || !container.Modifiers.Any(SyntaxKind.PartialKeyword) || container.Members.Count == 0 || container.Members.Any(member => member is not BaseTypeDeclarationSyntax))
                            .ToArray();
            if (types.Length > 1)
            {
                Report(types[1]
                     , "OneTypePerFile"
                     , "Keep each complete class/record/interface/enum in its own file; containing partial wrappers are allowed.");
            }

            foreach (var type in types)
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (name != type.Identifier.ValueText && !name.StartsWith(type.Identifier.ValueText + "."
                                                                        , StringComparison.Ordinal) && !name.EndsWith("." + type.Identifier.ValueText
                                                                                                                    , StringComparison.Ordinal))
                {
                    Report(type, "TypeFileName", "The file should identify the type it contains.");
                }
            }

            foreach (var parameter in root.DescendantNodes()
                                          .OfType<ParameterSyntax>()
                                          .Where(parameter => parameter.Parent is LambdaExpressionSyntax || parameter.Parent?.Parent is LambdaExpressionSyntax))
            {
                if (parameter.Identifier.ValueText.Length == 1 && parameter.Identifier.ValueText is not "_" and not "i" and not "j" and not "k")
                {
                    Report(parameter
                         , "LambdaName"
                         , "Use a descriptive lambda parameter; only simple counters and discards may use single characters.");
                }
            }

            foreach (var initializer in root.DescendantNodes().OfType<InitializerExpressionSyntax>())
            {
                CheckSeparators(initializer.Expressions.GetSeparators(), initializer, Report);
            }

            foreach (var enumeration in root.DescendantNodes().OfType<EnumDeclarationSyntax>())
            {
                CheckSeparators(enumeration.Members.GetSeparators(), enumeration, Report);
            }

            foreach (var parameters in root.DescendantNodes().OfType<ParameterListSyntax>().Where(parameters => parameters.Parameters.Count > 1))
            {
                var firstLine = parameters.Parameters[0].GetLocation().GetLineSpan().StartLinePosition.Line;
                var openingLine = parameters.OpenParenToken.GetLocation().GetLineSpan().StartLinePosition.Line;
                var multiline = parameters.Parameters.Last().GetLocation().GetLineSpan().StartLinePosition.Line != firstLine;
                if (multiline && openingLine != firstLine)
                {
                    Report(parameters
                         , "ParameterStart"
                         , "Start the first wrapped parameter on the method-signature line.");
                }

                if (multiline)
                {
                    CheckSeparators(parameters.Parameters.GetSeparators(), parameters, Report);
                }

                var typeColumns = parameters.Parameters.Where(parameter => parameter.AttributeLists.Count == 0)
                                                       .Select(parameter => parameter.Type?.GetLocation().GetLineSpan().StartLinePosition.Character)
                                                       .Distinct()
                                                       .ToArray();
                if (multiline && parameters.Parameters.All(parameter => parameter.AttributeLists.Count == 0) && typeColumns.Length > 1)
                {
                    Report(parameters, "ParameterColumns", "Align wrapped parameter type columns.");
                }
            }

            foreach (var arrow in root.DescendantNodes().OfType<ArrowExpressionClauseSyntax>())
            {
                if (arrow.Expression.ToString().Length > 120)
                {
                    Report(arrow, "ExpressionBody", "Use a statement body for a long or nontrivial member.");
                }
            }

            foreach (var group in root.DescendantNodes()
                                      .OfType<StatementSyntax>()
                                      .Where(statement => statement.Parent is BlockSyntax)
                                      .GroupBy(statement => statement.GetLocation().GetLineSpan().StartLinePosition.Line))
            {
                if (group.Count() > 1)
                {
                    Report(group.Skip(1).First()
                         , "CompoundStatements"
                         , "Place separate statements on separate lines.");
                }
            }
        }

        var report = new
        {
            FilesChecked = files.Length,
            Findings = findings,
            Passed = findings.Count == 0,
            Scope = "Maintained C# source, tests and tools; generated/vendor assets are excluded.",
            ManualReviewRequired = "Clean Code/SOLID, naming intent, responsibilities and justified protocol/interop exceptions require a separate code review."
        };
        var output = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        if (arguments.Length > 1)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(arguments[1]))!);
            File.WriteAllText(arguments[1], output);
        }

        Console.WriteLine(output);
        return findings.Count == 0 ? 0 : 1;
    }

    private static void CheckSeparators(IEnumerable<SyntaxToken>           separators
                                      , SyntaxNode                         owner
                                      , Action<SyntaxNode, string, string> report)
    {
        foreach (var separator in separators)
        {
            var previous = separator.GetPreviousToken().GetLocation().GetLineSpan().EndLinePosition.Line;
            var current = separator.GetLocation().GetLineSpan().StartLinePosition.Line;
            var next = separator.GetNextToken().GetLocation().GetLineSpan().StartLinePosition.Line;
            if (previous != next && (current != next || current == previous))
            {
                report(owner, "LeadingCommas", "Use leading commas on multiline lists.");
                return;
            }
        }
    }
}
