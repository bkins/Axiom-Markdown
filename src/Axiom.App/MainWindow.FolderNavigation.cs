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
    private async Task OpenFolderAsync()
    {
        var choices = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                                                                  {
                                                                      Title         = "Open Markdown folder"
                                                                    , AllowMultiple = false
                                                                  });
        if (choices.FirstOrDefault()?.TryGetLocalPath() is not { } root)
        {
            return;
        }

        await SetFolderAsync(root);
    }

    internal async Task SetFolderAsync(string root)
    {
        var files = await _files.ListMarkdownFilesAsync(root);
        _folder           = Path.GetFullPath(root);
        _folderLabel.Text = _folder;
        _folderPaths      = files.Files.Select(path => Path.GetRelativePath(_folder, path)).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        FilterFolder();
        _status.Text = $"{_folderPaths.Count} Markdown files; {files.SkippedDirectories.Count} inaccessible/linked folder(s) skipped.";
        RenderPreview();
    }

    private void FilterFolder()
    {
        _folderFiles.ItemsSource = _folderPaths.Where(path => string.IsNullOrEmpty(_folderFilter.Text) || path.Contains(_folderFilter.Text
                                                                                                                      , StringComparison.OrdinalIgnoreCase)).ToArray();
    }
}
