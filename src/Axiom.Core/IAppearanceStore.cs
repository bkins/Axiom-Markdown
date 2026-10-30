namespace Axiom.Core;

public interface IAppearanceStore
{
    Task<AppearancePreference> GetAppearanceAsync(CancellationToken cancellationToken = default);
    Task SetAppearanceAsync(AppearancePreference preference, CancellationToken cancellationToken = default);
}
