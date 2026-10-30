using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Axiom.Core;

namespace Axiom.App;

public sealed partial class MainWindow
{
    private Control BuildFileNavigator()
    {
        var actions = new MenuFlyout();
        var open = new MenuItem { Header = "Open folder…" };
        open.Click += async (_, _) => await RunAsync(OpenFolderAsync);
        var refresh = new MenuItem { Header = "Refresh folder" };
        refresh.Click += async (_, _) =>
        {
            if (_folder is { } folder)
            {
                await RunAsync(() => SetFolderAsync(folder));
            }
        };
        actions.Items.Add(open);
        actions.Items.Add(refresh);
        var more = new Button
                   {
                       Content = "…"
                     , Flyout  = actions
                   };
        more.Classes.Add("toolbar-icon");
        AutomationProperties.SetName(more, "Folder actions");
        var filter = IconButton("Show or hide file filter"
                              , "Filter Markdown filenames"
                              , "M3 4H21L14 12V20L10 18V12Z"
                              , () =>
        {
            _folderFilter.IsVisible = !_folderFilter.IsVisible;
            if (_folderFilter.IsVisible)
            {
                _folderFilter.Focus();
            }
            else
            {
                _folderFilter.Text = "";
            }

            return Task.CompletedTask;
        });
        var heading = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto") };
        heading.Children.Add(new TextBlock
                             {
                                 Text              = "Workspace Files"
                               , FontWeight        = FontWeight.SemiBold
                               , VerticalAlignment = VerticalAlignment.Center
                             });
        heading.Children.Add(filter);
        heading.Children.Add(more);
        Grid.SetColumn(filter, 1);
        Grid.SetColumn(more, 2);
        _folderLabel.Text             = "Open a folder to browse Markdown documents.";
        _folderLabel.FontSize         = 12;
        _folderLabel.Opacity          = 0.7;
        _folderFilter.IsVisible       = false;
        _folderFilter.PlaceholderText = "Filter files…";
        _folderFilter.TextChanged += (_, _) => FilterFolder();
        _folderFiles.ItemTemplate = new FuncTreeDataTemplate<WorkspaceTreeNode>((node, _) => new TextBlock
                                                                                             {
                                                                                                 Text         = (node.IsDocument ? "▤  " : "▸  ") + node.Name
                                                                                               , Margin       = new Thickness(0
                                                                                                                            , 3)
                                                                                               , TextTrimming = TextTrimming.CharacterEllipsis
                                                                                             }
                                                                              , node => node.Children);
        _folderFiles.Styles.Add(new Style(selector => selector.OfType<TreeViewItem>())
                                {
                                    Setters =
                                    {
                                        new Setter(TreeViewItem.IsExpandedProperty
                                                 , new Binding(nameof(WorkspaceTreeNode.IsExpanded)) { Mode = BindingMode.TwoWay })
                                    }
                                });
        _folderFiles.DoubleTapped += async (_, _) =>
        {
            if (!_suppressExplorerOpen)
            {
                await OpenSelectedFolderDocumentAsync();
            }
        };
        _folderFiles.ContextRequested += ShowExplorerContextMenu;
        InitializeExplorerDrag();
        _folderFiles.KeyDown += async (_, eventArgs) =>
        {
            if (eventArgs.Key != Key.Enter)
            {
                return;
            }

            eventArgs.Handled = true;
            await OpenSelectedFolderDocumentAsync();
        };
        var header = new StackPanel
                     {
                         Spacing  = 6
                       , Children =
                         {
                             heading
                           , _folderFilter
                           , _folderLabel
                           , new Separator()
                           , ActionButton("Open folder…"
                                        , OpenFolderAsync)
                         }
                     };
        var navigator = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        navigator.Children.Add(header);
        navigator.Children.Add(_folderFiles);
        return navigator;
    }

    private async Task OpenSelectedFolderDocumentAsync()
    {
        if (_folderFiles.SelectedItem is WorkspaceTreeNode { IsDocument: true } node && _folder is { } root)
        {
            await RunAsync(() => OpenAsync(Path.Combine(root, node.RelativePath)));
        }
    }

    private void BuildOutlinePane()
    {
        AutomationProperties.SetName(_outline, "Document outline");
        _outline.ItemTemplate = new FuncDataTemplate<DocumentHeading>((heading, _) => new TextBlock
                                                                                      {
                                                                                          Text         = heading.Text
                                                                                        , Margin       = new Thickness((heading.Level - 1) * 12
                                                                                                                     , 4
                                                                                                                     , 0
                                                                                                                     , 4)
                                                                                        , TextTrimming = TextTrimming.CharacterEllipsis
                                                                                      });
        _outline.SelectionChanged += (_, _) =>
        {
            if (_outline.SelectedItem is not DocumentHeading heading || Current is not { } tab)
            {
                return;
            }

            if (tab.Structure?.SourceText != tab.Session.Text)
            {
                RefreshOutline();
                return;
            }

            tab.Editor.ScrollToLine(heading.Source.LineNumber);
            tab.Editor.TextArea.Caret.Offset = heading.Source.Offset;
            tab.Editor.Focus();
        };
        var close = ActionButton("×", ToggleOutlineAsync);
        AutomationProperties.SetName(close, "Close document outline");
        var header = new DockPanel
                     {
                         Children =
                         {
                             close
                           , new TextBlock
                             {
                                 Text              = "Outline"
                               , FontWeight        = FontWeight.SemiBold
                               , VerticalAlignment = VerticalAlignment.Center
                             }
                         }
                     };
        DockPanel.SetDock(close, Dock.Right);
        var content = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        content.Children.Add(header);
        content.Children.Add(_outline);
        _outlineHost.Child = content;
        var outlineButton = IconButton("Toggle document outline"
                                     , "Show or hide Markdown headings"
                                     , "M4 5H20 M4 10H16 M4 15H20 M4 20H14"
                                     , ToggleOutlineAsync);
        _toolRail.Child = new StackPanel
                          {
                              Margin   = new Thickness(4)
                            , Children =
                              {
                                  outlineButton
                              }
                          };
    }

    private Task ToggleOutlineAsync()
    {
        _preferences = _preferences with

        {
            ShowOutline = !_preferences.ShowOutline
        };
        ApplyPreferences();
        return Task.CompletedTask;
    }

    private void RefreshOutline() => _outline.ItemsSource = Current?.Structure?.Headings;
}
