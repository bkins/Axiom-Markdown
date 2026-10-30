namespace Axiom.Core;

public sealed record WorkspacePin(string         RelativePath
                                , DateTimeOffset PinnedUtc)
{
    public override string ToString() => RelativePath;
}
