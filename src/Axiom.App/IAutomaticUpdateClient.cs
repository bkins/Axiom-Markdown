using Axiom.Core;
using Velopack;
using Velopack.Sources;

namespace Axiom.App;

internal interface IAutomaticUpdateClient
{
    bool IsInstalled { get; }

    Task<string?> DownloadLatestAsync(CancellationToken                   cancellationToken
                                    , IProgress<AutomaticUpdateProgress>? progress = null);
    void ApplyAndRestart();
}
