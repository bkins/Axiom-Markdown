using Axiom.Core;
using Microsoft.VisualBasic.FileIO;

namespace Axiom.Infrastructure;

public sealed class MarkdownWorkspaceOperations
{
    public string Resolve(string root
                        , string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative))
        {
            throw new IOException("Choose an item inside the open folder.");
        }

        var parts = relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]
                                 , StringSplitOptions.RemoveEmptyEntries);
        if (parts.Any(part => part is "." or ".." || part.Contains(':') || part.EndsWith('.') || part.EndsWith(' ') || IsVersionControlDirectory(part)))
        {
            throw new IOException("The path contains an unsupported or reserved folder/name.");
        }

        var path = Path.GetFullPath(Path.Combine(root, relative));
        if (!WorkspacePathBoundary.Contains(root, path))
        {
            throw new IOException("The path must remain inside the open folder.");
        }

        ValidateAncestors(root, path);
        return path;
    }

    public string Move(string root
                     , string sourceRelative
                     , string destinationRelative)
    {
        var source = Resolve(root, sourceRelative);
        var destination = Resolve(root, destinationRelative);
        var folder = Directory.Exists(source);
        if (!folder && !File.Exists(source))
        {
            throw new FileNotFoundException("The selected item no longer exists.", source);
        }

        if (!folder)
        {
            RequireMarkdown(source);
        }

        if (SamePath(source, destination) || folder && WorkspacePathBoundary.Contains(source, destination))
        {
            throw new IOException("Choose another folder; an item cannot move into itself or its descendants.");
        }

        RequireNewDestination(destination);
        if (folder)
        {
            ValidateFolderContents(root, source);
            Directory.Move(source, destination);
        }
        else
        {
            RequireMarkdown(destination);
            File.Move(source, destination, false);
        }

        return destination;
    }

    public string CreateDocument(string root
                               , string relative)
    {
        var path = Resolve(root, relative);
        RequireMarkdown(path);
        RequireNewDestination(path);
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        return path;
    }

    public string CreateFolder(string root
                             , string relative)
    {
        var path = Resolve(root, relative);
        RequireNewDestination(path);
        Directory.CreateDirectory(path);
        return path;
    }

    public async Task<string> DuplicateDocumentAsync(string root
                                                   , string sourceRelative
                                                   , string destinationRelative)
    {
        var source = Resolve(root, sourceRelative);
        var destination = Resolve(root, destinationRelative);
        RequireMarkdown(source);
        RequireMarkdown(destination);
        RequireNewDestination(destination);
        var file = await new FileDocumentStore().ReadAsync(source);
        var text = DocumentIdCodec.Find(file.Text) is null ? file.Text : DocumentIdCodec.Reassign(file.Text
                                                                                                , Guid.NewGuid());
        await using var stream = new FileStream(destination, FileMode.CreateNew, FileAccess.Write);
        await using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false));
        await writer.WriteAsync(text);
        return destination;
    }

    public void Recycle(string root
                      , string relative)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Recycle Bin operations require Windows.");
        }

        var source = Resolve(root, relative);
        if (Directory.Exists(source))
        {
            ValidateFolderContents(root, source);
            FileSystem.DeleteDirectory(source
                                     , UIOption.OnlyErrorDialogs
                                     , RecycleOption.SendToRecycleBin
                                     , UICancelOption.ThrowException);
        }
        else
        {
            RequireMarkdown(source);
            FileSystem.DeleteFile(source
                                , UIOption.OnlyErrorDialogs
                                , RecycleOption.SendToRecycleBin
                                , UICancelOption.ThrowException);
        }
    }

    private static void RequireNewDestination(string path)
    {
        if (File.Exists(path) || Directory.Exists(path))
        {
            throw new IOException("The destination already exists; nothing was overwritten.");
        }

        if (!Directory.Exists(Path.GetDirectoryName(path)))
        {
            throw new DirectoryNotFoundException("Choose an existing destination folder.");
        }
    }

    private static void RequireMarkdown(string path)
    {
        if (Path.GetExtension(path).ToLowerInvariant() is not ".md" and not ".markdown")
        {
            throw new IOException("Choose a .md or .markdown file.");
        }
    }

    private static bool SamePath(string first
                               , string second)
    {
        return string.Equals(first, second, WorkspacePathBoundary.Comparison);
    }

    private static bool IsVersionControlDirectory(string name)
    {
        return name.Equals(".git", StringComparison.OrdinalIgnoreCase) || name.Equals(".hg"
                                                                                    , StringComparison.OrdinalIgnoreCase) || name.Equals(".svn"
                                                                                                                                       , StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateAncestors(string root
                                        , string path)
    {
        var current = path;
        while (WorkspacePathBoundary.Contains(root, current, includeRoot: true))
        {
            if (File.Exists(current) || Directory.Exists(current))
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new IOException("Linked files/folders cannot participate in this operation.");
                }
            }

            if (SamePath(current, Path.GetFullPath(root)))
            {
                break;
            }

            current = Path.GetDirectoryName(current)!;
        }
    }

    private static void ValidateFolderContents(string root
                                             , string folder)
    {
        var pending = new Stack<string>();
        pending.Push(folder);
        while (pending.TryPop(out var current))
        {
            foreach (var child in Directory.EnumerateFileSystemEntries(current))
            {
                if (IsVersionControlDirectory(Path.GetFileName(child)))
                {
                    throw new IOException("Folders containing version-control metadata cannot be moved or recycled here.");
                }

                ValidateAncestors(root, child);
                if (Directory.Exists(child))
                {
                    pending.Push(child);
                }
            }
        }
    }
}
