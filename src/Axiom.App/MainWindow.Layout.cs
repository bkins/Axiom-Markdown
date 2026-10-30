using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Axiom.Core;

namespace Axiom.App;

public sealed partial class MainWindow
{
    private void ApplyView()
    {
        _editorHost.IsVisible                    = _viewMode.SelectedIndex != 2;
        _preview.IsVisible                       = _viewMode.SelectedIndex != 1;
        _documentGrid.ColumnDefinitions[0].Width = _viewMode.SelectedIndex == 2 ? new GridLength(0) : new GridLength(1
                                                                                                                   , GridUnitType.Star);
        _documentGrid.ColumnDefinitions[1].Width = new GridLength(_viewMode.SelectedIndex == 0 ? 5 : 0);
        _documentGrid.ColumnDefinitions[2].Width = _viewMode.SelectedIndex == 1 ? new GridLength(0) : new GridLength(1
                                                                                                                   , GridUnitType.Star);
        RefreshDocumentChrome();
    }

    private void BuildUserInterface()
    {
        Title             = "Axiom Markdown";
        Width             = 1280;
        Height            = 800;
        MinWidth          = 1000;
        MinHeight         = 580;
        _tabs.Height      = 40;
        _tabs.ItemsSource = _items;
        _tabs.SelectionChanged += (_, _) => ActivateCurrent();
        _viewMode.SelectionChanged += (_, _) =>
        {
            if (Current is { } tab)
            {
                tab.ViewMode = _viewMode.SelectedIndex;
            }

            ApplyView();
        };
        var root = new DockPanel();
        var commands = BuildCommandBar();
        DockPanel.SetDock(commands, Dock.Top);
        root.Children.Add(commands);
        BuildNoticeBar();
        DockPanel.SetDock(_noticeBar, Dock.Top);
        root.Children.Add(_noticeBar);
        BuildStatusBar();
        DockPanel.SetDock(_statusBar, Dock.Bottom);
        root.Children.Add(_statusBar);
        _sidebar.Child = BuildFileNavigator();
        _workbench.Children.Add(_sidebar);
        _workbench.Children.Add(ShellSplitter(1));
        var documents = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        documents.Children.Add(_tabs);
        documents.Children.Add(_documentGrid);
        Grid.SetRow(_documentGrid, 1);
        _workbench.Children.Add(documents);
        Grid.SetColumn(documents, 2);
        _workbench.Children.Add(ShellSplitter(3));
        BuildOutlinePane();
        _workbench.Children.Add(_outlineHost);
        Grid.SetColumn(_outlineHost, 4);
        _workbench.Children.Add(_toolRail);
        Grid.SetColumn(_toolRail, 5);
        _documentGrid.Children.Add(_editorHost);
        _documentGrid.Children.Add(ShellSplitter(1));
        _documentGrid.Children.Add(_preview);
        Grid.SetColumn(_preview, 2);
        root.Children.Add(_workbench);
        Content = root;
        AutomationProperties.SetName(_notice, "Preview content changes and external resources");
        AutomationProperties.SetName(_folderFiles, "Markdown files in open folder");
        AutomationProperties.SetName(_tabs, "Open Markdown documents");
        AutomationProperties.SetName(_preview, "Rendered Markdown preview");
    }

    private void BuildNoticeBar()
    {
        var notices = new DockPanel();
        var actions = new WrapPanel
                      {
                          HorizontalAlignment = HorizontalAlignment.Right
                        , Children            =
                          {
                              _noticeDetails
                            , _loadRemote
                            , _revokeRemote
                          }
                      };
        DockPanel.SetDock(actions, Dock.Right);
        notices.Children.Add(actions);
        notices.Children.Add(_notice);
        _noticeBar.Child = notices;
        _noticeDetails.Click += async (_, _) => await RunAsync(async () => await ChoiceDialog.ShowAsync(this
                                                                                                      , "Preview sanitization details"
                                                                                                      , string.Join("\n"
                                                                                                                  , Current?.Report?.Changes ?? [])
                                                                                                      , "OK"));
        _loadRemote.Click += async (_, _) => await RunAsync(TrustRemoteAsync);
        _revokeRemote.Click += (_, _) =>
        {
            if (Current is not { } tab)
            {
                return;
            }

            tab.RemoteImages.Clear();
            RenderPreview();
        };
    }

    private void BuildStatusBar()
    {
        _status.TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis;
        _status.TextWrapping = Avalonia.Media.TextWrapping.NoWrap;
        _buildIdentity.Text  = ApplicationBuildIdentity.FromAssembly(typeof(MainWindow).Assembly).InformationalVersion;
        var layout = new Grid
                     {
                         RowDefinitions    = new RowDefinitions("Auto,Auto")
                       , ColumnDefinitions = new ColumnDefinitions("*,Auto")
                     };
        layout.Children.Add(_status);
        layout.Children.Add(_recoveryStatus);
        Grid.SetColumn(layout.Children[1], 1);
        layout.Children.Add(_statistics);
        Grid.SetRow(_statistics, 1);
        layout.Children.Add(_buildIdentity);
        Grid.SetRow(_buildIdentity, 1);
        Grid.SetColumn(_buildIdentity, 1);
        _statusBar.Child = layout;
        AutomationProperties.SetName(_statistics, "Source words and caret position");
        AutomationProperties.SetName(_buildIdentity, "Application version");
    }
}
