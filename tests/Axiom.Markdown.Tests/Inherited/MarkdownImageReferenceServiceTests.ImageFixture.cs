using Axiom.Core;

namespace Axiom.Core.Tests;

public sealed partial class MarkdownImageReferenceServiceTests
{
    private sealed class ImageFixture : IDisposable
    {
        public ImageFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), $"AxiomImageTests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string CreateDocument(string relativePath)
        {
            var path = Prepare(relativePath);
            File.WriteAllText(path, "# Document");
            return path;
        }

        public string CreateImage(string relativePath)
        {
            var path = Prepare(relativePath);
            File.WriteAllBytes(path, [1, 2, 3]);
            return path;
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
        private string Prepare(string relativePath)
        {
            var path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            return path;
        }
    }
}
