using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Axiom.Infrastructure;

namespace Axiom.App;

internal sealed class DocumentImportProgressWindow : Window
{
    private readonly CancellationTokenSource _cancellation;
    private Exception? _error;
    private Task _operation = Task.CompletedTask;
    private DocumentImportProgressWindow(string                  path
                                       , CancellationTokenSource cancellation)
    {
        _cancellation         = cancellation;
        Title                 = "Converting to Markdown";
        Width                 = 440;
        SizeToContent         = SizeToContent.Height;
        CanResize             = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var cancel = new Button
                     {
                         Content  = "Cancel"
                       , IsCancel = true
                     };
        AutomationProperties.SetName(cancel, "Cancel document conversion");
        Content = new StackPanel
                  {
                      Margin   = new Thickness(20)
                    , Spacing  = 14
                    , Children = 
                      {
                          new TextBlock
                          {
                              Text         = $"Converting {System.IO.Path.GetFileName(path)}…"
                            , TextWrapping = TextWrapping.Wrap
                          }
                        , new ProgressBar { IsIndeterminate = true }
                        , cancel
                      }
                  };
        cancel.Click += (_, _) => Close(null);
        Closed += (_, _) => _cancellation.Cancel();
        Opened += (_, _) => _operation = ConvertAsync(path);
    }

    private async Task ConvertAsync(string path)
    {
        DocumentImportResult? result = null;
        try
        {
            result = await new DocumentImportService().ConvertAsync(path, _cancellation.Token);
        }
        catch (OperationCanceledException)when (_cancellation.IsCancellationRequested)
        {
        }
        catch (Exception error)
        {
            _error = error;
        }

        if (IsVisible)
        {
            Close(result);
        }
    }

    internal static async Task<DocumentImportResult?> RunAsync(Window            owner
                                                             , string            path
                                                             , CancellationToken cancellation)
    {
        using var source = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        var window = new DocumentImportProgressWindow(path, source);
        var result = await window.ShowDialog<DocumentImportResult?>(owner);
        await window._operation;
        if (window._error is not null)
        {
            throw new IOException("The document could not be converted. " + window._error.Message, window._error);
        }

        return result;
    }
}
