using Axiom.Core;

namespace Axiom.Core.Tests;

public sealed partial class DocumentServiceTests
{
    private sealed class MemoryRecovery : IRecoveryStore
    {
        public List<RecoveryDraft> Drafts { get; } = [];

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<string> BeginSessionAsync(CancellationToken cancellationToken = default) => Task.FromResult("session");
        public Task EndSessionAsync(string            sessionId
                                  , bool              cleanExit
                                  , CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveDraftAsync(RecoveryDraft     draft
                                 , CancellationToken cancellationToken = default)
        {
            Drafts.RemoveAll(item => item.DraftKey == draft.DraftKey);
            Drafts.Add(draft);
            return Task.CompletedTask;
        }

        public Task DeleteDraftAsync(string            draftKey
                                   , CancellationToken cancellationToken = default)
        {
            Drafts.RemoveAll(item => item.DraftKey == draftKey);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<RecoveryDraft>> GetRecoverableDraftsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<RecoveryDraft>>(Drafts);
    }
}
