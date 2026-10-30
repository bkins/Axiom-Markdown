namespace Axiom.Core;

public interface IDocumentMetadataStore
{
    Task RecordIdentityAsync(Guid documentId, string path, CancellationToken cancellationToken = default);
    Task RemovePathAsync(string path, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DocumentIdentityOccurrence>> GetIdentitiesAsync(CancellationToken cancellationToken = default);
}
