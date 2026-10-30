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

public sealed partial class MainWindow
{
    internal Task ProtectForReviewAsync() => ProtectAndCheckPolicyAsync();
    internal void RenderForReview() => RenderPreview();
    internal void CloseForReview()
    {
        _approvedClose = true;
        Close();
    }

    internal EditorTab ActiveForReview => Current!;
    internal NativeWebView PreviewForReview => _preview;
    internal string NoticeForReview => _notice.Text ?? "";
}
