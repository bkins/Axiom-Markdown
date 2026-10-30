namespace Axiom.Core;

public interface IAxiomLog
{
    void Information(string eventName, string? path = null);
    void Error(string eventName, Exception exception, string? path = null);
}
