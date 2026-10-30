namespace Axiom.Core;

public sealed class NullAxiomLog : IAxiomLog
{
    public void Information(string  eventName
                          , string? path = null)
    {
    }

    public void Error(string    eventName
                    , Exception exception
                    , string?   path = null)
    {
    }
}
