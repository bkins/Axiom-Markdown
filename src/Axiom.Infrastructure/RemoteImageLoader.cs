using System.Net;
using Axiom.Core;

namespace Axiom.Infrastructure;

/// <summary>Called only after approval of the displayed exact destinations. Never follows redirects or sends credentials/cookies.</summary>
public sealed class RemoteImageLoader
{
    public async Task<string> LoadAsync(string destination, CancellationToken cancellationToken)
    {
        FeatureAccess.Require(OptionalFeature.RemoteContent);
        if (!Uri.TryCreate(destination, UriKind.Absolute, out var uri) || uri.Scheme is not "http" and not "https" || !string.IsNullOrEmpty(uri.UserInfo))
            throw new IOException("Only HTTP(S) images without embedded credentials may be loaded.");
        using var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, UseDefaultCredentials = false, Credentials = null };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode is >= HttpStatusCode.MultipleChoices and < HttpStatusCode.BadRequest)
            throw new IOException("Redirect blocked. Review and open the destination directly if you trust it.");
        response.EnsureSuccessStatusCode();
        var type = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant();
        if (type is not "image/png" and not "image/jpeg" and not "image/gif" and not "image/webp" and not "image/bmp")
            throw new IOException("Remote content must be a supported raster image; SVG and active content are blocked.");
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var bytes = await DocumentImportService.ReadBoundedAsync(stream, DocumentImportService.MaximumImageBytes, cancellationToken);
        DocumentImportService.ValidateImage(bytes, type);
        FeatureAccess.Require(OptionalFeature.RemoteContent);
        return $"data:{type};base64,{Convert.ToBase64String(bytes)}";
    }
}
