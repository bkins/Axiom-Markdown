namespace Axiom.Core;





/// <summary>Every optional operation checks the effective machine policy and user preference.</summary>
public static class FeatureAccess
{
    private static Func<OptionalFeature, FeatureDecision> _evaluate = _ => new(false
                                                                             , "Feature policy has not initialized.");
    public static void Initialize(Func<OptionalFeature, FeatureDecision> evaluate) => _evaluate = evaluate;
    public static FeatureDecision Evaluate(OptionalFeature feature) => _evaluate(feature);
    public static void Require(OptionalFeature feature)
    {
        var decision = Evaluate(feature);
        if (!decision.Allowed)
        {
            throw new UnauthorizedAccessException($"{feature} is disabled. {decision.Reason}");
        }
    }
}
