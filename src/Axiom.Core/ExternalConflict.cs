using System.Text.RegularExpressions;

namespace Axiom.Core;

public sealed record ExternalConflict(string LocalText, string ExternalText, DateTimeOffset ExternalWriteUtc);
