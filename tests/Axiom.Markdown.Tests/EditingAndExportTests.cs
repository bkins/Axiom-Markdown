using System.IO.Compression;
using Axiom.Core;
using Axiom.Infrastructure;
using Xunit;

namespace Axiom.Markdown.Tests;

public sealed partial class EditingAndExportTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath()
                                               , "AxiomMarkdownTests"
                                               , Guid.NewGuid().ToString("N"));
    public EditingAndExportTests()
    {
        Directory.CreateDirectory(_root);
        FeatureAccess.Initialize(_ => new(true, "test"));
    }

    public void Dispose()
    {
        FeatureAccess.Initialize(_ => new(false, "test ended"));
        var expected = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "AxiomMarkdownTests")) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(_root).StartsWith(expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException("Refusing cleanup outside test root.");
        }

        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(_root, true);
    }

    [Fact]
    public void OrdinaryMarkdownDoesNotProduceFalseSanitizationWarning()
    {
        var report = new MarkdownPreviewService().RenderWithReport("# Heading\n\n**text**\n\n- [ ] Task"
                                                                 , null
                                                                 , AppearancePreference.Light
                                                                 , null
                                                                 , "token");
        Assert.Empty(report.Changes);
        Assert.Empty(report.RemoteImages);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EditorHighlightingHandlesHeadingsBlankLinesAndMarkdownWithoutEmptyMatches(bool dark)
    {
        var definition = Axiom.App.MarkdownHighlighting.Create(dark);
        var document = new AvaloniaEdit.Document.TextDocument("# Heading\n\n**bold** and `code`\n[link](relative.md)");
        using var highlighter = new AvaloniaEdit.Highlighting.DocumentHighlighter(document, definition);
        for (var line = 1; line <= document.LineCount; line++)
        {
            Assert.NotNull(highlighter.HighlightLine(line));
        }
    }

    [Fact]
    public async Task EditsMadeWhileSaveIsInFlightStayDirtyAndRecoverable()
    {
        var state = new EditorStateStore(new AppDataPaths(Path.Combine(_root, "state")));
        await state.InitializeAsync();
        var files = new DeferredFiles();
        var service = new DocumentService(files, state, state);
        var session = service.NewDocument();
        session.UpdateText("snapshot to save");
        var saving = service.SaveAsAsync(session, Path.Combine(_root, "saved.md"));
        await files.WriteStarted.Task;
        session.UpdateText("newer unsaved typing");
        files.Resume.SetResult();
        await saving;
        Assert.Equal("snapshot to save", session.SavedText);
        Assert.Equal("newer unsaved typing", session.Text);
        Assert.True(session.IsDirty);
        Assert.Equal("newer unsaved typing", Assert.Single(await state.GetRecoverableDraftsAsync()).Text);
    }

    [Theory]
    [InlineData("html")]
    [InlineData("txt")]
    [InlineData("docx")]
    [InlineData("epub")]
    [InlineData("odt")]
    public async Task ExportUsesSnapshotAndReportsHtmlReductionWithoutMutatingSource(string extension)
    {
        var source = Path.Combine(_root, "source.md");
        const string markdown = "# Title\n\n<div onclick='evil()'>Retained HTML text<script>bad()</script></div>\n\n**Markdown**";
        await File.WriteAllTextAsync(source, markdown);
        var result = await new DocumentExportService().WriteAsync(new(markdown
                                                                    , source)
                                                                , Path.Combine(_root
                                                                             , "output." + extension)
                                                                , false
                                                                , [source]
                                                                , Path.Combine(_root
                                                                             , "state"));
        Assert.True(new FileInfo(result.Path).Length > 0);
        Assert.Contains(result.Warnings, warning => warning.Contains("sanitization"));
        Assert.Equal(markdown, await File.ReadAllTextAsync(source));
        if (extension is "txt" or "html")
        {
            Assert.Contains("Retained HTML text", await File.ReadAllTextAsync(result.Path));
        }

        if (extension is "docx" or "epub" or "odt")
        {
            using var zip = ZipFile.OpenRead(result.Path);
            Assert.NotEmpty(zip.Entries);
        }
    }

    [Fact]
    public async Task CancellationAndProtectedPathsPreserveExistingFiles()
    {
        var source = Path.Combine(_root, "source.md");
        await File.WriteAllTextAsync(source, "original");
        var output = Path.Combine(_root, "existing.html");
        await File.WriteAllTextAsync(output, "existing output");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new DocumentExportService().WriteAsync(new("# next"
                                                                                                               , source)
                                                                                                           , output
                                                                                                           , true
                                                                                                           , [source]
                                                                                                           , Path.Combine(_root
                                                                                                                        , "state")
                                                                                                           , cancellation.Token));
        Assert.Equal("existing output", await File.ReadAllTextAsync(output));
        await Assert.ThrowsAsync<IOException>(() => new DocumentExportService().WriteAsync(new("# next"
                                                                                             , source)
                                                                                         , output
                                                                                         , true
                                                                                         , [output]
                                                                                         , Path.Combine(_root
                                                                                                      , "state")));
        Assert.Equal("existing output", await File.ReadAllTextAsync(output));
    }

    [Fact]
    public void ExportDoesNotReadImagesOutsideTheSourceFolderOrEncodedNetworkPaths()
    {
        var folder = Path.Combine(_root, "docs");
        Directory.CreateDirectory(folder);
        var snapshot = new ExportSnapshot("", Path.Combine(folder, "source.md"));
        var warnings = new List<string>();
        Assert.Null(ExportImageReader.Read("../secret.png", snapshot, warnings));
        Assert.Null(ExportImageReader.Read("%5c%5cserver%5cshare%5cimage.png", snapshot, warnings));
        Assert.Equal(2, warnings.Count);
    }

    [Fact]
    public async Task GitSelectedStageAndCheckpointLeaveOtherFileAndSavedDocumentUnchanged()
    {
        var history = new GitDocumentHistoryService();
        await history.InitializeAsync(_root);
        var selected = Path.Combine(_root, "selected.md");
        var other = Path.Combine(_root, "other.md");
        await File.WriteAllTextAsync(selected, "# selected");
        await File.WriteAllTextAsync(other, "# other");
        var service = new GitWorkspaceService();
        var snapshot = (await service.InspectAsync(_root))!;
        var hash = await service.ReviewWorkingHashAsync(snapshot, "selected.md");
        await service.StageAsync(snapshot, "selected.md", hash);
        var status = await new GitCommandRunner().RunAsync(_root, ["diff", "--cached", "--name-only"]);
        Assert.Contains("selected.md", System.Text.Encoding.UTF8.GetString(status.Output));
        Assert.DoesNotContain("other.md", System.Text.Encoding.UTF8.GetString(status.Output));
        var location = (await history.LocateAsync(selected))!;
        var bytes = await history.ReviewCheckpointAsync(location);
        var revision = await history.CheckpointAsync(location, bytes);
        Assert.NotEmpty(revision);
        Assert.Equal("# selected", await File.ReadAllTextAsync(selected));
        Assert.Equal("# other", await File.ReadAllTextAsync(other));
        Assert.NotEmpty(await history.ListAsync(location));
    }
}
