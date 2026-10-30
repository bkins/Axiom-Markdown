namespace Axiom.Core;

public interface IDocumentFileStore
{
    Task<MarkdownDiscoveryResult> ListMarkdownFilesAsync(string            folder
                                                       , CancellationToken cancellationToken = default);
    Task<DocumentFile> ReadAsync(string path, CancellationToken cancellationToken = default);
    Task<DocumentFile> WriteAsync(string path, string text, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default);
}
