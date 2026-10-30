using Axiom.Core;
using Velopack;
using Velopack.Sources;

namespace Axiom.App;

internal sealed class AutomaticUpdateService(Func<AutomaticUpdatePreferences, IAutomaticUpdateClient> createClient)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IAutomaticUpdateClient? _readyClient;
    public string? ReadyVersion { get; private set; }
    public AutomaticUpdatePreferences? ReadyPreferences { get; private set; }

    public async Task<string> CheckAsync(AutomaticUpdatePreferences          preferences
                                       , CancellationToken                   cancellationToken
                                       , IProgress<AutomaticUpdateProgress>? progress = null)
    {
        FeatureAccess.Require(OptionalFeature.Updates);
        _ = preferences.FeedChannel;
        if (!await _gate.WaitAsync(0, cancellationToken))
        {
            return "An update check is already running.";
        }

        try
        {
            ReadyVersion     = null;
            ReadyPreferences = null;
            _readyClient     = null;
            var client = createClient(preferences);
            if (!client.IsInstalled)
            {
                return "Automatic updates require the update-enabled Axiom installer. This portable review build is unchanged.";
            }

            var version = await client.DownloadLatestAsync(cancellationToken, progress);
            if (version is null)
            {
                return $"No newer update is available on {preferences.Channel}.";
            }

            FeatureAccess.Require(OptionalFeature.Updates);
            cancellationToken.ThrowIfCancellationRequested();
            _readyClient     = client;
            ReadyPreferences = preferences;
            ReadyVersion     = version;
            return $"Axiom {version} is downloaded. Choose Restart to update when convenient.";
        }
        finally
        {
            _gate.Release();
        }
    }

    public void ApplyAndRestart(AutomaticUpdatePreferences current)
    {
        FeatureAccess.Require(OptionalFeature.Updates);
        if (_readyClient is null || ReadyPreferences != current)
        {
            throw new InvalidOperationException("Update settings changed. Check for updates again before restarting.");
        }

        _readyClient.ApplyAndRestart();
    }
}
