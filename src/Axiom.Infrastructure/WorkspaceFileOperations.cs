using System.Security.Cryptography;
using Axiom.Core;

namespace Axiom.Infrastructure;

/// <summary>Only the shared path and hash guards retained from Axiom's larger workspace tools.</summary>
public static class WorkspaceFileOperations
{
    public static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    public static void ValidateAncestors(string root, string path, bool requireParent = true)
    {
        var normalizedRoot = WorkspaceService.NormalizeRootPath(root);
        if (!Directory.Exists(normalizedRoot)) throw new DirectoryNotFoundException("The workspace is unavailable.");
        if (!WorkspacePathBoundary.Contains(normalizedRoot, path, includeRoot: true))
            throw new ArgumentException("The target must remain inside its workspace.");
        for (var current = path; current is not null; current = Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current)) && File.GetAttributes(current).HasFlag(FileAttributes.ReparsePoint))
                throw new IOException("Linked files, folders, and ancestors cannot be changed through Axiom Markdown.");
        var parent = Path.GetDirectoryName(path);
        if (requireParent && (parent is null || !Directory.Exists(parent))) throw new DirectoryNotFoundException("Choose an existing destination folder.");
    }
}
