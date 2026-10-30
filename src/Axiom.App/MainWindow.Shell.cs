using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Input;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Axiom.Core;

namespace Axiom.App;

public sealed partial class MainWindow
{
    private readonly Border _commandBar = new()
                                          {
                                              BorderThickness = new Thickness(0
                                                                            , 0
                                                                            , 0
                                                                            , 1)
                                            , Padding         = new Thickness(6
                                                                            , 3)
                                          };
    private readonly Border _sidebar = new()
                                       {
                                           BorderThickness = new Thickness(0
                                                                         , 0
                                                                         , 1
                                                                         , 0)
                                         , Padding         = new Thickness(8)
                                       };
    private readonly Border _statusBar = new()
                                         {
                                             BorderThickness = new Thickness(0
                                                                           , 1
                                                                           , 0
                                                                           , 0)
                                           , Padding         = new Thickness(8
                                                                           , 5)
                                         };
    private readonly Border _noticeBar = new()
                                         {
                                             Padding   = new Thickness(10
                                                                     , 6)
                                           , IsVisible = false
                                         };
    private readonly Border _outlineHost = new()
                                           {
                                               BorderThickness = new Thickness(1
                                                                             , 0
                                                                             , 0
                                                                             , 0)
                                             , Padding         = new Thickness(8)
                                           };
    private readonly Border _toolRail = new()
                                        {
                                            BorderThickness = new Thickness(1
                                                                          , 0
                                                                          , 0
                                                                          , 0)
                                        };
    private readonly Grid _workbench = new() { ColumnDefinitions = new ColumnDefinitions("280,5,*,5,0,44") };
    private readonly ListBox _outline = new();
    private readonly TextBlock _statistics = new();
    private readonly TextBlock _recoveryStatus = new()
                                                 {
                                                     Text                = "Recovery ready"
                                                   , HorizontalAlignment = HorizontalAlignment.Right
                                                 };
    private readonly TextBlock _buildIdentity = new();
    private readonly TextBlock _windowTitle = new()
                                              {
                                                  Text              = "Axiom Markdown"
                                                , VerticalAlignment = VerticalAlignment.Center
                                                , Margin            = new Thickness(12
                                                                                  , 0)
                                                , TextTrimming      = TextTrimming.CharacterEllipsis
                                              };
    private readonly StackPanel _captionButtons = new() { Orientation = Orientation.Horizontal };
    private readonly List<Button> _menuButtons = [];
    private readonly List<MenuItem> _commandGroups = [];
    private Button _mainMenuButton = null!;
    private readonly List<GridSplitter> _splitters = [];
    private Button _readingMode = null!;
    private Control BuildCommandBar()
    {
        Styles.Add(new StyleInclude(new Uri("avares://Axiom.Markdown/")) { Source = new Uri("avares://Axiom.Markdown/ShellStyles.axaml") });
        var commands = new StackPanel
                       {
                           Orientation       = Orientation.Horizontal
                         , Spacing           = 3
                         , VerticalAlignment = VerticalAlignment.Center
                       };
        commands.Children.Add(IconButton("New document"
                                       , "New unsaved document — Ctrl+N"
                                       , "M12 5V19 M5 12H19"
                                       , NewAsync));
        commands.Children.Add(IconButton("Open document"
                                       , "Open Markdown — Ctrl+O"
                                       , "M3 7H10L12 9H21V20H3Z M3 7V4H10L12 7"
                                       , OpenPickerAsync));
        commands.Children.Add(IconButton("Save document"
                                       , "Save document — Ctrl+S"
                                       , "M4 3H17L21 7V21H3V3Z M7 3V10H17V3 M7 21V14H17V21"
                                       , () => SaveAsync(Current
                                                       , false)));
        _readingMode = IconButton("Toggle reading mode"
                                , "Reading mode — Ctrl+Shift+R"
                                , "M2 12Q12 0 22 12Q12 24 2 12 M15 12A3 3 0 1 1 9 12A3 3 0 1 1 15 12"
                                , ToggleReadingModeAsync);
        commands.Children.Add(_readingMode);
        commands.Children.Add(IconButton("Application settings"
                                       , "Editor and appearance settings"
                                       , "M12 3V6 M12 18V21 M3 12H6 M18 12H21 M5 5L7 7 M17 17L19 19 M5 19L7 17 M17 7L19 5 M17 12A5 5 0 1 1 7 12A5 5 0 1 1 17 12"
                                       , ShowEditorSettingsAsync));
        var menu = new MenuFlyout();
        var groups = BuildMenu();
        foreach (var group in groups.Items.OfType<MenuItem>().ToArray())
        {
            groups.Items.Remove(group);
            _commandGroups.Add(group);
            menu.Items.Add(group);
        }

        _mainMenuButton = new Button
                          {
                              Content = new WorkbenchIcon("M4 6H20 M4 12H20 M4 18H20")
                            , Flyout  = menu
                          };
        _mainMenuButton.Classes.Add("toolbar-icon");
        _mainMenuButton.Classes.Add("quiet-menu");
        AutomationProperties.SetName(_mainMenuButton, "Main menu: File Edit Format Tools View Help");
        ToolTip.SetTip(_mainMenuButton, "Main menu — F10; Alt+F/E/M/T/V/H opens a section");
        _menuButtons.Add(_mainMenuButton);
        commands.Children.Insert(0, _mainMenuButton);
        var titleRegion = new Border
                          {
                              Child      = _windowTitle
                            , Background = Brushes.Transparent
                            , MinWidth   = 40
                          };
        WindowDecorationProperties.SetElementRole(titleRegion, WindowDecorationsElementRole.TitleBar);
        AddCaptionButton("Minimize Axiom Markdown"
                       , WindowDecorationsElementRole.MinimizeButton
                       , () => WindowState = WindowState.Minimized);
        AddCaptionButton("Maximize or restore Axiom Markdown"
                       , WindowDecorationsElementRole.MaximizeButton
                       , () => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized);
        AddCaptionButton("Close Axiom Markdown", WindowDecorationsElementRole.CloseButton, Close);
        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        layout.Children.Add(commands);
        layout.Children.Add(titleRegion);
        layout.Children.Add(_captionButtons);
        Grid.SetColumn(titleRegion, 1);
        Grid.SetColumn(_captionButtons, 2);
        _commandBar.Child = layout;
        return _commandBar;
    }

    private void AddCaptionButton(string                       name
                                , WindowDecorationsElementRole role
                                , Action                       action)
    {
        var geometry = role switch
        {
            WindowDecorationsElementRole.MinimizeButton => "M4 12H20",
            WindowDecorationsElementRole.MaximizeButton => "M5 5H19V19H5Z",
            _ => "M5 5L19 19 M19 5L5 19"
        };
        var button = new Button
                     {
                         Content                    = new WorkbenchIcon(geometry)
                                   {
                                       Width  = 12
                                     , Height = 12
                                   }
                       , HorizontalContentAlignment = HorizontalAlignment.Center
                       , VerticalContentAlignment   = VerticalAlignment.Center
                     };
        button.Classes.Add("caption");
        if (role == WindowDecorationsElementRole.CloseButton)
        {
            button.Classes.Add("close");
        }

        AutomationProperties.SetName(button, name);
        WindowDecorationProperties.SetElementRole(button, role);
        button.Click += (_, _) => action();
        _captionButtons.Children.Add(button);
    }

    private void OpenMainMenu(int section = -1)
    {
        _mainMenuButton.Focus();
        _mainMenuButton.Flyout?.ShowAt(_mainMenuButton);
        if (section < 0)
        {
            return;
        }

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _commandGroups[section].Focus();
            _commandGroups[section].IsSubMenuOpen = true;
        });
    }

    private Button IconButton(string     name
                            , string     hint
                            , string     geometry
                            , Func<Task> action)
    {
        var button = new Button { Content = new WorkbenchIcon(geometry) };
        button.Classes.Add("toolbar-icon");
        AutomationProperties.SetName(button, name);
        ToolTip.SetTip(button, hint);
        button.Click += async (_, _) => await RunAsync(action);
        return button;
    }

    private GridSplitter ShellSplitter(int column)
    {
        var splitter = new GridSplitter
                       {
                           Width               = 5
                         , HorizontalAlignment = HorizontalAlignment.Stretch
                         , ResizeDirection     = GridResizeDirection.Columns
                       };
        Grid.SetColumn(splitter, column);
        _splitters.Add(splitter);
        return splitter;
    }

    private Task ToggleReadingModeAsync()
    {
        _viewMode.SelectedIndex = _viewMode.SelectedIndex == 2 ? 0 : 2;
        return Task.CompletedTask;
    }

    private void ApplyShellLayout()
    {
        ExtendClientAreaToDecorationsHint     = _preferences.CompactTitleBar && OperatingSystem.IsWindows();
        ExtendClientAreaTitleBarHeightHint    = 38;
        WindowDecorations                     = ExtendClientAreaToDecorationsHint ? WindowDecorations.BorderOnly : WindowDecorations.Full;
        _captionButtons.IsVisible             = ExtendClientAreaToDecorationsHint;
        _sidebar.IsVisible                    = _preferences.ShowFileNavigator;
        _splitters[0].IsVisible               = _preferences.ShowFileNavigator;
        _workbench.ColumnDefinitions[0].Width = new GridLength(_preferences.ShowFileNavigator ? 280 : 0);
        _workbench.ColumnDefinitions[1].Width = new GridLength(_preferences.ShowFileNavigator ? 5 : 0);
        _outlineHost.IsVisible                = _preferences.ShowOutline;
        _splitters[1].IsVisible               = _preferences.ShowOutline;
        _workbench.ColumnDefinitions[3].Width = new GridLength(_preferences.ShowOutline ? 5 : 0);
        _workbench.ColumnDefinitions[4].Width = new GridLength(_preferences.ShowOutline ? 250 : 0);
        foreach (var button in _menuButtons)
        {
            button.Classes.Set("quiet-menu", _preferences.MenuStyle == MenuStyle.Quiet);
            button.Classes.Set("rounded-menu", _preferences.MenuStyle == MenuStyle.Rounded);
        }
    }

    private void RefreshDocumentChrome()
    {
        if (Current is not { } tab)
        {
            return;
        }

        _windowTitle.Text = tab.Header.Text + " — Axiom Markdown";
        var caret = tab.Editor.TextArea.Caret;
        var words = tab.Session.Text.Split((char[]? )null, StringSplitOptions.RemoveEmptyEntries).Length;
        _statistics.Text = $"{words:N0} words · Ln {caret.Line}, Col {caret.Column}";
        ToolTip.SetTip(_statistics, tab.Session.Path ?? "Unsaved Markdown document");
        ToolTip.SetTip(_readingMode
                     , tab.ViewMode == 2 ? "Return to split editing — Ctrl+Shift+R" : "Reading mode — Ctrl+Shift+R");
    }
}
