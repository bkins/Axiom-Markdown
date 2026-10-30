using System.Text.Json;
using Axiom.Core;

namespace Axiom.Infrastructure;

public sealed record MarkdownPreferences(AppearancePreference Appearance = AppearancePreference.System
                                       , bool                 WordWrap = true
                                       , double               FontSize = 15)
{
    public bool CompactTitleBar { get; init; } = true;
    public MenuStyle MenuStyle { get; init; } = MenuStyle.Quiet;
    public bool ShowFileNavigator { get; init; } = true;
    public bool ShowOutline { get; init; }
}
