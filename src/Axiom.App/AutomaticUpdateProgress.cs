using Axiom.Core;
using Velopack;
using Velopack.Sources;

namespace Axiom.App;

internal sealed record AutomaticUpdateProgress(string Message, int? Percentage = null);
