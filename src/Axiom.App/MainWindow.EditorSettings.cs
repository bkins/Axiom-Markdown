using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Axiom.Core;

namespace Axiom.App;

public sealed partial class MainWindow
{
    private async Task ShowEditorSettingsAsync()
    {
        var appearance = new ComboBox
                         {
                             ItemsSource  = Enum.GetValues<AppearancePreference>()
                           , SelectedItem = _preferences.Appearance
                           , MinWidth     = 200
                         };
        var menuStyle = new ComboBox
                        {
                            ItemsSource  = Enum.GetValues<MenuStyle>()
                          , SelectedItem = _preferences.MenuStyle
                          , MinWidth     = 200
                        };
        var font = new NumericUpDown
                   {
                       Minimum   = 10
                     , Maximum   = 32
                     , Value     = (decimal)_preferences.FontSize
                     , Increment = 1
                   };
        var wrap = new CheckBox
                   {
                       Content   = "Wrap editor lines"
                     , IsChecked = _preferences.WordWrap
                   };
        var titleBar = new CheckBox
                       {
                           Content   = "Compact title and command bar"
                         , IsChecked = _preferences.CompactTitleBar
                       };
        var navigator = new CheckBox
                        {
                            Content   = "Show file navigator"
                          , IsChecked = _preferences.ShowFileNavigator
                        };
        var outline = new CheckBox
                      {
                          Content   = "Show document outline"
                        , IsChecked = _preferences.ShowOutline
                      };
        AutomationProperties.SetName(appearance, "Appearance theme");
        AutomationProperties.SetName(menuStyle, "Menu button style");
        AutomationProperties.SetName(font, "Editor font size");
        var window = new Window
                     {
                         Title                 = "Axiom Markdown — Settings"
                       , Width                 = 440
                       , Height                = 570
                       , MinWidth              = 400
                       , MinHeight             = 500
                       , WindowStartupLocation = WindowStartupLocation.CenterOwner
                       , RequestedThemeVariant = RequestedThemeVariant
                     };
        var apply = new Button
                    {
                        Content   = "Apply"
                      , IsDefault = true
                    };
        var cancel = new Button
                     {
                         Content  = "Cancel"
                       , IsCancel = true
                     };
        cancel.Click += (_, _) => window.Close();
        apply.Click += (_, _) =>
        {
            _preferences = _preferences with

            {
                Appearance        = (AppearancePreference)appearance.SelectedItem!
              , MenuStyle         = (MenuStyle)menuStyle.SelectedItem!
              , FontSize          = (double)(font.Value ?? 15)
              , WordWrap          = wrap.IsChecked == true
              , CompactTitleBar   = titleBar.IsChecked == true
              , ShowFileNavigator = navigator.IsChecked == true
              , ShowOutline       = outline.IsChecked == true
            };
            ApplyPreferences();
            window.Close();
        };
        window.Content = new ScrollViewer
                         {
                             Content = new StackPanel
                                       {
                                           Margin   = new Thickness(20)
                                         , Spacing  = 10
                                         , Children =
                                           {
                                               new TextBlock
                                               {
                                                   Text     = "Editor and appearance"
                                                 , FontSize = 20
                                               }
                                             , new TextBlock { Text = "Theme" }
                                             , appearance
                                             , new TextBlock { Text = "Menu buttons" }
                                             , menuStyle
                                             , new TextBlock { Text = "Editor font size" }
                                             , font
                                             , wrap
                                             , titleBar
                                             , navigator
                                             , outline
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
                                       }
                         };
        await window.ShowDialog(this);
    }
}
