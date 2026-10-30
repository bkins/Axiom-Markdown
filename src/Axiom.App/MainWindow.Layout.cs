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
    private void ApplyView()
    {
        _editorHost.IsVisible                    = _viewMode.SelectedIndex != 2;
        _preview.IsVisible                       = _viewMode.SelectedIndex != 1;
        _documentGrid.ColumnDefinitions[0].Width = _viewMode.SelectedIndex == 2 ? new GridLength(0) : new GridLength(1
                                                                                                                   , GridUnitType.Star);
        _documentGrid.ColumnDefinitions[1].Width = new GridLength(_viewMode.SelectedIndex == 0 ? 5 : 0);
        _documentGrid.ColumnDefinitions[2].Width = _viewMode.SelectedIndex == 1 ? new GridLength(0) : new GridLength(1
                                                                                                                   , GridUnitType.Star);
    }

    private void BuildUserInterface()
    {
        Title             = "Axiom Markdown";
        Width             = 1340;
        Height            = 880;
        MinWidth          = 800;
        MinHeight         = 550;
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
        var menus = BuildMenu();
        var toolbar = new WrapPanel
                      {
                          Margin   = new Thickness(8
                                                 , 4)
                        , Children = 
                          {
                              ActionButton("New"
                                         , () => NewAsync())
                            , ActionButton("Open…"
                                         , OpenPickerAsync)
                            , ActionButton("Save"
                                         , () => SaveAsync(Current, false))
                            , ActionButton("Find"
                                         , () => FindAsync())
                            , _viewMode
                          }
                      };
        var notices = new DockPanel
                      {
                          Margin = new Thickness(12
                                               , 4)
                      };
        var noticeActions = new WrapPanel
                            {
                                Children            = 
                                {
                                    _noticeDetails
                                  , _loadRemote
                                  , _revokeRemote
                                }
                              , HorizontalAlignment = HorizontalAlignment.Right
                            };
        DockPanel.SetDock(noticeActions, Dock.Right);
        notices.Children.Add(noticeActions);
        notices.Children.Add(_notice);
        _noticeDetails.Click += async (_, _) => await RunAsync(async () => await ChoiceDialog.ShowAsync(this
                                                                                                      , "Preview sanitization details"
                                                                                                      , string.Join("\n"
                                                                                                                  , Current?.Report?.Changes ?? [])
                                                                                                      , "OK"));
        _loadRemote.Click += async (_, _) => await RunAsync(TrustRemoteAsync);
        _revokeRemote.Click += (_, _) =>
        {
            if (Current is { } tab)
            {
                tab.RemoteImages.Clear();
                RenderPreview();
            }
        };
        var sidebar = new Grid
                      {
                          RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*")
                        , Margin         = new Thickness(8)
                        , RowSpacing     = 8
                      };
        var openFolder = ActionButton("Open folder…", OpenFolderAsync);
        sidebar.Children.Add(openFolder);
        sidebar.Children.Add(_folderLabel);
        Grid.SetRow(_folderLabel, 1);
        sidebar.Children.Add(_folderFilter);
        Grid.SetRow(_folderFilter, 2);
        sidebar.Children.Add(_folderFiles);
        Grid.SetRow(_folderFiles, 3);
        _folderFilter.TextChanged += (_, _) => FilterFolder();
        _folderFiles.DoubleTapped += async (_, _) =>
        {
            if (_folderFiles.SelectedItem is string relative && _folder is { } root)
            {
                await RunAsync(() => OpenAsync(Path.Combine(root, relative)));
            }
        };
        _folderFiles.KeyDown += async (_, eventArgs) =>
        {
            if (eventArgs.Key == Key.Enter && _folderFiles.SelectedItem is string relative && _folder is { } root)
            {
                eventArgs.Handled = true;
                await RunAsync(() => OpenAsync(Path.Combine(root, relative)));
            }
        };
        _documentGrid.Children.Add(_editorHost);
        var previewSplitter = new GridSplitter
                              {
                                  ResizeDirection     = GridResizeDirection.Columns
                                , HorizontalAlignment = HorizontalAlignment.Stretch
                              };
        _documentGrid.Children.Add(previewSplitter);
        Grid.SetColumn(previewSplitter, 1);
        _documentGrid.Children.Add(_preview);
        Grid.SetColumn(_preview, 2);
        var documents = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        documents.Children.Add(_tabs);
        documents.Children.Add(_documentGrid);
        Grid.SetRow(_documentGrid, 1);
        var workspace = new Grid { ColumnDefinitions = new ColumnDefinitions("235,5,*") };
        workspace.Children.Add(sidebar);
        var splitter = new GridSplitter
                       {
                           HorizontalAlignment = HorizontalAlignment.Stretch
                         , ResizeDirection     = GridResizeDirection.Columns
                       };
        workspace.Children.Add(splitter);
        Grid.SetColumn(splitter, 1);
        workspace.Children.Add(documents);
        Grid.SetColumn(documents, 2);
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*,Auto") };
        root.Children.Add(menus);
        root.Children.Add(toolbar);
        Grid.SetRow(toolbar, 1);
        root.Children.Add(notices);
        Grid.SetRow(notices, 2);
        root.Children.Add(workspace);
        Grid.SetRow(workspace, 3);
        var statusBorder = new Border
                           {
                               Padding = new Thickness(10
                                                     , 5)
                             , Child   = _status
                           };
        root.Children.Add(statusBorder);
        Grid.SetRow(statusBorder, 4);
        Content = root;
        AutomationProperties.SetName(_notice, "Preview content changes and external resources");
        AutomationProperties.SetName(_folderFiles, "Markdown files in open folder");
        AutomationProperties.SetName(_tabs, "Open Markdown documents");
        AutomationProperties.SetName(_preview, "Rendered Markdown preview");
    }
}
