namespace Axiom.Core;

public interface IRecoveryStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<string> BeginSessionAsync(CancellationToken cancellationToken = default);
    Task EndSessionAsync(string sessionId, bool cleanExit, CancellationToken cancellationToken = default);
    Task SaveDraftAsync(RecoveryDraft draft, CancellationToken cancellationToken = default);
    Task DeleteDraftAsync(string draftKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecoveryDraft>> GetRecoverableDraftsAsync(CancellationToken cancellationToken = default);
}
