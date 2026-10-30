using Axiom.Core;

namespace Axiom.Core.Tests;

public sealed partial class DocumentServiceTests
{
    private sealed class MemoryFiles : IDocumentFileStore
    {
        private readonly Dictionary<string, string> _files;
        private DateTimeOffset _writeUtc = DateTimeOffset.UtcNow;
        public int WriteCount { get; private set; }
        public string Text => _files.Values.Single();

        public MemoryFiles() : this(new Dictionary<string, string>())
        {
        }

        public MemoryFiles(string path, string text) : this(new Dictionary<string, string> { [path] = text })
        {
        }

        public MemoryFiles(Dictionary<string, string> files)
        {
            _files = new Dictionary<string, string>(files, StringComparer.OrdinalIgnoreCase);
        }

        public Task<MarkdownDiscoveryResult> ListMarkdownFilesAsync(string            folder
                                                                  , CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new MarkdownDiscoveryResult(_files.Keys.ToArray(), []));
        }

        public Task<DocumentFile> ReadAsync(string            path
                                          , CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DocumentFile(path
                                                  , _files[path]
                                                  , _writeUtc
                                                  , _files[path].Length));
        }

        public Task<DocumentFile> WriteAsync(string            path
                                           , string            text
                                           , CancellationToken cancellationToken = default)
        {
            _files[path] = text;
            WriteCount++;
            _writeUtc = _writeUtc.AddSeconds(1);
            return ReadAsync(path, cancellationToken);
        }

        public Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default) => Task.FromResult(_files.ContainsKey(path));
        public void ChangeExternally(string text)
        {
            var key = _files.Keys.Single();
            _files[key] = text;
            _writeUtc   = _writeUtc.AddSeconds(1);
        }
    }
}
