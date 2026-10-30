using System.Diagnostics;
using Axiom.App;
using Axiom.Core;
using Axiom.Infrastructure;
using Xunit;

namespace Axiom.Markdown.Tests;

public sealed class OptionalRuntimeTests
{
    [Fact]
    public async Task WindowsTerminalOwnsShellRejectsDisabledInputAndStopsOnDispose()
    {
        if (!OperatingSystem.IsWindows()) return;
        FeatureAccess.Initialize(_ => new(true, "test"));
        var root = Path.GetTempPath();
        int processId;
        await using (var session = new WindowsTerminalSession(root, Guid.NewGuid().ToString("N"), _ => { }))
        {
            processId = session.ProcessId;
            Assert.True(session.IsRunning);
            FeatureAccess.Initialize(feature => new(feature != OptionalFeature.Terminal, "test deny"));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => session.WriteAsync("ignored"));
        }
        try { using var process = Process.GetProcessById(processId); Assert.True(process.HasExited || process.WaitForExit(5000)); }
        catch (ArgumentException) { }
        finally { FeatureAccess.Initialize(_ => new(false, "test ended")); }
    }

    [Fact]
    public async Task UpdateDownloadCannotBeAppliedAfterPolicyIsDisabled()
    {
        FeatureAccess.Initialize(_ => new(true, "test"));
        var client = new FakeUpdateClient(); var service = new AutomaticUpdateService(_ => client);
        await service.CheckAsync(new(), CancellationToken.None);
        Assert.Equal("0.2.0", service.ReadyVersion); Assert.True(client.Downloaded);
        FeatureAccess.Initialize(feature => new(feature != OptionalFeature.Updates, "organization deny"));
        Assert.Throws<UnauthorizedAccessException>(() => service.ApplyAndRestart(new())); Assert.False(client.Applied);
        FeatureAccess.Initialize(_ => new(false, "test ended"));
    }

    private sealed class FakeUpdateClient : IAutomaticUpdateClient
    {
        public bool IsInstalled => true;
        public bool Downloaded { get; private set; }
        public bool Applied { get; private set; }
        public Task<string?> DownloadLatestAsync(CancellationToken cancellationToken, IProgress<AutomaticUpdateProgress>? progress = null) { Downloaded = true; return Task.FromResult<string?>("0.2.0"); }
        public void ApplyAndRestart() => Applied = true;
    }
}
