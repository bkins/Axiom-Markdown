namespace Axiom.Core;

public sealed class WorkspaceService(IWorkspaceStore store)
{
    public Task<WorkspaceRecord> OpenAsync(string            folderPath
                                         , DateTimeOffset?   openedUtc = null
                                         , CancellationToken cancellationToken = default)
    {
        var rootPath = NormalizeRootPath(folderPath);
        var displayName = GetDisplayName(rootPath);
        return store.OpenWorkspaceAsync(rootPath
                                      , displayName
                                      , openedUtc ?? DateTimeOffset.UtcNow
                                      , cancellationToken);
    }

    public Task<WorkspaceRecord?> GetActiveAsync(CancellationToken cancellationToken = default) => store.GetActiveWorkspaceAsync(cancellationToken);
    public Task<IReadOnlyList<WorkspacePin>> GetPinsAsync(WorkspaceRecord   workspace
                                                        , CancellationToken cancellationToken = default)
    {
        return store.GetPinsAsync(workspace.WorkspaceId, cancellationToken);
    }

    public Task PinAsync(WorkspaceRecord   workspace
                       , string            documentPath
                       , CancellationToken cancellationToken = default)
    {
        return store.AddPinAsync(workspace.WorkspaceId
                               , GetRelativeDocumentPath(workspace.RootPath
                                                       , documentPath)
                               , DateTimeOffset.UtcNow
                               , cancellationToken);
    }

    public Task UnpinAsync(WorkspaceRecord   workspace
                         , string            documentPath
                         , CancellationToken cancellationToken = default)
    {
        return store.RemovePinAsync(workspace.WorkspaceId
                                  , GetRelativeDocumentPath(workspace.RootPath
                                                          , documentPath)
                                  , cancellationToken);
    }

    public Task RemovePinAsync(WorkspaceRecord   workspace
                             , WorkspacePin      pin
                             , CancellationToken cancellationToken = default)
    {
        return store.RemovePinAsync(workspace.WorkspaceId, pin.RelativePath, cancellationToken);
    }

    public static string NormalizeRootPath(string folderPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(folderPath));
    }

    private static string GetDisplayName(string rootPath)
    {
        var name = new DirectoryInfo(rootPath).Name;
        return string.IsNullOrWhiteSpace(name) ? rootPath : name;
    }

    public static string GetRelativeDocumentPath(string workspaceRoot
                                               , string documentPath)
    {
        var root = NormalizeRootPath(workspaceRoot);
        var fullPath = Path.GetFullPath(Path.IsPathRooted(documentPath) ? documentPath : Path.Combine(root
                                                                                                    , documentPath));
        var relativePath = Path.GetRelativePath(root, fullPath);
        if (relativePath == ".." || relativePath.StartsWith(".." + Path.DirectorySeparatorChar
                                                          , StringComparison.Ordinal) || Path.IsPathRooted(relativePath))
        {
            throw new ArgumentOutOfRangeException(nameof(documentPath)
                                                , "A pinned document must remain inside its workspace.");
        }

        if (!relativePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase) && !relativePath.EndsWith(".markdown"
                                                                                                      , StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentOutOfRangeException(nameof(documentPath), "Only Markdown documents can be pinned.");
        }

        return relativePath;
    }
}
