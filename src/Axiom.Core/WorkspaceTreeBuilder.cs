namespace Axiom.Core;

public static partial class WorkspaceTreeBuilder
{
    public static IReadOnlyList<WorkspaceTreeNode> Build(string               workspaceRoot
                                                       , IEnumerable<string>  markdownPaths
                                                       , IEnumerable<string>? folderPaths = null)
    {
        var root = WorkspaceService.NormalizeRootPath(workspaceRoot);
        var tree = new MutableFolder(string.Empty, string.Empty);
        foreach (var folderPath in folderPaths ?? [])
        {
            var relative = Path.GetRelativePath(root, Path.GetFullPath(folderPath));
            if (Path.IsPathRooted(relative) || relative == "." || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar
                                                                                                        , StringComparison.Ordinal))
            {
                continue;
            }

            var folder = tree;
            var currentPath = string.Empty;
            foreach (var part in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            {
                currentPath = string.IsNullOrEmpty(currentPath) ? part : Path.Combine(currentPath, part);
                folder      = folder.GetOrAddFolder(part, currentPath);
            }
        }

        foreach (var markdownPath in markdownPaths)
        {
            var fullPath = Path.GetFullPath(markdownPath);
            var relativePath = Path.GetRelativePath(root, fullPath);
            if (relativePath == ".." || relativePath.StartsWith(".." + Path.DirectorySeparatorChar
                                                              , StringComparison.Ordinal))
            {
                continue;
            }

            var parts = relativePath.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]
                                         , StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                continue;
            }

            var folder = tree;
            var currentPath = string.Empty;
            for (var index = 0; index < parts.Length - 1; index++)
            {
                currentPath = string.IsNullOrEmpty(currentPath) ? parts[index] : Path.Combine(currentPath
                                                                                            , parts[index]);
                folder      = folder.GetOrAddFolder(parts[index], currentPath);
            }

            folder.Documents.Add(new WorkspaceTreeNode(parts[^1]
                                                     , relativePath
                                                     , true
                                                     , []));
        }

        return tree.ToNodes();
    }
}
