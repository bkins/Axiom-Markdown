using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Axiom.App;

internal sealed class TextEntryDialog : Window
{
    private TextEntryDialog(string title
                          , string message
                          , string initial)
    {
        Title                 = title;
        Width                 = 520;
        SizeToContent         = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var text = new TextBox { Text = initial };
        AutomationProperties.SetName(text, "Item name");
        var apply = new Button
                    {
                        Content   = "Continue"
                      , IsDefault = true
                    };
        var cancel = new Button
                     {
                         Content  = "Cancel"
                       , IsCancel = true
                     };
        apply.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(text.Text))
            {
                Close(text.Text.Trim());
            }
        };
        cancel.Click += (_, _) => Close(null);
        Content = new StackPanel
                  {
                      Margin   = new Thickness(20)
                    , Spacing  = 12
                    , Children =
                      {
                          new TextBlock
                          {
                              Text         = message
                            , TextWrapping = Avalonia.Media.TextWrapping.Wrap
                          }
                        , text
                        , new StackPanel
                          {
                              Orientation         = Orientation.Horizontal
                            , Spacing             = 8
                            , HorizontalAlignment = HorizontalAlignment.Right
                            , Children            =
                              {
                                  apply
                                , cancel
                              }
                          }
                      }
                  };
        Opened += (_, _) =>
        {
            text.Focus();
            text.SelectAll();
        };
    }

    internal static Task<string?> ShowAsync(Window owner
                                          , string title
                                          , string message
                                          , string initial)
    {
        return new TextEntryDialog(title, message, initial).ShowDialog<string?>(owner);
    }
}
