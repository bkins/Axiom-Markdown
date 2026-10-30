using Axiom.Core;
using Axiom.Infrastructure;
using Xunit;

namespace Axiom.Markdown.Tests;

public sealed class ShellPreferencesTests
{
    [Fact]
    public void ExistingPreferencesGainOriginalAxiomShellDefaults()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root
                                         , "editor.json")
                            , "{\"Appearance\":1,\"WordWrap\":false,\"FontSize\":17}");
            var preferences = new EditorPreferencesStore(root).Load();
            Assert.True(preferences.CompactTitleBar);
            Assert.True(preferences.ShowFileNavigator);
            Assert.False(preferences.ShowOutline);
            Assert.Equal(MenuStyle.Quiet, preferences.MenuStyle);
            Assert.False(preferences.WordWrap);
            Assert.Equal(17, preferences.FontSize);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ShellChoicesPersistAndMalformedMenuStyleIsRejected()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new EditorPreferencesStore(root);
            var preferences = new MarkdownPreferences
                              {
                                  CompactTitleBar   = false
                                , MenuStyle         = MenuStyle.Rounded
                                , ShowFileNavigator = false
                                , ShowOutline       = true
                              };
            store.Save(preferences);
            Assert.Equal(preferences, store.Load());
            File.WriteAllText(Path.Combine(root, "editor.json"), "{\"MenuStyle\":999}");
            Assert.Throws<IOException>(() => store.Load());
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
