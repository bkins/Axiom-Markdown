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
    private void WatchDocuments() => _monitor.WatchMany(Documents.Select(tab => tab.Session.Path).OfType<string>());
    private async Task CheckExternalAsync(string path)
    {
        if (_checkingExternal)
        {
            return;
        }

        _checkingExternal = true;
        try
        {
            foreach (var tab in Documents.Where(tab => SamePath(tab.Session.Path, path)).ToArray())
            {
                if (!File.Exists(path))
                {
                    _status.Text = "File removed outside the editor; current buffer retained. Use Save As to keep a copy.";
                    continue;
                }

                if (!await _documents.CheckExternalChangeAsync(tab.Session))
                {
                    continue;
                }

                tab.RemoteImages.Clear();
                if (tab.Session.Conflict is not null)
                {
                    var choice = await ChoiceDialog.ShowAsync(this
                                                            , "File changed outside Axiom Markdown"
                                                            , "Both your buffer and the saved file changed. Keep editing without overwriting, reload the saved file, or deliberately keep your local version for the next Save."
                                                            , "Reload saved file"
                                                            , "Keep local version"
                                                            , "Keep editing");
                    if (choice == "Reload saved file")
                    {
                        tab.Session.Reload(await _files.ReadAsync(path));
                    }
                    else if (choice == "Keep local version")
                    {
                        tab.Session.KeepLocalAfterConflict();
                    }
                }

                tab.Editor.Text = tab.Session.Text;
                RefreshHeader(tab);
                RenderPreview();
            }
        }
        finally
        {
            _checkingExternal = false;
        }
    }

    private Task HandleRenameAsync(string oldPath
                                 , string newPath)
    {
        foreach (var tab in Documents.Where(tab => SamePath(tab.Session.Path, oldPath)))
        {
            tab.Session.UpdatePath(newPath);
            RefreshHeader(tab);
            tab.RemoteImages.Clear();
        }

        WatchDocuments();
        return CheckExternalAsync(newPath);
    }
}
