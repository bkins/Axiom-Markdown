using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Axiom.Infrastructure;

namespace Axiom.App;

internal sealed class DocumentImportPreviewWindow : Window
{
    internal DocumentImportPreviewWindow(DocumentImportResult result)
    {
        Title                 = "Review converted Markdown";
        Width                 = 850;
        Height                = 650;
        MinWidth              = 540;
        MinHeight             = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        const int previewCharacters = 160000;
        var warnings = string.Join("\n", result.Warnings.Select(warning => "• " + warning));
        if (result.Markdown.Length > previewCharacters)
        {
            warnings += "\n• Preview shows the first 160,000 characters. Opening uses the full converted document.";
        }

        var open = new Button
                   {
                       Content   = "Open Markdown"
                     , IsDefault = true
                   };
        var cancel = new Button
                     {
                         Content  = "Cancel"
                       , IsCancel = true
                     };
        AutomationProperties.SetName(open, "Open converted Markdown as unsaved document");
        AutomationProperties.SetName(cancel, "Cancel document conversion preview");
        var preview = new TextBox
                      {
                          Text          = result.Markdown[..Math.Min(result.Markdown.Length, previewCharacters)]
                        , IsReadOnly    = true
                        , AcceptsReturn = true
                        , TextWrapping  = TextWrapping.Wrap
                        , FontFamily    = new FontFamily("Consolas")
                      };
        AutomationProperties.SetName(preview, "Converted Markdown preview");
        var grid = new Grid
                   {
                       Margin         = new Thickness(18)
                     , RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto")
                     , RowSpacing     = 12
                   };
        grid.Children.Add(new TextBlock
                          {
                              Text         = $"{System.IO.Path.GetFileName(result.SourcePath)} → Markdown\nReview the warnings and Markdown below. Open creates a new unsaved document; Save chooses its location."
                            , TextWrapping = TextWrapping.Wrap
                          });
        var warningView = new ScrollViewer
                          {
                              MaxHeight = 170
                            , Content   = new TextBlock
                                        {
                                            Text         = warnings
                                          , TextWrapping = TextWrapping.Wrap
                                        }
                          };
        Grid.SetRow(warningView, 1);
        grid.Children.Add(warningView);
        Grid.SetRow(preview, 2);
        grid.Children.Add(preview);
        var actions = new StackPanel
                      {
                          Orientation         = Orientation.Horizontal
                        , Spacing             = 12
                        , HorizontalAlignment = HorizontalAlignment.Right
                        , Children            = 
                          {
                              open
                            , cancel
                          }
                      };
        Grid.SetRow(actions, 3);
        grid.Children.Add(actions);
        Content = grid;
        open.Click += (_, _) => Close(true);
        cancel.Click += (_, _) => Close(false);
    }
}
