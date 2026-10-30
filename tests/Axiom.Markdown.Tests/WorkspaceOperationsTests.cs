using Axiom.Core;
using Axiom.Infrastructure;
using Xunit;

namespace Axiom.Markdown.Tests;

public sealed class WorkspaceOperationsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath()
                                               , "AxiomMarkdownMoves"
                                               , Guid.NewGuid().ToString("N"));
    private readonly MarkdownWorkspaceOperations _operations = new();
    private string? _junction;
    public WorkspaceOperationsTests() => Directory.CreateDirectory(_root);
    public void Dispose()
    {
        if (_junction is { } junction)
        {
            RunFixtureScript("Remove-Item -LiteralPath '" + junction.Replace("'", "''") + "' -Force -ErrorAction Stop");
        }

        Directory.Delete(_root, true);
    }

    [Fact]
    public void FileAndFolderMovesPreserveBytesAndRejectCollisions()
    {
        Directory.CreateDirectory(Path.Combine(_root, "target"));
        Directory.CreateDirectory(Path.Combine(_root, "source"));
        var bytes = new byte[]
        {
            0xff
          , 0xfe
          , 35
          , 0
          , 32
          , 0
          , 65
          , 0
        };
        File.WriteAllBytes(Path.Combine(_root, "source", "Note.md"), bytes);
        File.WriteAllBytes(Path.Combine(_root, "source", "image.png"), [1, 2, 3]);
        _operations.Move(_root, "source", Path.Combine("target", "source"));
        Assert.Equal(bytes
                   , File.ReadAllBytes(Path.Combine(_root
                                                  , "target"
                                                  , "source"
                                                  , "Note.md")));
        Assert.Equal(new byte[]
        {
            1
          , 2
          , 3
        }
                   , File.ReadAllBytes(Path.Combine(_root
                                                  , "target"
                                                  , "source"
                                                  , "image.png")));
        _operations.Move(_root, Path.Combine("target", "source", "Note.md"), "Note.md");
        File.WriteAllText(Path.Combine(_root, "target", "Existing.md"), "keep");
        Assert.Throws<IOException>(() => _operations.Move(_root, "Note.md", Path.Combine("target", "Existing.md")));
        Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(_root, "Note.md")));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(_root, "target", "Existing.md")));
    }

    [Theory]
    [InlineData("../outside.md")]
    [InlineData(".git/config.md")]
    [InlineData("folder/../other.md")]
    [InlineData("Note.md:stream")]
    [InlineData("folder./Note.md")]
    public void InvalidDestinationsLeaveSourceUntouched(string destination)
    {
        File.WriteAllText(Path.Combine(_root, "Note.md"), "keep");
        Assert.Throws<IOException>(() => _operations.Move(_root, "Note.md", destination));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(_root, "Note.md")));
    }

    [Fact]
    public void DescendantRootAndVersionControlMovesAreRejected()
    {
        Directory.CreateDirectory(Path.Combine(_root, "folder", "child"));
        Assert.Throws<IOException>(() => _operations.Move(_root
                                                        , "folder"
                                                        , Path.Combine("folder"
                                                                     , "child"
                                                                     , "folder")));
        Assert.Throws<IOException>(() => _operations.Move(_root, ".", "new-root"));
        Directory.CreateDirectory(Path.Combine(_root, "folder", ".git"));
        Assert.Throws<IOException>(() => _operations.Move(_root, "folder", "new-folder"));
        Assert.True(Directory.Exists(Path.Combine(_root, "folder", ".git")));
        Assert.False(Directory.Exists(Path.Combine(_root, "new-folder")));
    }

    [Fact]
    public async Task EmptyFoldersRemainVisibleAndMetadataIsExcluded()
    {
        _operations.CreateFolder(_root, "empty");
        Directory.CreateDirectory(Path.Combine(_root, ".git", "hidden"));
        File.WriteAllText(Path.Combine(_root
                                     , ".git"
                                     , "hidden"
                                     , "Note.md")
                        , "metadata");
        var discovery = await new FileDocumentStore().ListMarkdownFilesAsync(_root);
        Assert.Contains(Path.Combine(_root, "empty"), discovery.Folders!);
        Assert.DoesNotContain(discovery.Folders!, path => path.Contains(".git"));
        Assert.Empty(discovery.Files);
        Assert.Throws<IOException>(() => _operations.CreateFolder(_root, "empty"));
    }

    [Fact]
    public async Task DuplicateGetsIndependentIdentityAndCannotOverwrite()
    {
        var original = DocumentIdCodec.Enroll("# Note", Guid.NewGuid());
        File.WriteAllText(Path.Combine(_root, "Note.md"), original);
        var copy = await _operations.DuplicateDocumentAsync(_root, "Note.md", "Copy.md");
        var text = await File.ReadAllTextAsync(copy);
        Assert.NotEqual(DocumentIdCodec.Find(original), DocumentIdCodec.Find(text));
        Assert.Contains("# Note", text);
        Assert.Equal(original
                   , text.Replace(DocumentIdCodec.Find(text)!.Value.ToString("D")
                                , DocumentIdCodec.Find(original)!.Value.ToString("D")));
        Assert.Equal(original, await File.ReadAllTextAsync(Path.Combine(_root, "Note.md")));
        await Assert.ThrowsAsync<IOException>(() => _operations.DuplicateDocumentAsync(_root
                                                                                     , "Note.md"
                                                                                     , "Copy.md"));
    }

    [Fact]
    public void JunctionSourcesDestinationsAndNestedContentsAreRejected()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var target = Path.Combine(_root, "target");
        var source = Path.Combine(_root, "source");
        Directory.CreateDirectory(target);
        Directory.CreateDirectory(source);
        var link = Path.Combine(source, "linked");
        var script = "New-Item -ItemType Junction -Path '" + link.Replace("'", "''") + "' -Target '" + target.Replace("'"
                                                                                                                    , "''") + "' -ErrorAction Stop | Out-Null";
        RunFixtureScript(script);
        _junction = link;
        File.WriteAllText(Path.Combine(_root, "Note.md"), "keep");
        Assert.Throws<IOException>(() => _operations.Move(_root
                                                        , "Note.md"
                                                        , Path.Combine("source"
                                                                     , "linked"
                                                                     , "Note.md")));
        Assert.Throws<IOException>(() => _operations.Move(_root, Path.Combine("source", "linked"), "new-link"));
        Assert.Throws<IOException>(() => _operations.Move(_root, "source", "new-source"));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(_root, "Note.md")));
        Assert.True(Directory.Exists(link));
    }

    private static void RunFixtureScript(string script)
    {
        var start = new System.Diagnostics.ProcessStartInfo("powershell.exe")
                    {
                        UseShellExecute        = false
                      , CreateNoWindow         = true
                      , RedirectStandardError  = true
                      , RedirectStandardOutput = true
                    };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-EncodedCommand");
        start.ArgumentList.Add(Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script)));
        using var process = System.Diagnostics.Process.Start(start)!;
        if (!process.WaitForExit(10000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Junction fixture operation timed out.");
        }

        Assert.True(process.ExitCode == 0, process.StandardError.ReadToEnd());
    }
}
