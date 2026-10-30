using Avalonia.Controls;
using Avalonia.Platform;

namespace Axiom.App;

internal static class PreviewBrowserPolicy
{
    internal static void Attach(NativeWebView     preview
                              , NativeHtmlContent html)
    {
        if (!OperatingSystem.IsWindows() || preview.TryGetPlatformHandle() is not IWindowsWebView2PlatformHandle handle)
        {
            return;
        }

        var browser = Microsoft.Web.WebView2.Core.CoreWebView2.CreateFromComICoreWebView2(handle.CoreWebView2);
        browser.Settings.AreHostObjectsAllowed         = false;
        browser.Settings.IsWebMessageEnabled           = false;
        browser.Settings.AreDefaultContextMenusEnabled = false;
        browser.AddWebResourceRequestedFilter("*", Microsoft.Web.WebView2.Core.CoreWebView2WebResourceContext.All);
        browser.WebResourceRequested += (_, request) =>
        {
            if (Uri.TryCreate(request.Request.Uri, UriKind.Absolute, out var uri) && uri.Scheme is not "data" and not "about" && !html.Allows(uri))
            {
                request.Response = browser.Environment.CreateWebResourceResponse(new MemoryStream()
                                                                               , 403
                                                                               , "Blocked by preview policy"
                                                                               , "Content-Type: text/plain");
            }
        };
    }
}
