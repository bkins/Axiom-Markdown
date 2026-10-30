using AngleSharp.Html.Parser;
using Axiom.App;
using Axiom.Core;
using Axiom.Infrastructure;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Axiom.Markdown.Tests;

public sealed class SecurityBoundaryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "AxiomMarkdownTests", Guid.NewGuid().ToString("N"));
    public SecurityBoundaryTests() { Directory.CreateDirectory(_root); FeatureAccess.Initialize(_ => new(true, "test")); }
    public void Dispose() { FeatureAccess.Initialize(_ => new(false, "uninitialized")); Directory.Delete(_root, true); }

    [Theory]
    [InlineData("<script>alert(1)</script><p>retained</p>")]
    [InlineData("<div onclick='evil()'>retained</div>")]
    [InlineData("<iframe src='https://example.invalid'></iframe><p>retained</p>")]
    [InlineData("<p style='background:url(https://example.invalid)'>retained</p>")]
    [InlineData("<svg><foreignObject><iframe src='https://example.invalid'></iframe></foreignObject></svg><p>retained</p>")]
    [InlineData("<meta http-equiv='refresh' content='0;url=https://example.invalid'><p>retained</p>")]
    [InlineData("<a href='jav&#x61;script:evil()'>retained</a>")]
    [InlineData("<img srcset='https://example.invalid/x 2x' src='data:image/svg+xml;base64,AAAA'><p>retained</p>")]
    public void UnsafeContentIsRemovedAndReported(string html)
    {
        var report = new PreviewSanitizer().Sanitize(html);
        Assert.NotEmpty(report.Changes); Assert.Contains("retained", report.Html);
        using var document = new HtmlParser().ParseDocument(report.Html);
        Assert.Null(document.QuerySelector("script,iframe,object,embed,svg,meta,[onclick],[style],[srcset]"));
        Assert.DoesNotContain("javascript:", report.Html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SafeRawHtmlIsRetainedWithoutFalseSanitizationNotice()
    {
        var report = new PreviewSanitizer().Sanitize("<details><summary>More</summary><p><strong>retained</strong></p></details>");
        Assert.Empty(report.Changes); Assert.Contains("<strong>retained</strong>", report.Html);
    }

    [Fact]
    public void RemoteApprovalOnlyAppliesToTheExactApprovedImages()
    {
        const string approved = "https://one.invalid/image.png";
        const string data = "data:image/png;base64,AAAA";
        var sanitizer = new PreviewSanitizer();
        var blocked = sanitizer.Sanitize($"<img src='{approved}'><img src='https://two.invalid/image.png'>");
        Assert.Equal(2, blocked.RemoteImages.Count); Assert.DoesNotContain("src=", blocked.Html);
        var trusted = sanitizer.Sanitize($"<img src='{approved}'><img src='https://two.invalid/image.png'>", new Dictionary<string, string> { [approved] = data });
        Assert.Contains(data, trusted.Html); Assert.DoesNotContain("two.invalid", trusted.Html);
    }

    [Fact]
    public void WholePreviewContainsOnlyTrustedHeadAndCannotEscapeTheSanitizer()
    {
        var source = "# retained\n\n</body></html><head><script>evil()</script><meta http-equiv='refresh' content='0;url=https://example.invalid'></head><body><p onclick='evil()'>body</p>";
        var result = new MarkdownPreviewService().RenderWithReport(source, null, AppearancePreference.Light, null, "abc");
        using var parsed = new HtmlParser().ParseDocument(result.Html);
        Assert.Null(parsed.QuerySelector("script,[onclick],meta[http-equiv=refresh]"));
        Assert.NotEmpty(result.Changes); Assert.Contains("script-src 'none'", result.Html);
        Assert.DoesNotContain("img-src file:", result.Html);
    }

    [Fact]
    public void LocalImagesOutsideDocumentFolderAndLinkedImagesRemainBlocked()
    {
        var folder = Path.Combine(_root, "docs"); Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(_root, "secret.png"), [137, 80, 78, 71]);
        var result = new MarkdownPreviewService().RenderWithReport("![secret](../secret.png)", Path.Combine(folder, "note.md"), AppearancePreference.Light, folder, "abc");
        Assert.Contains("Blocked an unsupported", string.Join(" ", result.Changes));
        Assert.DoesNotContain("data:image/png;base64", result.Html);
    }

    [Theory]
    [InlineData(OptionalFeature.Import)]
    [InlineData(OptionalFeature.Export)]
    [InlineData(OptionalFeature.Git)]
    [InlineData(OptionalFeature.Terminal)]
    [InlineData(OptionalFeature.RemoteContent)]
    [InlineData(OptionalFeature.Updates)]
    public async Task DisabledFeatureRejectsDirectServiceCallsBeforeSideEffects(OptionalFeature feature)
    {
        FeatureAccess.Initialize(value => new(value != feature, "disabled for test"));
        var target = Path.Combine(_root, "never-created.html");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            switch (feature)
            {
                case OptionalFeature.Import: await new DocumentImportService().ConvertAsync(Path.Combine(_root, "missing.html")); break;
                case OptionalFeature.Export: await new DocumentExportService().WriteAsync(new("# hi", null), target, false, [], _root); break;
                case OptionalFeature.Git: await new GitCommandRunner("must-not-launch-any-process").RunAsync(_root, ["status"]); break;
                case OptionalFeature.Terminal: await using (var terminal = new WindowsTerminalSession(_root, new string('a', 32), _ => { })) { } break;
                case OptionalFeature.RemoteContent: await new RemoteImageLoader().LoadAsync("https://example.invalid", CancellationToken.None); break;
                case OptionalFeature.Updates: await new AutomaticUpdateService(_ => throw new Exception("Factory must not execute")).CheckAsync(new(), CancellationToken.None); break;
            }
        });
        Assert.False(File.Exists(target));
    }

    [Fact]
    public void PreferencesCanDisableIndependentlyAndMalformedPreferencesFailClosed()
    {
        var policy = new FeaturePolicyStore(_root);
        policy.SetPreference(OptionalFeature.Terminal, false);
        Assert.False(policy.Evaluate(OptionalFeature.Terminal).Allowed);
        // Machine policy can also deny any feature, so only assert the user preference remains independent in storage.
        Assert.DoesNotContain("Git", File.ReadAllText(Path.Combine(_root, "features.json")));
        File.WriteAllText(Path.Combine(_root, "features.json"), "malformed");
        Assert.All(Enum.GetValues<OptionalFeature>(), feature => Assert.False(policy.Evaluate(feature).Allowed));
    }

    [Theory]
    [InlineData(0, true, false)]
    [InlineData(0, false, false)]
    [InlineData(1, true, true)]
    [InlineData(1, false, false)]
    [InlineData(2, true, false)]
    public void MachineDenyWinsOverUserChoice(int machine, bool user, bool expected)
        => Assert.Equal(expected, FeaturePolicyStore.Resolve(machine, user).Allowed);

    [Fact]
    public void MalformedMachineValueCannotBeEnabledByUser()
    {
        Assert.False(FeaturePolicyStore.Resolve("1", true).Allowed);
        Assert.True(FeaturePolicyStore.Resolve(null, true).Allowed);
        Assert.False(FeaturePolicyStore.Resolve(null, false).Allowed);
    }

    [Fact]
    public async Task ImportReportsRemovedActiveAttributesAndPreservesOriginal()
    {
        var path = Path.Combine(_root, "input.html");
        const string source = "<h1>Title</h1><p onclick='bad()'>retained</p><script>bad()</script>";
        await File.WriteAllTextAsync(path, source);
        var result = await new DocumentImportService().ConvertAsync(path);
        Assert.Contains("retained", result.Markdown); Assert.DoesNotContain("bad()", result.Markdown);
        Assert.Contains(result.Warnings, warning => warning.Contains("sanitization")); Assert.Equal(source, await File.ReadAllTextAsync(path));
    }
}
