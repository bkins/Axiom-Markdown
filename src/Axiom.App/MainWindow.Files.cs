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
    private async Task OpenPickerAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                                                              {
                                                                  AllowMultiple  = true
                                                                , Title          = "Open Markdown"
                                                                , FileTypeFilter = [MarkdownType]
                                                              });
        foreach (var file in files)
        {
            if (file.TryGetLocalPath() is { } path)
            {
                await OpenAsync(path);
            }
        }
    }

    internal async Task OpenAsync(string path)
    {
        path = Path.GetFullPath(path);
        ValidateMarkdownPath(path);
        if (Documents.FirstOrDefault(tab => SamePath(tab.Session.Path, path)) is { } existing)
        {
            _tabs.SelectedItem = existing.Item;
            return;
        }

        AddTab(await _documents.OpenAsync(path));
    }

    private async Task SaveAsync(EditorTab? tab
                               , bool       saveAs)
    {
        if (tab is null)
        {
            return;
        }

        if (!saveAs && tab.Session.Path is { } existing)
        {
            if (!File.Exists(existing))
            {
                await SaveAsync(tab, true);
                return;
            }

            ValidateMarkdownPath(existing);
            await CheckExternalAsync(existing);
            if (tab.Session.Conflict is not null)
            {
                return;
            }

            await _documents.SaveAsync(tab.Session);
        }
        else
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                                                                 {
                                                                     Title               = "Save Markdown"
                                                                   , DefaultExtension    = "md"
                                                                   , SuggestedFileName   = tab.Session.Path is null ? "Untitled.md" : Path.GetFileName(tab.Session.Path)
                                                                   , FileTypeChoices     = [MarkdownType]
                                                                   , ShowOverwritePrompt = true
                                                                 });
            if (file?.TryGetLocalPath() is not { } path)
            {
                return;
            }

            await SaveToAsync(tab, path);
        }

        tab.Editor.Text = tab.Session.Text;
        RefreshHeader(tab);
        WatchDocuments();
        RenderPreview();
        _status.Text = "Saved " + tab.Session.Path;
    }

    internal async Task SaveToAsync(EditorTab tab
                                  , string    path)
    {
        path = Path.GetFullPath(path);
        ValidateMarkdownPath(path);
        if (WorkspacePathBoundary.Contains(_paths.Root, path, true))
        {
            throw new IOException("Choose a location outside application state.");
        }

        if (Documents.Any(other => !ReferenceEquals(other, tab) && SamePath(other.Session.Path, path)))
        {
            throw new IOException("That destination is open in another tab.");
        }

        if (SamePath(tab.Session.Path, path))
        {
            await _documents.SaveAsync(tab.Session);
        }
        else
        {
            await _documents.SaveAsAsync(tab.Session, path);
        }

        tab.Editor.Text = tab.Session.Text;
        RefreshHeader(tab);
        WatchDocuments();
    }

    private async Task SaveAllAsync()
    {
        foreach (var tab in Documents.ToArray())
        {
            await SaveAsync(tab, false);
            if (tab.Session.IsDirty)
            {
                break;
            }
        }
    }

    private async Task<bool> ResolveDirtyAsync(EditorTab tab)
    {
        if (!tab.Session.IsDirty)
        {
            return true;
        }

        var answer = await ChoiceDialog.ShowAsync(this
                                                , "Unsaved Markdown"
                                                , "Save changes to " + tab.Header.Text + " before closing?"
                                                , "Save"
                                                , "Discard changes"
                                                , "Cancel");
        if (answer == "Cancel")
        {
            return false;
        }

        if (answer == "Save")
        {
            await SaveAsync(tab, false);
            return !tab.Session.IsDirty;
        }

        await _state.DeleteDraftAsync(tab.Session.DraftKey);
        return true;
    }

    private static bool SamePath(string? first
                               , string  second)
    {
        return first is not null && Path.GetFullPath(first).Equals(Path.GetFullPath(second)
                                                                 , WorkspacePathBoundary.Comparison);
    }

    private static void ValidateMarkdownPath(string path)
    {
        if (Path.GetExtension(path).ToLowerInvariant() is not ".md" and not ".markdown")
        {
            throw new IOException("Choose a .md or .markdown file.");
        }
    }
}
