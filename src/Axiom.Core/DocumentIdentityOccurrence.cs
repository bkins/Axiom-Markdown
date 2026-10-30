using System.Text.RegularExpressions;

namespace Axiom.Core;

public sealed record DocumentIdentityOccurrence(Guid DocumentId, string Path);
