using System.Text.Json;
using Axiom.Core;

namespace Axiom.Infrastructure;

public sealed record MarkdownPreferences(AppearancePreference Appearance = AppearancePreference.System, bool WordWrap = true, double FontSize = 15);
public sealed class EditorPreferencesStore(string stateRoot)
{
    private string PathName => Path.Combine(stateRoot, "editor.json");
    public MarkdownPreferences Load()
    {
        if (!File.Exists(PathName)) return new();
        var value = JsonSerializer.Deserialize<MarkdownPreferences>(File.ReadAllText(PathName)) ?? throw new IOException("Editor preferences are invalid.");
        if (!Enum.IsDefined(value.Appearance) || !double.IsFinite(value.FontSize) || value.FontSize is < 10 or > 32) throw new IOException("Editor preferences are invalid.");
        return value;
    }
    public void Save(MarkdownPreferences value)
    {
        Directory.CreateDirectory(stateRoot);
        var temporary = PathName + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, JsonSerializer.Serialize(value)); File.Move(temporary, PathName, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
