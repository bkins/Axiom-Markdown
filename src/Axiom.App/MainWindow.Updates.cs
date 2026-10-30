using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using Axiom.Core;
using Axiom.Infrastructure;

namespace Axiom.App;

public sealed partial class MainWindow
{
    private async Task CheckUpdatesAsync(bool showResult)
    {
        if (!FeatureAccess.Evaluate(OptionalFeature.Updates).Allowed)
        {
            if (showResult)
            {
                FeatureAccess.Require(OptionalFeature.Updates);
            }

            return;
        }

        _updateCancellation?.Cancel();
        _updateCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        try
        {
            var message = await _updates.CheckAsync(new()
                                                  , _updateCancellation.Token
                                                  , new Progress<AutomaticUpdateProgress>(progress => _status.Text = progress.Message));
            _status.Text = message;
            if (showResult)
            {
                await ChoiceDialog.ShowAsync(this
                                           , "Updates"
                                           , message
                                           , "OK");
            }
        }
        catch (Exception error)when (!showResult)
        {
            _status.Text = "Update check: " + error.Message;
        }
    }

    private async Task ApplyUpdateAsync()
    {
        FeatureAccess.Require(OptionalFeature.Updates);
        if (_updates.ReadyVersion is null)
        {
            throw new IOException("No update is downloaded. Check for updates first.");
        }

        if (Documents.Any(tab => tab.Session.IsDirty))
        {
            throw new IOException("Save or close unsaved documents before restarting to update.");
        }

        if (await ChoiceDialog.ShowAsync(this
                                       , "Restart to update?"
                                       , "Install downloaded version " + _updates.ReadyVersion + " and restart?"
                                       , "Restart"
                                       , "Cancel") == "Restart")
        {
            _updates.ApplyAndRestart(new());
        }
    }
}
