using Axiom.Core;

namespace Axiom.Core.Tests;

public sealed partial class DocumentServiceTests
{
    private sealed class MemoryMetadata : IDocumentMetadataStore
    {
        public Task RecordIdentityAsync(Guid              documentId
                                      , string            path
                                      , CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemovePathAsync(string path, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<DocumentIdentityOccurrence>> GetIdentitiesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DocumentIdentityOccurrence>>([]);
    }
}
