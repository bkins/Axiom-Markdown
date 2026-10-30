namespace Axiom.Core;

public sealed record WorkspaceRecord(Guid           WorkspaceId
                                   , string         RootPath
                                   , string         DisplayName
                                   , DateTimeOffset CreatedUtc
                                   , DateTimeOffset LastOpenedUtc);
