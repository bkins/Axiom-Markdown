using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Axiom.Infrastructure;





/// <summary>Owns one interactive process per application-state root, before any state/log writer opens.</summary>
public sealed partial class SingleInstanceCoordinator
{
    private sealed record ActivationRequest(string? Document, string? BuildIdentity);
}
