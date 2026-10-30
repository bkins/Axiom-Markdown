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
    private void ApplyPreferences(bool save = true)
    {
        _appearance           = _preferences.Appearance;
        RequestedThemeVariant = _appearance switch
        {
            AppearancePreference.Light => ThemeVariant.Light,
            AppearancePreference.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
        var dark = _appearance == AppearancePreference.Dark || _appearance == AppearancePreference.System && ActualThemeVariant == ThemeVariant.Dark;
        Background              = new SolidColorBrush(Color.Parse(dark ? "#111827" : "#FFFFFF"));
        Foreground              = new SolidColorBrush(Color.Parse(dark ? "#E5E7EB" : "#1F2937"));
        _folderFiles.Background = new SolidColorBrush(Color.Parse(dark ? "#1F2937" : "#F3F4F6"));
        _folderFiles.Foreground = Foreground;
        ApplyShellLayout();
        ApplyShellColors(dark);
        foreach (var tab in Documents)
        {
            tab.Editor.WordWrap = _preferences.WordWrap;
            tab.Editor.FontSize = _preferences.FontSize;
            ApplyEditorColors(tab.Editor);
        }

        if (save)
        {
            new EditorPreferencesStore(_paths.Root).Save(_preferences);
        }

        RenderPreview();
    }

    private void ApplyEditorColors(TextEditor editor)
    {
        var dark = _appearance == AppearancePreference.Dark || _appearance == AppearancePreference.System && ActualThemeVariant == ThemeVariant.Dark;
        editor.Background         = new SolidColorBrush(Color.Parse(dark ? "#111827" : "#FFFFFF"));
        editor.Foreground         = new SolidColorBrush(Color.Parse(dark ? "#E5E7EB" : "#1F2937"));
        editor.SyntaxHighlighting = MarkdownHighlighting.Create(dark);
    }

    private void ApplyShellColors(bool dark)
    {
        var page = Background;
        var surface = new SolidColorBrush(Color.Parse(dark ? "#1F2937" : "#F3F4F6"));
        var border = new SolidColorBrush(Color.Parse(dark ? "#374151" : "#CBD5E1"));
        _commandBar.Background   = surface;
        _commandBar.BorderBrush  = border;
        _statusBar.Background    = page;
        _statusBar.BorderBrush   = border;
        _sidebar.BorderBrush     = border;
        _folderFiles.Background  = page;
        _outlineHost.Background  = surface;
        _outlineHost.BorderBrush = border;
        _outline.Background      = surface;
        _toolRail.Background     = surface;
        _toolRail.BorderBrush    = border;
        _noticeBar.Background    = new SolidColorBrush(Color.Parse(dark ? "#451A03" : "#FEF3C7"));
        _notice.Foreground       = new SolidColorBrush(Color.Parse(dark ? "#FDE68A" : "#78350F"));
        foreach (var splitter in _splitters)
        {
            splitter.Background = border;
        }
    }

    private async Task FeatureSettingsAsync()
    {
        var controls = new StackPanel
                       {
                           Margin  = new Thickness(18)
                         , Spacing = 10
                       };
        controls.Children.Add(new TextBlock
                              {
                                  Text         = "Feature controls — organization policy takes precedence"
                                , FontWeight   = FontWeight.Bold
                                , TextWrapping = TextWrapping.Wrap
                              });
        foreach (var feature in Enum.GetValues<OptionalFeature>())
        {
            var decision = _policy.Evaluate(feature);
            var toggle = new CheckBox
                         {
                             Content   = feature + " — " + decision.Reason
                           , IsChecked = decision.Allowed
                           , IsEnabled = !decision.Reason.Contains("organization policy"
                                                                 , StringComparison.OrdinalIgnoreCase) || decision.Allowed
                         };
            toggle.IsCheckedChanged += (_, _) =>
            {
                _policy.SetPreference(feature, toggle.IsChecked == true);
                toggle.Content = feature + " — " + _policy.Evaluate(feature).Reason;
            };
            controls.Children.Add(toggle);
        }

        controls.Children.Add(new TextBlock
                              {
                                  Text         = "Remote images always require separate document-session approval. Disabling a feature blocks its operations; its code remains in this edition. Policy: HKLM\\SOFTWARE\\AxiomMarkdown\\Policy (64-bit)."
                                , TextWrapping = TextWrapping.Wrap
                              });
        var window = new Window
                     {
                         Title                 = "Axiom Markdown — Feature controls"
                       , Width                 = 640
                       , Height                = 490
                       , WindowStartupLocation = WindowStartupLocation.CenterOwner
                       , Content               = new ScrollViewer { Content = controls }
                     };
        await window.ShowDialog(this);
        await ProtectAndCheckPolicyAsync();
        RenderPreview();
    }
}
