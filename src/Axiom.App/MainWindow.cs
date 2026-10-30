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

public sealed class MainWindow : Window
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
    private readonly ListBox _folderFiles = new();
    private readonly TextBox _folderFilter = new() { PlaceholderText = "Filter Markdown files…" };
    private readonly TextBlock _folderLabel = new() { Text = "Open a folder", TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _notice = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Button _noticeDetails = new() { Content = "Details", IsVisible = false };
    private readonly Button _loadRemote = new() { Content = "Review external content…", IsVisible = false };
    private readonly Button _revokeRemote = new() { Content = "Block external content", IsVisible = false };
    private readonly TextBlock _status = new() { Text = "Ready", TextWrapping = TextWrapping.Wrap };
    private readonly ComboBox _viewMode = new() { ItemsSource = new[] { "Split", "Editor", "Preview" }, SelectedIndex = 0, MinWidth = 100 };
    private readonly DispatcherTimer _previewTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly DispatcherTimer _protectTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly DispatcherTimer _updateTimer = new() { Interval = TimeSpan.FromHours(6) };
    private readonly CancellationTokenSource _lifetime = new();
    private readonly AutomaticUpdateService _updates = new(preferences => new VelopackUpdateClient(preferences));
    private readonly List<(Window Window, GitTerminalView View)> _terminals = [];
    private readonly List<(Window Window, WorkspaceGitView View)> _gitWindows = [];
    private readonly List<(MenuItem Item, OptionalFeature Feature)> _featureMenuItems = [];
    private readonly Queue<string?> _externalRequests = new();
    private bool _startupReady, _openingExternal;
    private MarkdownPreferences _preferences = new();
    private CancellationTokenSource? _updateCancellation;
    private AppearancePreference _appearance = AppearancePreference.System;
    private string? _folder;
    private IReadOnlyList<string> _folderPaths = [];
    private string? _sessionId;
    private bool _ready, _busy, _approvedClose, _closing, _protecting, _checkingExternal;
    private EditorTab? Current => (_tabs.SelectedItem as TabItem)?.Tag as EditorTab;
    private IEnumerable<EditorTab> Documents => _items.Select(item => (EditorTab)item.Tag!);

    public MainWindow()
    {
        _state = new(_paths);
        _policy = new(_paths.Root);
        FeatureAccess.Initialize(_policy.Evaluate);
        _documents = new(_files, _state, _state);
        _html = new(Path.Combine(_paths.Root, "PreviewHtml"));
        Title = "Axiom Markdown";
        Width = 1340; Height = 880; MinWidth = 800; MinHeight = 550;
        _tabs.ItemsSource = _items;
        _tabs.SelectionChanged += (_, _) => ActivateCurrent();
        _viewMode.SelectionChanged += (_, _) => { if (Current is { } tab) tab.ViewMode = _viewMode.SelectedIndex; ApplyView(); };
        var menus = BuildMenu();
        var toolbar = new WrapPanel { Margin = new Thickness(8, 4), Children = { ActionButton("New", () => NewAsync()), ActionButton("Open…", OpenPickerAsync), ActionButton("Save", () => SaveAsync(Current, false)), ActionButton("Find", () => FindAsync()), _viewMode } };
        var notices = new DockPanel { Margin = new Thickness(12, 4) };
        var noticeActions = new WrapPanel { Children = { _noticeDetails, _loadRemote, _revokeRemote }, HorizontalAlignment = HorizontalAlignment.Right };
        DockPanel.SetDock(noticeActions, Dock.Right); notices.Children.Add(noticeActions); notices.Children.Add(_notice);
        _noticeDetails.Click += async (_, _) => await RunAsync(async () => await ChoiceDialog.ShowAsync(this, "Preview sanitization details", string.Join("\n", Current?.Report?.Changes ?? []), "OK"));
        _loadRemote.Click += async (_, _) => await RunAsync(TrustRemoteAsync);
        _revokeRemote.Click += (_, _) => { if (Current is { } tab) { tab.RemoteImages.Clear(); RenderPreview(); } };
        var sidebar = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*"), Margin = new Thickness(8), RowSpacing = 8 };
        var openFolder = ActionButton("Open folder…", OpenFolderAsync);
        sidebar.Children.Add(openFolder); sidebar.Children.Add(_folderLabel); Grid.SetRow(_folderLabel, 1);
        sidebar.Children.Add(_folderFilter); Grid.SetRow(_folderFilter, 2);
        sidebar.Children.Add(_folderFiles); Grid.SetRow(_folderFiles, 3);
        _folderFilter.TextChanged += (_, _) => FilterFolder();
        _folderFiles.DoubleTapped += async (_, _) => { if (_folderFiles.SelectedItem is string relative && _folder is { } root) await RunAsync(() => OpenAsync(Path.Combine(root, relative))); };
        _folderFiles.KeyDown += async (_, e) => { if (e.Key == Key.Enter && _folderFiles.SelectedItem is string relative && _folder is { } root) { e.Handled = true; await RunAsync(() => OpenAsync(Path.Combine(root, relative))); } };
        _documentGrid.Children.Add(_editorHost);
        var previewSplitter = new GridSplitter { ResizeDirection = GridResizeDirection.Columns, HorizontalAlignment = HorizontalAlignment.Stretch };
        _documentGrid.Children.Add(previewSplitter); Grid.SetColumn(previewSplitter, 1);
        _documentGrid.Children.Add(_preview); Grid.SetColumn(_preview, 2);
        var documents = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        documents.Children.Add(_tabs); documents.Children.Add(_documentGrid); Grid.SetRow(_documentGrid, 1);
        var workspace = new Grid { ColumnDefinitions = new ColumnDefinitions("235,5,*") };
        workspace.Children.Add(sidebar);
        var splitter = new GridSplitter { HorizontalAlignment = HorizontalAlignment.Stretch, ResizeDirection = GridResizeDirection.Columns };
        workspace.Children.Add(splitter); Grid.SetColumn(splitter, 1); workspace.Children.Add(documents); Grid.SetColumn(documents, 2);
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*,Auto") };
        root.Children.Add(menus); root.Children.Add(toolbar); Grid.SetRow(toolbar, 1);
        root.Children.Add(notices); Grid.SetRow(notices, 2); root.Children.Add(workspace); Grid.SetRow(workspace, 3);
        var statusBorder = new Border { Padding = new Thickness(10, 5), Child = _status };
        root.Children.Add(statusBorder); Grid.SetRow(statusBorder, 4); Content = root;
        AutomationProperties.SetName(_notice, "Preview content changes and external resources");
        AutomationProperties.SetName(_folderFiles, "Markdown files in open folder");
        AutomationProperties.SetName(_tabs, "Open Markdown documents");
        AutomationProperties.SetName(_preview, "Rendered Markdown preview");
        _preview.EnvironmentRequested += (_, e) => { if (e is WindowsWebView2EnvironmentRequestedEventArgs windows) windows.UserDataFolder = Path.Combine(_paths.Root, "PreviewCache"); };
        _preview.AdapterCreated += (_, _) => { HardenPreview(); _ready = true; RenderPreview(); };
        _preview.NavigationStarted += (_, e) =>
        {
            if (e.Request is not { } uri) return;
            if (uri.Scheme == "axiom-destination" || uri.Scheme == "axiom-link" || uri.Scheme == "axiom-source" || uri.Scheme == "axiom-task" || uri.Scheme is "http" or "https" or "mailto" || uri.Fragment.Length > 0)
            { e.Cancel = true; Dispatcher.UIThread.Post(async () => await RunAsync(() => FollowLinkAsync(uri))); return; }
            if (uri.Scheme is not "about" and not "data" && !_html.Allows(uri)) e.Cancel = true;
        };
        _preview.NewWindowRequested += (_, e) => e.Handled = true;
        _preview.NavigationCompleted += async (_, e) =>
        {
            if (!e.IsSuccess || Current is not { } tab) return;
            try { var token = tab.Token; await _preview.InvokeScript(MermaidPreviewRenderer.BuildScript(token, _appearance == AppearancePreference.Dark)); await ObserveDiagramChangesAsync(tab, token); }
            catch (Exception error) { _status.Text = "Diagram rendering unavailable: " + error.Message; }
        };
        _previewTimer.Tick += (_, _) => { _previewTimer.Stop(); RenderPreview(); };
        _protectTimer.Tick += async (_, _) => { if (!_busy) { await ProtectAndCheckPolicyAsync(); await DrainExternalOpensAsync(); } };
        _updateTimer.Tick += async (_, _) => { if (!_busy && !_closing) await CheckUpdatesAsync(false); };
        _monitor.Changed += (_, path) => Dispatcher.UIThread.Post(async () => await RunAsync(() => CheckExternalAsync(path)));
        _monitor.Renamed += (_, pair) => Dispatcher.UIThread.Post(async () => await RunAsync(() => HandleRenameAsync(pair.OldPath, pair.NewPath)));
        Activated += async (_, _) => { if (_sessionId is not null) foreach (var path in Documents.Select(tab => tab.Session.Path).OfType<string>().ToArray()) await RunAsync(() => CheckExternalAsync(path)); };
        AddHandler(KeyDownEvent, OnKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        Closing += OnClosing;
        Closed += async (_, _) =>
        {
            _lifetime.Cancel(); _updateCancellation?.Cancel(); _protectTimer.Stop(); _previewTimer.Stop(); _updateTimer.Stop(); _monitor.Dispose(); _html.Dispose();
            foreach (var terminal in _terminals.ToArray()) { await terminal.View.DisposeAsync(); terminal.Window.Close(); }
            foreach (var git in _gitWindows.ToArray()) git.Window.Close();
        };
        Opened += async (_, _) => await RunAsync(InitializeAsync);
    }

    private Menu BuildMenu()
    {
        var menu = new Menu();
        MenuItem Group(string label) { var group = new MenuItem { Header = label }; menu.Items.Add(group); return group; }
        void Item(MenuItem group, string label, Func<Task> action, OptionalFeature? feature = null) { var item = new MenuItem { Header = label }; item.Click += async (_, _) => await RunAsync(action); group.Items.Add(item); if (feature is { } value) _featureMenuItems.Add((item, value)); }
        var file = Group("_File");
        Item(file, "New     Ctrl+N", () => NewAsync()); Item(file, "Open…     Ctrl+O", OpenPickerAsync); Item(file, "Open folder…", OpenFolderAsync);
        Item(file, "Save     Ctrl+S", () => SaveAsync(Current, false)); Item(file, "Save As…", () => SaveAsync(Current, true));
        Item(file, "Save all     Ctrl+Shift+S", SaveAllAsync); Item(file, "Import HTML or Word…", ImportAsync, OptionalFeature.Import); Item(file, "Export…", ExportAsync, OptionalFeature.Export);
        Item(file, "Close document     Ctrl+W", () => CloseTabAsync(Current)); Item(file, "Exit", () => { Close(); return Task.CompletedTask; });
        var edit = Group("_Edit");
        Item(edit, "Undo     Ctrl+Z", () => { Current?.Editor.Undo(); return Task.CompletedTask; }); Item(edit, "Redo     Ctrl+Y", () => { Current?.Editor.Redo(); return Task.CompletedTask; });
        Item(edit, "Find and replace…     Ctrl+F", FindAsync);
        var format = Group("F_ormat");
        foreach (var (label, kind) in new[] { ("Bold", MarkdownEditKind.Bold), ("Italic", MarkdownEditKind.Italic), ("Heading 1", MarkdownEditKind.Heading1), ("Heading 2", MarkdownEditKind.Heading2), ("Bulleted list", MarkdownEditKind.UnorderedList), ("Numbered list", MarkdownEditKind.OrderedList), ("Task list", MarkdownEditKind.TaskList), ("Link", MarkdownEditKind.Link), ("Code block", MarkdownEditKind.FencedCode), ("Table", MarkdownEditKind.Table), ("Hard line break", MarkdownEditKind.HardBreak) })
            Item(format, label, () => { Format(kind); return Task.CompletedTask; });
        var tools = Group("_Tools"); Item(tools, "Git changes…", OpenGitAsync, OptionalFeature.Git); Item(tools, "Document history…", OpenHistoryAsync, OptionalFeature.Git); Item(tools, "Set up local Git history…", InitializeGitAsync, OptionalFeature.Git); Item(tools, "Checkpoint saved document…", CheckpointAsync, OptionalFeature.Git); Item(tools, "Embedded terminal…", OpenTerminalAsync, OptionalFeature.Terminal);
        Item(tools, "Feature controls and policy…", FeatureSettingsAsync); Item(tools, "Check for updates…", () => CheckUpdatesAsync(true), OptionalFeature.Updates);
        Item(tools, "Restart to apply downloaded update…", ApplyUpdateAsync, OptionalFeature.Updates);
        var view = Group("_View");
        Item(view, "Toggle word wrap", () => { _preferences = _preferences with { WordWrap = !_preferences.WordWrap }; ApplyPreferences(); return Task.CompletedTask; });
        Item(view, "Larger editor font", () => { _preferences = _preferences with { FontSize = Math.Min(32, _preferences.FontSize + 1) }; ApplyPreferences(); return Task.CompletedTask; });
        Item(view, "Smaller editor font", () => { _preferences = _preferences with { FontSize = Math.Max(10, _preferences.FontSize - 1) }; ApplyPreferences(); return Task.CompletedTask; });
        foreach (var appearance in Enum.GetValues<AppearancePreference>()) Item(view, appearance + " theme", () => { _preferences = _preferences with { Appearance = appearance }; ApplyPreferences(); return Task.CompletedTask; });
        var help = Group("_Help");
        Item(help, "About and local data", async () => await ChoiceDialog.ShowAsync(this, "Axiom Markdown", $"Axiom Markdown {ApplicationBuildIdentity.FromAssembly(typeof(MainWindow).Assembly).InformationalVersion}\nMarkdown editing with optional organization-controlled tools.\nLocal drafts, preferences and webview cache: {_paths.Root}\nThis edition has its own state and update feed.\nSee the repository User Guide and security review packet for policy and limitations.", "OK"));
        return menu;
    }

    private Button ActionButton(string label, Func<Task> action)
    {
        var button = new Button { Content = label, Margin = new Thickness(0, 0, 6, 3) };
        button.Click += async (_, _) => await RunAsync(action); return button;
    }
    private async Task RunAsync(Func<Task> action)
    {
        if (_busy || _closing) return;
        _busy = true;
        try { await action(); }
        catch (OperationCanceledException) { _status.Text = "Cancelled; current work retained."; }
        catch (Exception error) { _status.Text = error.Message; await ChoiceDialog.ShowAsync(this, "Operation not completed", error.Message, "OK"); }
        finally { _busy = false; }
    }

    private async Task InitializeAsync()
    {
        await _state.InitializeAsync(); _sessionId = await _state.BeginSessionAsync();
        try { _preferences = new EditorPreferencesStore(_paths.Root).Load(); } catch (Exception error) { _status.Text = "Default editor preferences used: " + error.Message; }
        ApplyPreferences(save: false);
        ActualThemeVariantChanged += (_, _) => ApplyPreferences(save: false);
        foreach (var draft in await _state.GetRecoverableDraftsAsync())
        {
            var answer = await ChoiceDialog.ShowAsync(this, "Recover unsaved Markdown", $"Recover changes from {draft.FilePath ?? "an unsaved document"}? Your saved file will not be changed.", "Recover", "Discard draft", "Keep for later");
            if (answer == "Discard draft") { await _state.DeleteDraftAsync(draft.DraftKey); continue; }
            if (answer != "Recover") continue;
            var saved = draft.FilePath is { } path && File.Exists(path) ? (await _files.ReadAsync(path)).Text : "";
            AddTab(DocumentSession.Recover(draft, saved));
        }
        if (Program.StartupDocument is { } startup) await OpenAsync(startup);
        if (_items.Count == 0) AddTab(_documents.NewDocument());
        _protectTimer.Start();
        RefreshFeatureMenus();
        _startupReady = true;
        if (Program.ProbeRoot is { } probe) { Dispatcher.UIThread.Post(async () => await ReviewProbe.RunAsync(this, probe)); return; }
        if (FeatureAccess.Evaluate(OptionalFeature.Updates).Allowed) _ = CheckUpdatesAsync(false);
        _updateTimer.Start();
    }
    private Task NewAsync() { AddTab(_documents.NewDocument()); return Task.CompletedTask; }
    internal void QueueExternalOpen(string? path) => _externalRequests.Enqueue(path);
    private async Task DrainExternalOpensAsync()
    {
        if (!_startupReady || _busy || _closing || _openingExternal) return;
        _openingExternal = true;
        try
        {
            while (!_busy && !_closing && _externalRequests.TryDequeue(out var path))
                await RunAsync(async () => { if (path is not null) await OpenAsync(path); if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal; Activate(); });
        }
        finally { _openingExternal = false; }
    }
    private EditorTab AddTab(DocumentSession session)
    {
        var editor = new TextEditor { Text = session.Text, ShowLineNumbers = true, WordWrap = _preferences.WordWrap, FontFamily = new FontFamily("Cascadia Mono,Consolas,monospace"), FontSize = _preferences.FontSize, Padding = new Thickness(12) };
        editor.Options.EnableHyperlinks = false;
        editor.Options.EnableEmailHyperlinks = false;
        ApplyEditorColors(editor);
        AutomationProperties.SetName(editor, "Markdown source editor");
        var tab = new EditorTab(session, editor);
        var close = new Button { Content = "×", Padding = new Thickness(5, 0), Margin = new Thickness(7, 0, 0, 0) };
        AutomationProperties.SetName(close, "Close document");
        close.Click += async (_, e) => { e.Handled = true; await RunAsync(() => CloseTabAsync(tab)); };
        var label = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        tab.Header = label;
        var item = new TabItem { Header = new StackPanel { Orientation = Orientation.Horizontal, Children = { label, close } }, Tag = tab };
        editor.TextChanged += (_, _) => { session.UpdateText(editor.Text); RefreshHeader(tab); if (ReferenceEquals(Current, tab)) { _previewTimer.Stop(); _previewTimer.Start(); } };
        tab.Item = item; _items.Add(item); _tabs.SelectedItem = item; RefreshHeader(tab); WatchDocuments(); return tab;
    }
    private void ActivateCurrent()
    {
        _editorHost.Content = Current?.Editor; _viewMode.SelectedIndex = Current?.ViewMode ?? 0; ApplyView(); RenderPreview();
        if (Current is { } tab) { RefreshHeader(tab); tab.Editor.Focus(); }
    }
    private void RefreshHeader(EditorTab tab)
    {
        tab.Header.Text = (tab.Session.Path is { } path ? Path.GetFileName(path) : "Untitled") + (tab.Session.IsDirty ? " *" : "") + (tab.Session.Conflict is null ? "" : " ⚠");
        if (ReferenceEquals(Current, tab)) Title = tab.Header.Text + " — Axiom Markdown";
    }
    private void ApplyPreferences(bool save = true)
    {
        _appearance = _preferences.Appearance;
        RequestedThemeVariant = _appearance switch { AppearancePreference.Light => ThemeVariant.Light, AppearancePreference.Dark => ThemeVariant.Dark, _ => ThemeVariant.Default };
        var dark = _appearance == AppearancePreference.Dark || _appearance == AppearancePreference.System && ActualThemeVariant == ThemeVariant.Dark;
        Background = new SolidColorBrush(Color.Parse(dark ? "#111827" : "#FFFFFF"));
        Foreground = new SolidColorBrush(Color.Parse(dark ? "#E5E7EB" : "#1F2937"));
        _folderFiles.Background = new SolidColorBrush(Color.Parse(dark ? "#1F2937" : "#F3F4F6"));
        _folderFiles.Foreground = Foreground;
        foreach (var tab in Documents) { tab.Editor.WordWrap = _preferences.WordWrap; tab.Editor.FontSize = _preferences.FontSize; ApplyEditorColors(tab.Editor); }
        if (save) new EditorPreferencesStore(_paths.Root).Save(_preferences);
        RenderPreview();
    }
    private void ApplyEditorColors(TextEditor editor)
    {
        var dark = _appearance == AppearancePreference.Dark || _appearance == AppearancePreference.System && ActualThemeVariant == ThemeVariant.Dark;
        editor.Background = new SolidColorBrush(Color.Parse(dark ? "#111827" : "#FFFFFF"));
        editor.Foreground = new SolidColorBrush(Color.Parse(dark ? "#E5E7EB" : "#1F2937"));
        editor.SyntaxHighlighting = MarkdownHighlighting.Create(dark);
    }
    private void ApplyView()
    {
        _editorHost.IsVisible = _viewMode.SelectedIndex != 2; _preview.IsVisible = _viewMode.SelectedIndex != 1;
        _documentGrid.ColumnDefinitions[0].Width = _viewMode.SelectedIndex == 2 ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        _documentGrid.ColumnDefinitions[1].Width = new GridLength(_viewMode.SelectedIndex == 0 ? 5 : 0);
        _documentGrid.ColumnDefinitions[2].Width = _viewMode.SelectedIndex == 1 ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
    }
    private void RenderPreview()
    {
        if (!_ready || Current is not { } tab) return;
        try
        {
            tab.Token = Guid.NewGuid().ToString("N");
            tab.Structure = new MarkdownStructureService().Parse(tab.Session.Text);
            tab.Report = _renderer.RenderWithReport(tab.Session.Text, tab.Session.Path, _appearance, _folder, tab.Token,
                FeatureAccess.Evaluate(OptionalFeature.RemoteContent).Allowed ? tab.RemoteImages : null);
            _notice.Text = tab.Report.Changes.Count > 0 ? $"Preview sanitized: {tab.Report.Changes.Count} type(s) of content removed or changed. Markdown source is unchanged." : "";
            _noticeDetails.IsVisible = tab.Report.Changes.Count > 0;
            var blocked = tab.Report.RemoteImages.Count(url => !tab.RemoteImages.ContainsKey(url) || !FeatureAccess.Evaluate(OptionalFeature.RemoteContent).Allowed);
            if (blocked > 0) _notice.Text += $" {blocked} external image(s) blocked.";
            _loadRemote.IsVisible = blocked > 0 && FeatureAccess.Evaluate(OptionalFeature.RemoteContent).Allowed;
            _revokeRemote.IsVisible = tab.RemoteImages.Count > 0;
            _html.Load(_preview, tab.Report.Html);
        }
        catch (Exception error) { _notice.Text = "Preview could not be rendered: " + error.Message + ". Source remains available."; }
    }
    private void HardenPreview()
    {
        if (!OperatingSystem.IsWindows() || _preview.TryGetPlatformHandle() is not IWindowsWebView2PlatformHandle handle) return;
        var browser = Microsoft.Web.WebView2.Core.CoreWebView2.CreateFromComICoreWebView2(handle.CoreWebView2);
        browser.Settings.AreHostObjectsAllowed = false;
        browser.Settings.IsWebMessageEnabled = false;
        browser.Settings.AreDefaultContextMenusEnabled = false;
        browser.AddWebResourceRequestedFilter("*", Microsoft.Web.WebView2.Core.CoreWebView2WebResourceContext.All);
        browser.WebResourceRequested += (_, request) =>
        {
            if (Uri.TryCreate(request.Request.Uri, UriKind.Absolute, out var uri) && uri.Scheme is not "data" and not "about" && !_html.Allows(uri))
                request.Response = browser.Environment.CreateWebResourceResponse(new MemoryStream(), 403, "Blocked by preview policy", "Content-Type: text/plain");
        };
    }
    private async Task ObserveDiagramChangesAsync(EditorTab tab, string token)
    {
        for (var attempt = 0; attempt < 45 && ReferenceEquals(tab, Current) && tab.Token == token && !_lifetime.IsCancellationRequested; attempt++)
        {
            await Task.Delay(250);
            var value = ReviewProbe.DecodeScriptResult(await _preview.InvokeScript("JSON.stringify(window.axiomMermaidStatus)"));
            if (string.IsNullOrWhiteSpace(value) || value == "null" || value == "undefined") continue;
            using var result = System.Text.Json.JsonDocument.Parse(value);
            if (result.RootElement.GetProperty("token").GetString() != token) continue;
            var removed = result.RootElement.GetProperty("sanitized").GetInt32();
            if (removed > 0 && tab.Report is { } report)
            {
                var message = $"Mermaid sanitization removed {removed} unsafe diagram attribute/style item(s).";
                tab.Report = report with { Changes = [.. report.Changes.Where(change => !change.StartsWith("Mermaid sanitization")), message] };
                _notice.Text = "Preview sanitized. Markdown source is unchanged. " + message;
                _noticeDetails.IsVisible = true;
            }
            if (result.RootElement.GetProperty("completed").GetInt32() >= result.RootElement.GetProperty("total").GetInt32()) return;
        }
    }
    private async Task TrustRemoteAsync()
    {
        FeatureAccess.Require(OptionalFeature.RemoteContent);
        if (Current is not { Report: { } report } tab) return;
        var targets = report.RemoteImages.Where(url => !tab.RemoteImages.ContainsKey(url)).ToArray();
        if (targets.Length == 0) return;
        if (targets.Length > 16) throw new IOException("This document references more than 16 external images. Reduce them before loading.");
        var answer = await ChoiceDialog.ShowAsync(this, "Load external content?", "This document references external content. Loading it may contact other servers, disclose information about your connection, or retrieve harmful content. Only load external content if you trust the document and its source.\n\n" + string.Join("\n", targets) + "\n\nApproval covers only these images in this document session. Redirects and active content remain blocked.", "Load for this session", "Keep blocked");
        if (answer != "Load for this session") return;
        FeatureAccess.Require(OptionalFeature.RemoteContent);
        var source = tab.Session.Text;
        var loader = new RemoteImageLoader();
        foreach (var target in targets)
        {
            var data = await loader.LoadAsync(target, _lifetime.Token);
            if (!ReferenceEquals(tab, Current) || tab.Session.Text != source) throw new IOException("The document changed; review its external content again.");
            tab.RemoteImages[target] = data;
        }
        RenderPreview();
    }

    private async Task OpenPickerAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { AllowMultiple = true, Title = "Open Markdown", FileTypeFilter = [MarkdownType] });
        foreach (var file in files) if (file.TryGetLocalPath() is { } path) await OpenAsync(path);
    }
    internal async Task OpenAsync(string path)
    {
        path = Path.GetFullPath(path); ValidateMarkdownPath(path);
        if (Documents.FirstOrDefault(tab => SamePath(tab.Session.Path, path)) is { } existing) { _tabs.SelectedItem = existing.Item; return; }
        AddTab(await _documents.OpenAsync(path));
    }
    private async Task OpenFolderAsync()
    {
        var choices = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Open Markdown folder", AllowMultiple = false });
        if (choices.FirstOrDefault()?.TryGetLocalPath() is not { } root) return;
        await SetFolderAsync(root);
    }
    internal async Task SetFolderAsync(string root)
    {
        var files = await _files.ListMarkdownFilesAsync(root);
        _folder = Path.GetFullPath(root); _folderLabel.Text = _folder;
        _folderPaths = files.Files.Select(path => Path.GetRelativePath(_folder, path)).Order(StringComparer.OrdinalIgnoreCase).ToArray(); FilterFolder();
        _status.Text = $"{_folderPaths.Count} Markdown files; {files.SkippedDirectories.Count} inaccessible/linked folder(s) skipped.";
        RenderPreview();
    }
    private void FilterFolder() => _folderFiles.ItemsSource = _folderPaths.Where(path => string.IsNullOrEmpty(_folderFilter.Text) || path.Contains(_folderFilter.Text, StringComparison.OrdinalIgnoreCase)).ToArray();
    private async Task SaveAsync(EditorTab? tab, bool saveAs)
    {
        if (tab is null) return;
        if (!saveAs && tab.Session.Path is { } existing)
        {
            if (!File.Exists(existing)) { await SaveAsync(tab, true); return; }
            ValidateMarkdownPath(existing);
            await CheckExternalAsync(existing);
            if (tab.Session.Conflict is not null) return;
            await _documents.SaveAsync(tab.Session);
        }
        else
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Save Markdown", DefaultExtension = "md", SuggestedFileName = tab.Session.Path is null ? "Untitled.md" : Path.GetFileName(tab.Session.Path), FileTypeChoices = [MarkdownType], ShowOverwritePrompt = true });
            if (file?.TryGetLocalPath() is not { } path) return;
            await SaveToAsync(tab, path);
        }
        tab.Editor.Text = tab.Session.Text; RefreshHeader(tab); WatchDocuments(); RenderPreview();
        _status.Text = "Saved " + tab.Session.Path;
    }
    internal async Task SaveToAsync(EditorTab tab, string path)
    {
        path = Path.GetFullPath(path); ValidateMarkdownPath(path);
        if (WorkspacePathBoundary.Contains(_paths.Root, path, true)) throw new IOException("Choose a location outside application state.");
        if (Documents.Any(other => !ReferenceEquals(other, tab) && SamePath(other.Session.Path, path))) throw new IOException("That destination is open in another tab.");
        if (SamePath(tab.Session.Path, path)) await _documents.SaveAsync(tab.Session);
        else await _documents.SaveAsAsync(tab.Session, path);
        tab.Editor.Text = tab.Session.Text; RefreshHeader(tab); WatchDocuments();
    }
    private async Task SaveAllAsync() { foreach (var tab in Documents.ToArray()) { await SaveAsync(tab, false); if (tab.Session.IsDirty) break; } }
    private async Task<bool> ResolveDirtyAsync(EditorTab tab)
    {
        if (!tab.Session.IsDirty) return true;
        var answer = await ChoiceDialog.ShowAsync(this, "Unsaved Markdown", "Save changes to " + tab.Header.Text + " before closing?", "Save", "Discard changes", "Cancel");
        if (answer == "Cancel") return false;
        if (answer == "Save") { await SaveAsync(tab, false); return !tab.Session.IsDirty; }
        await _state.DeleteDraftAsync(tab.Session.DraftKey); return true;
    }
    private async Task CloseTabAsync(EditorTab? tab)
    {
        if (tab is null || !await ResolveDirtyAsync(tab)) return;
        _items.Remove(tab.Item); await _state.DeleteDraftAsync(tab.Session.DraftKey);
        if (_items.Count == 0) AddTab(_documents.NewDocument()); else if (_tabs.SelectedIndex < 0) _tabs.SelectedIndex = 0;
        WatchDocuments();
    }
    private async void OnClosing(object? sender, WindowClosingEventArgs args)
    {
        if (_approvedClose) return;
        args.Cancel = true;
        if (_closing || _busy) return;
        _closing = true;
        try
        {
            foreach (var tab in Documents.ToArray()) if (!await ResolveDirtyAsync(tab)) return;
            _protectTimer.Stop(); while (_protecting) await Task.Delay(20);
            foreach (var tab in Documents) await _state.DeleteDraftAsync(tab.Session.DraftKey);
            if (_sessionId is { } id) await _state.EndSessionAsync(id, true);
            _approvedClose = true; Close();
        }
        catch (Exception error) { _status.Text = "Close cancelled: " + error.Message; }
        finally { _closing = false; if (!_approvedClose) _protectTimer.Start(); }
    }
    private void WatchDocuments() => _monitor.WatchMany(Documents.Select(tab => tab.Session.Path).OfType<string>());
    private async Task CheckExternalAsync(string path)
    {
        if (_checkingExternal) return;
        _checkingExternal = true;
        try
        {
            foreach (var tab in Documents.Where(tab => SamePath(tab.Session.Path, path)).ToArray())
            {
                if (!File.Exists(path)) { _status.Text = "File removed outside the editor; current buffer retained. Use Save As to keep a copy."; continue; }
                if (!await _documents.CheckExternalChangeAsync(tab.Session)) continue;
                tab.RemoteImages.Clear();
                if (tab.Session.Conflict is not null)
                {
                    var choice = await ChoiceDialog.ShowAsync(this, "File changed outside Axiom Markdown", "Both your buffer and the saved file changed. Keep editing without overwriting, reload the saved file, or deliberately keep your local version for the next Save.", "Reload saved file", "Keep local version", "Keep editing");
                    if (choice == "Reload saved file") tab.Session.Reload(await _files.ReadAsync(path));
                    else if (choice == "Keep local version") tab.Session.KeepLocalAfterConflict();
                }
                tab.Editor.Text = tab.Session.Text; RefreshHeader(tab); RenderPreview();
            }
        }
        finally { _checkingExternal = false; }
    }
    private Task HandleRenameAsync(string oldPath, string newPath)
    {
        foreach (var tab in Documents.Where(tab => SamePath(tab.Session.Path, oldPath))) { tab.Session.UpdatePath(newPath); RefreshHeader(tab); tab.RemoteImages.Clear(); }
        WatchDocuments(); return CheckExternalAsync(newPath);
    }
    private async Task ProtectAndCheckPolicyAsync()
    {
        if (_protecting || _closing) return;
        _protecting = true;
        try
        {
            RefreshFeatureMenus();
            foreach (var tab in Documents.ToArray()) await _documents.ProtectAsync(tab.Session);
            if (!FeatureAccess.Evaluate(OptionalFeature.Updates).Allowed) _updateCancellation?.Cancel();
            if (!FeatureAccess.Evaluate(OptionalFeature.RemoteContent).Allowed && Documents.Any(tab => tab.RemoteImages.Count > 0))
            { foreach (var tab in Documents) tab.RemoteImages.Clear(); RenderPreview(); }
            if (!FeatureAccess.Evaluate(OptionalFeature.Terminal).Allowed)
                foreach (var terminal in _terminals.ToArray()) { await terminal.View.DisposeAsync(); terminal.Window.Close(); }
            if (!FeatureAccess.Evaluate(OptionalFeature.Git).Allowed)
                foreach (var git in _gitWindows.ToArray()) { git.View.CancelOperation(); git.Window.Close(); }
        }
        catch (Exception error) { _status.Text = "Recovery/policy check failed: " + error.Message; }
        finally { _protecting = false; }
    }
    private void RefreshFeatureMenus()
    {
        foreach (var (item, feature) in _featureMenuItems) { var decision = FeatureAccess.Evaluate(feature); item.IsEnabled = decision.Allowed; ToolTip.SetTip(item, decision.Reason); }
    }
    private static bool SamePath(string? first, string second) => first is not null && Path.GetFullPath(first).Equals(Path.GetFullPath(second), WorkspacePathBoundary.Comparison);
    private static void ValidateMarkdownPath(string path)
    { if (Path.GetExtension(path).ToLowerInvariant() is not ".md" and not ".markdown") throw new IOException("Choose a .md or .markdown file."); }
    private static FilePickerFileType MarkdownType => new("Markdown") { Patterns = ["*.md", "*.markdown"] };

    private void Format(MarkdownEditKind kind)
    {
        if (Current is not { } tab) return;
        var editor = tab.Editor;
        var result = new MarkdownEditService().Apply(editor.Text, editor.SelectionStart, editor.SelectionLength, kind);
        if (!result.Changed) return;
        editor.Document.Replace(0, editor.Document.TextLength, result.Text);
        editor.Select(result.SelectionStart, result.SelectionLength); editor.Focus();
    }
    private async Task FindAsync()
    {
        if (Current is not { } tab) return;
        var find = new TextBox { PlaceholderText = "Find text" }; var replace = new TextBox { PlaceholderText = "Replace with" };
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var next = new Button { Content = "Find next" }; var one = new Button { Content = "Replace" }; var all = new Button { Content = "Replace all" };
        var window = new Window { Title = "Find and replace", Width = 480, Height = 255, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        void Next() { if (string.IsNullOrEmpty(find.Text)) return; var text = tab.Editor.Text; var start = Math.Min(text.Length, tab.Editor.SelectionStart + tab.Editor.SelectionLength); var at = text.IndexOf(find.Text, start, StringComparison.OrdinalIgnoreCase); if (at < 0) at = text.IndexOf(find.Text, StringComparison.OrdinalIgnoreCase); if (at < 0) { status.Text = "No match."; return; } tab.Editor.Select(at, find.Text.Length); tab.Editor.ScrollToLine(tab.Editor.Document.GetLineByOffset(at).LineNumber); status.Text = "Match selected in editor."; }
        next.Click += (_, _) => Next();
        one.Click += (_, _) => { if (!string.IsNullOrEmpty(find.Text) && tab.Editor.SelectedText.Equals(find.Text, StringComparison.OrdinalIgnoreCase)) tab.Editor.Document.Replace(tab.Editor.SelectionStart, tab.Editor.SelectionLength, replace.Text ?? ""); Next(); };
        all.Click += (_, _) => { if (string.IsNullOrEmpty(find.Text)) return; tab.Editor.Document.Replace(0, tab.Editor.Document.TextLength, tab.Editor.Text.Replace(find.Text, replace.Text ?? "", StringComparison.OrdinalIgnoreCase)); status.Text = "All matches replaced; use Undo to restore."; };
        window.Content = new StackPanel { Margin = new Thickness(15), Spacing = 10, Children = { find, replace, new WrapPanel { Children = { next, one, all } }, status } };
        await window.ShowDialog(this);
    }
    private async Task FollowLinkAsync(Uri uri)
    {
        if (Current is not { Structure: { } structure } tab) return;
        if (uri.Scheme.StartsWith("axiom-", StringComparison.Ordinal))
        {
            var parts = uri.AbsolutePath.Trim('/').Split('/');
            if (parts.Length < 2 || parts[0] != tab.Token || structure.SourceText != tab.Session.Text) return;
            if (uri.Scheme == "axiom-destination") { await NavigateDestinationAsync(Uri.UnescapeDataString(parts[1]), tab); return; }
            if (!int.TryParse(parts[1], out var number)) return;
            if (uri.Scheme == "axiom-source") { tab.Editor.CaretOffset = Math.Clamp(number, 0, tab.Editor.Text.Length); tab.Editor.ScrollToLine(tab.Editor.Document.GetLineByOffset(tab.Editor.CaretOffset).LineNumber); if (_viewMode.SelectedIndex == 2) _viewMode.SelectedIndex = 0; tab.Editor.Focus(); return; }
            if (uri.Scheme == "axiom-task" && parts.Length == 3 && PreviewTaskToggle.TryToggle(structure, tab.Session.Text, number, parts[2] == "1", out var replacement)) { tab.Editor.Document.Replace(number, 1, replacement.ToString()); return; }
            if (uri.Scheme != "axiom-link" || number < 0 || number >= structure.Links.Count) return;
            await NavigateDestinationAsync(structure.Links[number].Destination, tab); return;
        }
        if (uri.Fragment.Length > 0 && (uri.Scheme is "about" or "data" || _html.CurrentNavigation is { } current && uri.GetLeftPart(UriPartial.Path) == current.GetLeftPart(UriPartial.Path)))
        { await NavigateDestinationAsync(uri.Fragment, tab); return; }
        await NavigateDestinationAsync(uri.AbsoluteUri, tab);
    }
    private async Task NavigateDestinationAsync(string destination, EditorTab tab)
    {
        if (destination.StartsWith('#')) { await _preview.InvokeScript("document.getElementById(" + System.Text.Json.JsonSerializer.Serialize(Uri.UnescapeDataString(destination[1..])) + ")?.scrollIntoView()"); return; }
        if (destination.StartsWith("axiom-wiki:")) destination = Uri.UnescapeDataString(destination[11..]);
        if (Uri.TryCreate(destination, UriKind.Absolute, out var external))
        {
            if (external.Scheme is not "http" and not "https" and not "mailto") throw new IOException("Unsupported external link.");
            if (await ChoiceDialog.ShowAsync(this, "Open external link?", "Only open this link if you trust its source. Your browser may contact an external server.\n\n" + external.AbsoluteUri, "Open link", "Cancel") == "Open link") Process.Start(new ProcessStartInfo(external.AbsoluteUri) { UseShellExecute = true });
            return;
        }
        if (destination.StartsWith("//") || destination.Contains('\\')) throw new IOException("Network-share links are blocked.");
        var baseFolder = tab.Session.Path is { } path ? Path.GetDirectoryName(path) : _folder;
        if (baseFolder is null) throw new IOException("Save the document or open a folder before following relative links.");
        var target = Path.GetFullPath(Path.Combine(baseFolder, Uri.UnescapeDataString(destination.Split('#')[0])));
        if (Path.GetExtension(target).Length == 0) target += ".md";
        await OpenAsync(target);
    }

    private async Task ImportAsync()
    {
        FeatureAccess.Require(OptionalFeature.Import);
        var choices = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Import as Markdown", FileTypeFilter = [new("HTML and Word") { Patterns = ["*.html", "*.htm", "*.docx"] }] });
        if (choices.FirstOrDefault()?.TryGetLocalPath() is not { } path) return;
        var result = await DocumentImportProgressWindow.RunAsync(this, path, _lifetime.Token);
        if (result is null || !await new DocumentImportPreviewWindow(result).ShowDialog<bool>(this)) return;
        FeatureAccess.Require(OptionalFeature.Import);
        var session = _documents.NewDocument(); session.UpdateText(result.Markdown); AddTab(session);
        await _documents.ProtectAsync(session); _status.Text = "Imported into a new unsaved document. " + string.Join(" ", result.Warnings);
    }
    private async Task ExportAsync()
    {
        FeatureAccess.Require(OptionalFeature.Export);
        if (Current is not { } tab) return;
        var snapshot = new ExportSnapshot(tab.Session.Text, tab.Session.Path);
        var types = new[] { ("HTML", "html"), ("Word", "docx"), ("PDF", "pdf"), ("Text", "txt"), ("EPUB", "epub"), ("OpenDocument", "odt") }.Select(item => new FilePickerFileType(item.Item1) { Patterns = ["*." + item.Item2] }).ToArray();
        var selected = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Export current buffer", FileTypeChoices = types, SuggestedFileName = Path.GetFileNameWithoutExtension(tab.Session.Path ?? "Untitled") + ".html", ShowOverwritePrompt = true });
        if (selected?.TryGetLocalPath() is not { } destination) return;
        var replace = File.Exists(destination);
        if (replace && await ChoiceDialog.ShowAsync(this, "Replace exported file?", destination, "Replace", "Cancel") != "Replace") return;
        var result = await new DocumentExportService(new NativePdfExportRenderer(_paths.Root)).WriteAsync(snapshot, destination, replace, Documents.Select(doc => doc.Session.Path).OfType<string>(), _paths.Root, _lifetime.Token);
        await ChoiceDialog.ShowAsync(this, "Export complete", result.Path + (result.Warnings.Count > 0 ? "\n\nContent changes and limitations:\n" + string.Join("\n", result.Warnings) : ""), "OK");
    }
    private async Task OpenGitAsync()
    {
        FeatureAccess.Require(OptionalFeature.Git);
        var folder = _folder ?? (Current?.Session.Path is { } path ? Path.GetDirectoryName(path) : null) ?? throw new IOException("Open a folder or saved document first.");
        var service = new GitWorkspaceService(); var snapshot = await service.InspectAsync(folder) ?? throw new IOException("This folder is not in a Git repository.");
        var window = new Window { Title = "Axiom Markdown — Git changes", Width = 880, Height = 760, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        bool Valid() => FeatureAccess.Evaluate(OptionalFeature.Git).Allowed && Directory.Exists(folder);
        bool Dirty(string relative) => Documents.Any(tab => tab.Session.Path is { } path && SamePath(path, Path.Combine(snapshot.RepositoryRoot, relative)) && (tab.Session.IsDirty || tab.Session.Conflict is not null));
        var view = new WorkspaceGitView(window, service, snapshot, Dirty, Valid, window.Close); window.Content = view;
        _gitWindows.Add((window, view)); window.Closed += (_, _) => { view.CancelOperation(); _gitWindows.RemoveAll(item => item.Window == window); };
        window.Show(this);
    }
    private async Task OpenHistoryAsync()
    {
        FeatureAccess.Require(OptionalFeature.Git);
        if (Current is not { Session.Path: { } path } tab) throw new IOException("Save the document before browsing its history.");
        var service = new GitDocumentHistoryService();
        var location = await service.LocateAsync(path) ?? throw new IOException("This document is not in a Git repository.");
        var revisions = await service.ListAsync(location);
        var selected = await new DocumentHistoryWindow(service, location, revisions, tab.Session.Text, tab.Session.SavedText, tab.Session.Conflict?.ExternalText).ShowDialog<HistoricalRestoreRequest?>(this);
        if (selected is null) return;
        FeatureAccess.Require(OptionalFeature.Git);
        if (tab.Session.Conflict is not null) throw new IOException("Resolve the external-change conflict before restoring history.");
        if (!await ResolveDirtyAsync(tab)) return;
        if (await ChoiceDialog.ShowAsync(this, "Restore revision into editor?", selected.Revision.CommitHash + "\nThe saved file remains unchanged until Save. Undo can restore the previous buffer.", "Restore", "Cancel") != "Restore") return;
        FeatureAccess.Require(OptionalFeature.Git);
        var text = new RevisionRestoreService().Prepare(tab.Session.Text, selected.Text);
        tab.Editor.Document.Replace(0, tab.Editor.Document.TextLength, text); tab.RemoteImages.Clear();
        await _documents.ProtectAsync(tab.Session); RenderPreview();
    }
    private async Task InitializeGitAsync()
    {
        FeatureAccess.Require(OptionalFeature.Git);
        var root = _folder ?? throw new IOException("Open a folder first.");
        if (await ChoiceDialog.ShowAsync(this, "Set up local Git history?", "Create .git in " + root + "? No remote or automatic commits will be created.", "Create repository", "Cancel") != "Create repository") return;
        await new GitDocumentHistoryService().InitializeAsync(root);
        _status.Text = "Local Git history initialized. Save and checkpoint documents deliberately.";
    }
    private async Task CheckpointAsync()
    {
        FeatureAccess.Require(OptionalFeature.Git);
        if (Current is not { Session.Path: { } path } tab || tab.Session.IsDirty || tab.Session.Conflict is not null) throw new IOException("Save the document and resolve conflicts before checkpointing.");
        var service = new GitDocumentHistoryService();
        var location = await service.LocateAsync(path) ?? throw new IOException("Set up local Git history first.");
        var bytes = await service.ReviewCheckpointAsync(location);
        if (await ChoiceDialog.ShowAsync(this, "Checkpoint saved document?", "Create a separate document-history checkpoint for " + path + "? No branch commit, staging or network action occurs.", "Checkpoint", "Cancel") != "Checkpoint") return;
        if (tab.Session.IsDirty || tab.Session.Conflict is not null) throw new IOException("The buffer changed; save and review again.");
        await service.CheckpointAsync(location, bytes);
        _status.Text = "Saved document checkpoint created.";
    }
    private async Task OpenTerminalAsync()
    {
        FeatureAccess.Require(OptionalFeature.Terminal);
        if (!OperatingSystem.IsWindows()) throw new IOException("Embedded terminal currently requires Windows.");
        var folder = _folder ?? (Current?.Session.Path is { } path ? Path.GetDirectoryName(path) : null) ?? throw new IOException("Open a folder first.");
        if (await ChoiceDialog.ShowAsync(this, "Open embedded terminal?", "The terminal can execute commands with your Windows account's permissions, including Git commands. Only run commands you understand and trust.\n\nFolder: " + folder, "Open terminal", "Cancel") != "Open terminal") return;
        FeatureAccess.Require(OptionalFeature.Terminal);
        var window = new Window { Title = "Axiom Markdown — Terminal", Width = 1000, Height = 650, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var view = new GitTerminalView(folder, () => FeatureAccess.Evaluate(OptionalFeature.Terminal).Allowed && Directory.Exists(folder));
        window.Content = view; _terminals.Add((window, view));
        window.Closed += async (_, _) => { _terminals.RemoveAll(item => item.Window == window); await view.DisposeAsync(); };
        window.Show(this);
    }
    private async Task FeatureSettingsAsync()
    {
        var controls = new StackPanel { Margin = new Thickness(18), Spacing = 10 };
        controls.Children.Add(new TextBlock { Text = "Feature controls — organization policy takes precedence", FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap });
        foreach (var feature in Enum.GetValues<OptionalFeature>())
        {
            var decision = _policy.Evaluate(feature);
            var toggle = new CheckBox { Content = feature + " — " + decision.Reason, IsChecked = decision.Allowed, IsEnabled = !decision.Reason.Contains("organization policy", StringComparison.OrdinalIgnoreCase) || decision.Allowed };
            toggle.IsCheckedChanged += (_, _) => { _policy.SetPreference(feature, toggle.IsChecked == true); toggle.Content = feature + " — " + _policy.Evaluate(feature).Reason; };
            controls.Children.Add(toggle);
        }
        controls.Children.Add(new TextBlock { Text = "Remote images always require separate document-session approval. Disabling a feature blocks its operations; its code remains in this edition. Policy: HKLM\\SOFTWARE\\AxiomMarkdown\\Policy (64-bit).", TextWrapping = TextWrapping.Wrap });
        var window = new Window { Title = "Axiom Markdown — Feature controls", Width = 640, Height = 490, WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = new ScrollViewer { Content = controls } };
        await window.ShowDialog(this); await ProtectAndCheckPolicyAsync(); RenderPreview();
    }
    private async Task CheckUpdatesAsync(bool showResult)
    {
        if (!FeatureAccess.Evaluate(OptionalFeature.Updates).Allowed) { if (showResult) FeatureAccess.Require(OptionalFeature.Updates); return; }
        _updateCancellation?.Cancel(); _updateCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        try
        {
            var message = await _updates.CheckAsync(new(), _updateCancellation.Token, new Progress<AutomaticUpdateProgress>(progress => _status.Text = progress.Message));
            _status.Text = message;
            if (showResult) await ChoiceDialog.ShowAsync(this, "Updates", message, "OK");
        }
        catch (Exception error) when (!showResult) { _status.Text = "Update check: " + error.Message; }
    }
    private async Task ApplyUpdateAsync()
    {
        FeatureAccess.Require(OptionalFeature.Updates);
        if (_updates.ReadyVersion is null) throw new IOException("No update is downloaded. Check for updates first.");
        if (Documents.Any(tab => tab.Session.IsDirty)) throw new IOException("Save or close unsaved documents before restarting to update.");
        if (await ChoiceDialog.ShowAsync(this, "Restart to update?", "Install downloaded version " + _updates.ReadyVersion + " and restart?", "Restart", "Cancel") == "Restart") _updates.ApplyAndRestart(new());
    }
    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            Func<Task>? action = e.Key switch { Key.N => () => NewAsync(), Key.O => OpenPickerAsync, Key.S => e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? SaveAllAsync : () => SaveAsync(Current, false), Key.W => () => CloseTabAsync(Current), Key.F => FindAsync, Key.B => () => { Format(MarkdownEditKind.Bold); return Task.CompletedTask; }, Key.I => () => { Format(MarkdownEditKind.Italic); return Task.CompletedTask; }, _ => null };
            if (action is not null) { e.Handled = true; await RunAsync(action); }
        }
    }
    internal EditorTab ActiveForReview => Current!;
    internal NativeWebView PreviewForReview => _preview;
    internal string NoticeForReview => _notice.Text ?? "";
    internal Task ProtectForReviewAsync() => ProtectAndCheckPolicyAsync();
    internal void RenderForReview() => RenderPreview();
    internal void CloseForReview() { _approvedClose = true; Close(); }
}

internal sealed class EditorTab(DocumentSession session, TextEditor editor)
{
    internal DocumentSession Session { get; } = session;
    internal TextEditor Editor { get; } = editor;
    internal TabItem Item { get; set; } = null!;
    internal TextBlock Header { get; set; } = null!;
    internal string Token { get; set; } = Guid.NewGuid().ToString("N");
    internal Dictionary<string, string> RemoteImages { get; } = new(StringComparer.Ordinal);
    internal SanitizedPreview? Report { get; set; }
    internal DocumentStructure? Structure { get; set; }
    internal int ViewMode { get; set; }
}

