using Axiom.Core;
using Velopack;
using Velopack.Sources;

namespace Axiom.App;

internal sealed class VelopackUpdateClient : IAutomaticUpdateClient
{
    // Public binary distribution only. No source code or credentials are published here.
    internal const string Repository = "https://github.com/bkins/Axiom-Markdown";
    private readonly UpdateManager _manager;
    private VelopackAsset? _ready;
    public VelopackUpdateClient(AutomaticUpdatePreferences preferences)
    {
        FeatureAccess.Require(OptionalFeature.Updates);
        _manager = new UpdateManager(new GithubSource(Repository
                                                    , null
                                                    , preferences.Channel == "preview")
                                   , new UpdateOptions
                                     {
                                         ExplicitChannel       = preferences.FeedChannel
                                       , AllowVersionDowngrade = false
                                     });
    }

    internal VelopackUpdateClient(UpdateManager manager) => _manager = manager;
    public bool IsInstalled => OperatingSystem.IsWindows() && _manager.IsInstalled;

    public async Task<string?> DownloadLatestAsync(CancellationToken                   cancellationToken
                                                 , IProgress<AutomaticUpdateProgress>? progress = null)
    {
        FeatureAccess.Require(OptionalFeature.Updates);
        _ready = null;
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(new("Checking for updates…"));
        var update = await _manager.CheckForUpdatesAsync();
        cancellationToken.ThrowIfCancellationRequested();
        if (update is null)
        {
            return null;
        }

        FeatureAccess.Require(OptionalFeature.Updates);
        progress?.Report(new($"Downloading Axiom {update.TargetFullRelease.Version}…", 0));
        await _manager.DownloadUpdatesAsync(update
                                          , value => progress?.Report(new(value >= 100 ? "Verifying downloaded update…" : $"Downloading Axiom {update.TargetFullRelease.Version}… {value}%"
                                                                        , value))
                                          , cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        FeatureAccess.Require(OptionalFeature.Updates);
        _ready = update.TargetFullRelease;
        return _ready.Version.ToString();
    }

    public void ApplyAndRestart()
    {
        FeatureAccess.Require(OptionalFeature.Updates);
        _manager.ApplyUpdatesAndRestart(_ready ?? throw new InvalidOperationException("Check and download an update first."));
    }
}
