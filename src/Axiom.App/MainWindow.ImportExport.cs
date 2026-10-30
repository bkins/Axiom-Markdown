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
    private async Task ImportAsync()
    {
        FeatureAccess.Require(OptionalFeature.Import);
        var choices = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                                                                {
                                                                    Title          = "Import as Markdown"
                                                                  , FileTypeFilter = [new("HTML and Word")
                                                                    {
                                                                        Patterns = ["*.html", "*.htm", "*.docx"]
                                                                    }]
                                                                });
        if (choices.FirstOrDefault()?.TryGetLocalPath() is not { } path)
        {
            return;
        }

        var result = await DocumentImportProgressWindow.RunAsync(this, path, _lifetime.Token);
        if (result is null || !await new DocumentImportPreviewWindow(result).ShowDialog<bool>(this))
        {
            return;
        }

        FeatureAccess.Require(OptionalFeature.Import);
        var session = _documents.NewDocument();
        session.UpdateText(result.Markdown);
        AddTab(session);
        await _documents.ProtectAsync(session);
        _status.Text = "Imported into a new unsaved document. " + string.Join(" ", result.Warnings);
    }

    private async Task ExportAsync()
    {
        FeatureAccess.Require(OptionalFeature.Export);
        if (Current is not { } tab)
        {
            return;
        }

        var snapshot = new ExportSnapshot(tab.Session.Text, tab.Session.Path);
        var types = new[]
        {
            ("HTML", "html")
          , ("Word", "docx")
          , ("PDF", "pdf")
          , ("Text", "txt")
          , ("EPUB", "epub")
          , ("OpenDocument", "odt")
        }.Select(item => new FilePickerFileType(item.Item1) { Patterns = ["*." + item.Item2] }).ToArray();
        var selected = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                                                                 {
                                                                     Title               = "Export current buffer"
                                                                   , FileTypeChoices     = types
                                                                   , SuggestedFileName   = Path.GetFileNameWithoutExtension(tab.Session.Path ?? "Untitled") + ".html"
                                                                   , ShowOverwritePrompt = true
                                                                 });
        if (selected?.TryGetLocalPath() is not { } destination)
        {
            return;
        }

        var replace = File.Exists(destination);
        if (replace && await ChoiceDialog.ShowAsync(this
                                                  , "Replace exported file?"
                                                  , destination
                                                  , "Replace"
                                                  , "Cancel") != "Replace")
        {
            return;
        }

        var result = await new DocumentExportService(new NativePdfExportRenderer(_paths.Root)).WriteAsync(snapshot
                                                                                                        , destination
                                                                                                        , replace
                                                                                                        , Documents.Select(doc => doc.Session.Path).OfType<string>()
                                                                                                        , _paths.Root
                                                                                                        , _lifetime.Token);
        await ChoiceDialog.ShowAsync(this
                                   , "Export complete"
                                   , result.Path + (result.Warnings.Count > 0 ? "\n\nContent changes and limitations:\n" + string.Join("\n"
                                                                                                                                     , result.Warnings) : "")
                                   , "OK");
    }
}
