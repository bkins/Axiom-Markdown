namespace Axiom.Core;

public interface IWorkspaceStore
{
    Task<WorkspaceRecord> OpenWorkspaceAsync(string            normalizedRootPath
                                           , string            displayName
                                           , DateTimeOffset    openedUtc
                                           , CancellationToken cancellationToken = default);
    Task<WorkspaceRecord?> GetActiveWorkspaceAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkspacePin>> GetPinsAsync(Guid              workspaceId
                                                 , CancellationToken cancellationToken = default);
    Task AddPinAsync(Guid              workspaceId
                   , string            relativePath
                   , DateTimeOffset    pinnedUtc
                   , CancellationToken cancellationToken = default);
    Task RemovePinAsync(Guid workspaceId, string relativePath, CancellationToken cancellationToken = default);
}
