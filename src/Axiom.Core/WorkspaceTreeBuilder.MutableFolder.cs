namespace Axiom.Core;

public static partial class WorkspaceTreeBuilder
{
    private sealed class MutableFolder(string name
                                     , string relativePath)
    {
        private readonly Dictionary<string, MutableFolder> _folders = new(WorkspacePathBoundary.Comparer);
        public List<WorkspaceTreeNode> Documents { get; } = [];

        public MutableFolder GetOrAddFolder(string childName
                                          , string childPath)
        {
            if (!_folders.TryGetValue(childName, out var folder))
            {
                folder = new MutableFolder(childName, childPath);
                _folders.Add(childName, folder);
            }

            return folder;
        }

        public IReadOnlyList<WorkspaceTreeNode> ToNodes()
        {
            var folders = _folders.Values.OrderBy(folder => folder.Name, StringComparer.OrdinalIgnoreCase).Select(folder => new WorkspaceTreeNode(folder.Name
                                                                                                                                                , folder.RelativePath
                                                                                                                                                , false
                                                                                                                                                , folder.ToNodes()));
            var documents = Documents.OrderBy(document => document.Name, StringComparer.OrdinalIgnoreCase);
            return folders.Concat(documents).ToArray();
        }

        private string Name { get; } = name;
        private string RelativePath { get; } = relativePath;
    }
}
