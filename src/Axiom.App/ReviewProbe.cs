using System.Text.Json;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Axiom.Core;
using Axiom.Infrastructure;

namespace Axiom.App;

/// <summary>Explicit opt-in test against this process's owned window and isolated state.</summary>
internal static class ReviewProbe
{
    internal static async Task RunAsync(MainWindow window, string root)
    {
        Directory.CreateDirectory(root);
        var checks = new Dictionary<string, bool>();
        try
        {
            var sample = Path.Combine(root, "Review.md");
            var source = "# Axiom Markdown review\n\nOrdinary **Markdown**, tables and HTML are retained.\n\n<div onclick=\"window.compromised=true\">HTML with an unsafe event handler.</div>\n<script>window.compromised=true</script>\n\n![Remote image](https://example.invalid/tracker.png)\n\n```mermaid\ngraph LR\n  Markdown --> Preview\n```\n\n| Feature | Result |\n| --- | --- |\n| Editing | Preserved |\n";
            await File.WriteAllTextAsync(sample, source);
            await window.OpenAsync(sample);
            await window.SetFolderAsync(root);
            window.RenderForReview();
            async Task<string?> Script(string script) => DecodeScriptResult(await window.PreviewForReview.InvokeScript(script).WaitAsync(TimeSpan.FromSeconds(5)));
            for (var attempt = 0; attempt < 80; attempt++)
            {
                await Task.Delay(250);
                if (await Script("document.querySelector('h1')?.textContent") == "Axiom Markdown review") break;
            }
            var view = window.PreviewForReview;
            checks["NativePreviewHeading"] = await Script("document.querySelector('h1')?.textContent") == "Axiom Markdown review";
            checks["SourceUntouchedBySanitization"] = window.ActiveForReview.Session.Text == source && !window.ActiveForReview.Session.IsDirty && await File.ReadAllTextAsync(sample) == source;
            checks["SanitizationNoticeVisible"] = window.NoticeForReview.Contains("Preview sanitized");
            checks["NoDocumentScriptOrHandler"] = await Script("String(!window.compromised && !document.querySelector('[onclick]') && !document.querySelector('script'))") == "true";
            checks["RemoteImageBlocked"] = await Script("String([...document.images].every(image => !image.src.startsWith('https://')))") == "true";
            for (var attempt = 0; attempt < 80; attempt++)
            {
                if (await Script("String(!!document.querySelector('.mermaid-diagram svg'))") == "true") break;
                await Task.Delay(250);
            }
            checks["BundledMermaidRendered"] = await Script("String(!!document.querySelector('.mermaid-diagram svg'))") == "true";
            window.ActiveForReview.Editor.AppendText("\nUnsaved buffer protected.\n");
            await window.ProtectForReviewAsync();
            checks["EditingDoesNotSilentlySave"] = await File.ReadAllTextAsync(sample) == source && window.ActiveForReview.Session.IsDirty;
            var state = new EditorStateStore(new AppDataPaths(Path.Combine(root, "state")));
            checks["RecoveryDraftPersisted"] = (await state.GetRecoverableDraftsAsync()).Any(draft => draft.Text.EndsWith("Unsaved buffer protected.\n", StringComparison.Ordinal));
            var saved = Path.Combine(root, "Saved.md");
            await window.SaveToAsync(window.ActiveForReview, saved);
            checks["ExplicitSaveMatchesBuffer"] = await File.ReadAllTextAsync(saved) == window.ActiveForReview.Session.Text && !window.ActiveForReview.Session.IsDirty;
            var imported = Path.Combine(root, "Import.html");
            await File.WriteAllTextAsync(imported, "<h1>Import title</h1><p onclick='evil()'>Retained text</p><script>evil()</script>");
            var converted = await new DocumentImportService().ConvertAsync(imported);
            checks["ImportReportsSanitization"] = converted.Markdown.Contains("Retained text") && converted.Warnings.Any(warning => warning.Contains("sanitization", StringComparison.OrdinalIgnoreCase));
            var export = Path.Combine(root, "Export.html");
            await new DocumentExportService().WriteAsync(new("# Exported from an unsaved snapshot", null), export, false, [], Path.Combine(root, "state"));
            checks["HtmlExport"] = File.ReadAllText(export).Contains("Exported from an unsaved snapshot");
            var pdf = Path.Combine(root, "Native.pdf");
            await new DocumentExportService(new NativePdfExportRenderer(Path.Combine(root, "state"))).WriteAsync(new("# Native PDF\n\nA bounded native PDF export check.", null), pdf, false, [], Path.Combine(root, "state"));
            checks["NativePdfExport"] = (await File.ReadAllBytesAsync(pdf)).AsSpan(0, 5).SequenceEqual("%PDF-"u8);
            window.RenderForReview(); await Task.Delay(600);
            var scale = window.RenderScaling;
            using (var bitmap = new RenderTargetBitmap(new PixelSize((int)(window.Bounds.Width * scale), (int)(window.Bounds.Height * scale)), new Vector(96 * scale, 96 * scale)))
            { bitmap.Render(window); bitmap.Save(Path.Combine(root, "window.png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions()); }
            if (OperatingSystem.IsWindows() && view.TryGetPlatformHandle() is IWindowsWebView2PlatformHandle handle)
            {
                var browser = Microsoft.Web.WebView2.Core.CoreWebView2.CreateFromComICoreWebView2(handle.CoreWebView2);
                await using var capture = File.Create(Path.Combine(root, "preview.png"));
                await browser.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png, capture);
            }
            await File.WriteAllTextAsync(Path.Combine(root, "preview-dom.html"), await Script("document.documentElement.outerHTML"));
            await File.WriteAllTextAsync(Path.Combine(root, "result.json"), JsonSerializer.Serialize(new { Version = ApplicationBuildIdentity.FromAssembly(typeof(MainWindow).Assembly).InformationalVersion, Checks = checks, Passed = checks.Values.All(value => value), HumanReview = "Visual comfort, real-document quality, accessibility/DPI and organizational security approval remain pending." }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception error)
        {
            await File.WriteAllTextAsync(Path.Combine(root, "failure.txt"), error.ToString());
        }
        finally { window.CloseForReview(); }
    }
    internal static string? DecodeScriptResult(string? value) => value?.StartsWith('"') == true ? JsonSerializer.Deserialize<string>(value) : value;
}
