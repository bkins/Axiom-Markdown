using System.IO.Compression;
using Axiom.Core;
using Axiom.Infrastructure;
using Xunit;

namespace Axiom.Markdown.Tests;

public sealed partial class EditingAndExportTests
{
    private sealed class DeferredFiles : IDocumentFileStore
    {
        public TaskCompletionSource WriteStarted { get; } = new();
        public TaskCompletionSource Resume { get; } = new();

        public async Task<DocumentFile> WriteAsync(string            path
                                                 , string            text
                                                 , CancellationToken cancellationToken = default)
        {
            WriteStarted.SetResult();
            await Resume.Task;
            return new(path
                     , text
                     , DateTimeOffset.UtcNow
                     , text.Length);
        }

        public Task<DocumentFile> ReadAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<MarkdownDiscoveryResult> ListMarkdownFilesAsync(string            folder
                                                                  , CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new MarkdownDiscoveryResult([], []));
        }
    }
}
