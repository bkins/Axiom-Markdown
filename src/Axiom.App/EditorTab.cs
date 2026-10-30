using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using Axiom.Core;
using Axiom.Infrastructure;

namespace Axiom.App;

internal sealed class EditorTab(DocumentSession session
                              , TextEditor      editor)
{
    internal DocumentSession Session { get; } = session;
    internal TextEditor Editor { get; } = editor;
    internal TabItem Item { get; set; } = null!;
    internal TextBlock Header { get; set; } = null!;
    internal string Token { get; set; } = Guid.NewGuid().ToString("N");
    internal Dictionary<string, string> RemoteImages { get; } = new(StringComparer.Ordinal);
    internal SanitizedPreview? Report { get; set; }
    internal DocumentStructure? Structure { get; set; }
    internal int ViewMode { get; set; }
}
