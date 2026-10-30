using System.Text.RegularExpressions;

namespace Axiom.Core;

public sealed record RecoveryDraft(string          DraftKey
                                 , string?         FilePath
                                 , string          Text
                                 , DateTimeOffset  UpdatedUtc
                                 , DateTimeOffset? SavedFileWriteUtc
                                 , bool            IsNewDocument);
