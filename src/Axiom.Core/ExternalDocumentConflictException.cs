namespace Axiom.Core;

public sealed class ExternalDocumentConflictException(string path) : IOException($"The file changed outside Axiom. Resolve both versions before saving: {path}");
