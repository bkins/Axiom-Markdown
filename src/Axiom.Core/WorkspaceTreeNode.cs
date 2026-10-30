namespace Axiom.Core;

public sealed class WorkspaceTreeNode(string                           name
                                    , string                           relativePath
                                    , bool                             isDocument
                                    , IReadOnlyList<WorkspaceTreeNode> children)
{
    public string Name { get; } = name;
    public string RelativePath { get; } = relativePath;
    public bool IsDocument { get; } = isDocument;
    public IReadOnlyList<WorkspaceTreeNode> Children { get; } = children;
    public bool IsExpanded { get; set; }
}
