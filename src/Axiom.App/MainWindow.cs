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

public sealed partial class MainWindow : Window
{
    private readonly AppDataPaths _paths = new();
    private readonly EditorStateStore _state;
    private readonly FeaturePolicyStore _policy;
    private readonly FileDocumentStore _files = new(extractImportImages: true);
    private readonly DocumentService _documents;
    private readonly ExternalFileMonitor _monitor = new();
    private readonly MarkdownPreviewService _renderer = new();
    private readonly NativeHtmlContent _html;
    private readonly NativeWebView _preview = new();
    private readonly TabControl _tabs = new() { Height = 42 };
    private readonly ObservableCollection<TabItem> _items = [];
    private readonly ContentControl _editorHost = new();
    private readonly Grid _documentGrid = new() { ColumnDefinitions = new ColumnDefinitions("*,5,*") };
    private readonly TreeView _folderFiles = new();
    private readonly TextBox _folderFilter = new() { PlaceholderText = "Filter Markdown files…" };
    private readonly TextBlock _folderLabel = new()
                                              {
                                                  Text         = "Open a folder"
                                                , TextWrapping = TextWrapping.Wrap
                                              };
    private readonly TextBlock _notice = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Button _noticeDetails = new()
                                             {
                                                 Content   = "Details"
                                               , IsVisible = false
                                             };
    private readonly Button _loadRemote = new()
                                          {
                                              Content   = "Review external content…"
                                            , IsVisible = false
                                          };
    private readonly Button _revokeRemote = new()
                                            {
                                                Content   = "Block external content"
                                              , IsVisible = false
                                            };
    private readonly TextBlock _status = new()
                                         {
                                             Text         = "Ready"
                                           , TextWrapping = TextWrapping.Wrap
                                         };
    private readonly ComboBox _viewMode = new()
                                          {
                                              ItemsSource   = new[]
                                                            {
                                                                "Split"
                                                              , "Editor"
                                                              , "Preview"
                                                            }
                                            , SelectedIndex = 0
                                            , MinWidth      = 100
                                          };
    private readonly DispatcherTimer _previewTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly DispatcherTimer _protectTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly DispatcherTimer _updateTimer = new() { Interval = TimeSpan.FromHours(6) };
    private readonly CancellationTokenSource _lifetime = new();
    private readonly AutomaticUpdateService _updates = new(preferences => new VelopackUpdateClient(preferences));
    private readonly List<(Window Window, GitTerminalView View)> _terminals = [];
    private readonly List<(Window Window, WorkspaceGitView View)> _gitWindows = [];
    private readonly List<(MenuItem Item, OptionalFeature Feature)> _featureMenuItems = [];
    private readonly Queue<string?> _externalRequests = new();
    private bool _startupReady;
    private bool _openingExternal;
    private MarkdownPreferences _preferences = new();
    private CancellationTokenSource? _updateCancellation;
    private AppearancePreference _appearance = AppearancePreference.System;
    private string? _folder;
    private IReadOnlyList<string> _folderPaths = [];
    private IReadOnlyList<string> _folderDirectories = [];
    private string? _sessionId;
    private bool _ready;
    private bool _busy;
    private bool _approvedClose;
    private bool _closing;
    private bool _protecting;
    private bool _checkingExternal;
    private EditorTab? Current => (_tabs.SelectedItem as TabItem)?.Tag as EditorTab;
    private IEnumerable<EditorTab> Documents => _items.Select(item => (EditorTab)item.Tag!);

    public MainWindow()
    {
        _state  = new(_paths);
        _policy = new(_paths.Root);
        FeatureAccess.Initialize(_policy.Evaluate);
        _documents = new(_files, _state, _state);
        _html      = new(Path.Combine(_paths.Root, "PreviewHtml"));
        BuildUserInterface();
        WirePreviewEvents();
        WireLifecycleEvents();
    }

    private static FilePickerFileType MarkdownType
    {
        get
        {
            return new("Markdown") { Patterns = ["*.md", "*.markdown"] };
        }
    }
}
