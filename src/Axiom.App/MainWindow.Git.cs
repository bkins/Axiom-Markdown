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
    private async Task OpenGitAsync()
    {
        FeatureAccess.Require(OptionalFeature.Git);
        var folder = _folder ?? (Current?.Session.Path is { } path ? Path.GetDirectoryName(path) : null) ?? throw new IOException("Open a folder or saved document first.");
        var service = new GitWorkspaceService();
        var snapshot = await service.InspectAsync(folder) ?? throw new IOException("This folder is not in a Git repository.");
        var window = new Window
                     {
                         Title                 = "Axiom Markdown — Git changes"
                       , Width                 = 880
                       , Height                = 760
                       , WindowStartupLocation = WindowStartupLocation.CenterOwner
                     };
        bool Valid() => FeatureAccess.Evaluate(OptionalFeature.Git).Allowed && Directory.Exists(folder);
        bool Dirty(string relative)
        {
            return Documents.Any(tab => tab.Session.Path is { } path && SamePath(path
                                                                               , Path.Combine(snapshot.RepositoryRoot
                                                                                            , relative)) && (tab.Session.IsDirty || tab.Session.Conflict is not null));
        }

        var view = new WorkspaceGitView(window
                                      , service
                                      , snapshot
                                      , Dirty
                                      , Valid
                                      , window.Close);
        window.Content = view;
        _gitWindows.Add((window, view));
        window.Closed += (_, _) =>
        {
            view.CancelOperation();
            _gitWindows.RemoveAll(item => item.Window == window);
        };
        window.Show(this);
    }

    private async Task OpenHistoryAsync()
    {
        FeatureAccess.Require(OptionalFeature.Git);
        if (Current is not { Session.Path: { } path } tab)
        {
            throw new IOException("Save the document before browsing its history.");
        }

        var service = new GitDocumentHistoryService();
        var location = await service.LocateAsync(path) ?? throw new IOException("This document is not in a Git repository.");
        var revisions = await service.ListAsync(location);
        var selected = await new DocumentHistoryWindow(service
                                                     , location
                                                     , revisions
                                                     , tab.Session.Text
                                                     , tab.Session.SavedText
                                                     , tab.Session.Conflict?.ExternalText).ShowDialog<HistoricalRestoreRequest?>(this);
        if (selected is null)
        {
            return;
        }

        FeatureAccess.Require(OptionalFeature.Git);
        if (tab.Session.Conflict is not null)
        {
            throw new IOException("Resolve the external-change conflict before restoring history.");
        }

        if (!await ResolveDirtyAsync(tab))
        {
            return;
        }

        if (await ChoiceDialog.ShowAsync(this
                                       , "Restore revision into editor?"
                                       , selected.Revision.CommitHash + "\nThe saved file remains unchanged until Save. Undo can restore the previous buffer."
                                       , "Restore"
                                       , "Cancel") != "Restore")
        {
            return;
        }

        FeatureAccess.Require(OptionalFeature.Git);
        var text = new RevisionRestoreService().Prepare(tab.Session.Text, selected.Text);
        tab.Editor.Document.Replace(0, tab.Editor.Document.TextLength, text);
        tab.RemoteImages.Clear();
        await _documents.ProtectAsync(tab.Session);
        RenderPreview();
    }

    private async Task InitializeGitAsync()
    {
        FeatureAccess.Require(OptionalFeature.Git);
        var root = _folder ?? throw new IOException("Open a folder first.");
        if (await ChoiceDialog.ShowAsync(this
                                       , "Set up local Git history?"
                                       , "Create .git in " + root + "? No remote or automatic commits will be created."
                                       , "Create repository"
                                       , "Cancel") != "Create repository")
        {
            return;
        }

        await new GitDocumentHistoryService().InitializeAsync(root);
        _status.Text = "Local Git history initialized. Save and checkpoint documents deliberately.";
    }

    private async Task CheckpointAsync()
    {
        FeatureAccess.Require(OptionalFeature.Git);
        if (Current is not { Session.Path: { } path } tab || tab.Session.IsDirty || tab.Session.Conflict is not null)
        {
            throw new IOException("Save the document and resolve conflicts before checkpointing.");
        }

        var service = new GitDocumentHistoryService();
        var location = await service.LocateAsync(path) ?? throw new IOException("Set up local Git history first.");
        var bytes = await service.ReviewCheckpointAsync(location);
        if (await ChoiceDialog.ShowAsync(this
                                       , "Checkpoint saved document?"
                                       , "Create a separate document-history checkpoint for " + path + "? No branch commit, staging or network action occurs."
                                       , "Checkpoint"
                                       , "Cancel") != "Checkpoint")
        {
            return;
        }

        if (tab.Session.IsDirty || tab.Session.Conflict is not null)
        {
            throw new IOException("The buffer changed; save and review again.");
        }

        await service.CheckpointAsync(location, bytes);
        _status.Text = "Saved document checkpoint created.";
    }

    private async Task OpenTerminalAsync()
    {
        FeatureAccess.Require(OptionalFeature.Terminal);
        if (!OperatingSystem.IsWindows())
        {
            throw new IOException("Embedded terminal currently requires Windows.");
        }

        var folder = _folder ?? (Current?.Session.Path is { } path ? Path.GetDirectoryName(path) : null) ?? throw new IOException("Open a folder first.");
        if (await ChoiceDialog.ShowAsync(this
                                       , "Open embedded terminal?"
                                       , "The terminal can execute commands with your Windows account's permissions, including Git commands. Only run commands you understand and trust.\n\nFolder: " + folder
                                       , "Open terminal"
                                       , "Cancel") != "Open terminal")
        {
            return;
        }

        FeatureAccess.Require(OptionalFeature.Terminal);
        var window = new Window
                     {
                         Title                 = "Axiom Markdown — Terminal"
                       , Width                 = 1000
                       , Height                = 650
                       , WindowStartupLocation = WindowStartupLocation.CenterOwner
                     };
        var view = new GitTerminalView(folder
                                     , () => FeatureAccess.Evaluate(OptionalFeature.Terminal).Allowed && Directory.Exists(folder));
        window.Content = view;
        _terminals.Add((window, view));
        window.Closed += async (_, _) =>
        {
            _terminals.RemoveAll(item => item.Window == window);
            await view.DisposeAsync();
        };
        window.Show(this);
    }
}
