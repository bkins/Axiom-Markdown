using System.Text.Json;
using Axiom.Core;

namespace Axiom.Infrastructure;

public sealed record MarkdownPreferences(AppearancePreference Appearance = AppearancePreference.System
                                       , bool                 WordWrap = true
                                       , double               FontSize = 15);
