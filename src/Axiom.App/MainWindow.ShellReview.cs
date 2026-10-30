using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Axiom.Core;
using Axiom.Infrastructure;

namespace Axiom.App;

public sealed partial class MainWindow
{
    internal async Task<IReadOnlyDictionary<string, bool>> VerifyShellForReviewAsync(string root)
    {
        var checks = new Dictionary<string, bool>();
        var tab = Current!;
        var text = tab.Session.Text;
        var dirty = tab.Session.IsDirty;
        var original = _preferences;
        try
        {
            var screen = Screens.ScreenFromWindow(this)!;
            await File.WriteAllTextAsync(Path.Combine(root
                                                    , "window-placement.json")
                                       , System.Text.Json.JsonSerializer.Serialize(new { Width, Height, Bounds = Bounds.ToString(), Position = Position.ToString(), ScreenScale = screen.Scaling, RenderScaling, WorkingArea = screen.WorkingArea.ToString() }));
            checks["InitialWindowFitsWorkingArea"] = Position.X >= screen.WorkingArea.X && Position.Y >= screen.WorkingArea.Y && Position.X + Width * screen.Scaling <= screen.WorkingArea.Right && Position.Y + Height * screen.Scaling <= screen.WorkingArea.Bottom;
            checks["CompactCommandBarAndMenus"]    = _menuButtons.Count == 1 && _commandGroups.Count == 6 && _commandBar.IsVisible && (!OperatingSystem.IsWindows() || _captionButtons.IsVisible);
            _preferences                           = _preferences with

            {
                ShowOutline = true
              , Appearance  = AppearancePreference.Light
              , MenuStyle   = MenuStyle.Rounded
            };
            ApplyPreferences();
            await Task.Delay(400);
            checks["RoundedMenuAndOutline"]       = _menuButtons.All(button => button.Classes.Contains("rounded-menu")) && _outlineHost.IsVisible && _outline.ItemCount > 0;
            _outline.SelectedItem                 = tab.Structure!.Headings.Last();
            checks["OutlineSelectsSourceHeading"] = tab.Editor.TextArea.Caret.Offset == tab.Structure.Headings.Last().Source.Offset;
            CaptureShellForReview(Path.Combine(root, "shell-light.png"));
            _viewMode.SelectedIndex            = 2;
            checks["ReadingModeRetainsSource"] = !_editorHost.IsVisible && _preview.IsVisible && tab.Session.Text == text && tab.Session.IsDirty == dirty;
            _viewMode.SelectedIndex            = 0;
            _preferences                       = _preferences with

            {
                Appearance        = AppearancePreference.Dark
              , MenuStyle         = MenuStyle.Quiet
              , ShowFileNavigator = false
              , CompactTitleBar   = false
            };
            ApplyPreferences();
            await Task.Delay(250);
            checks["NavigatorAndNativeTitleBarOptions"] = !_sidebar.IsVisible && !_captionButtons.IsVisible && _workbench.ColumnDefinitions[0].Width.Value == 0;
            _preferences                                = _preferences with

            {
                CompactTitleBar   = true
              , ShowFileNavigator = true
            };
            ApplyPreferences();
            var nested = Path.Combine(root, "folder", "nested");
            Directory.CreateDirectory(nested);
            var nestedDocument = Path.Combine(nested, "Nested.md");
            await File.WriteAllTextAsync(nestedDocument, "# Nested document");
            await SetFolderAsync(Path.Combine(root, "folder"));
            _folderFilter.Text = "Nested";
            await Task.Delay(150);
            var folder = _folderFiles.Items.OfType<WorkspaceTreeNode>().Single();
            checks["FilteredFolderTreeRetainsHierarchy"] = folder.IsExpanded && folder.Children.Single().RelativePath == Path.Combine("nested"
                                                                                                                                    , "Nested.md");
            checks["AppearanceChangesRetainDirtyBuffer"] = ReferenceEquals(tab, Current) && tab.Session.Text == text && tab.Session.IsDirty == dirty && _menuButtons.All(button => button.Classes.Contains("quiet-menu"));
            checks["ShellPreferencesPersist"]            = new EditorPreferencesStore(_paths.Root).Load() == _preferences;
            _folderFilter.Text                           = "";
            await Task.Delay(250);
            CaptureShellForReview(Path.Combine(root, "shell-dark.png"));
        }
        finally
        {
            _preferences = original;
            ApplyPreferences();
        }

        return checks;
    }

    private void CaptureShellForReview(string path)
    {
        var scale = RenderScaling;
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)(Bounds.Width * scale)
                                                              , (int)(Bounds.Height * scale))
                                                , new Vector(96 * scale
                                                           , 96 * scale));
        bitmap.Render(this);
        bitmap.Save(path, new PngBitmapEncoderOptions());
    }
}
