using System.Diagnostics;
using Axiom.App;
using Axiom.Core;
using Axiom.Infrastructure;
using Xunit;

namespace Axiom.Markdown.Tests;

public sealed partial class OptionalRuntimeTests
{
    private sealed class FakeUpdateClient : IAutomaticUpdateClient
    {
        public bool IsInstalled => true;
        public bool Downloaded { get; private set; }
        public bool Applied { get; private set; }

        public Task<string?> DownloadLatestAsync(CancellationToken                   cancellationToken
                                               , IProgress<AutomaticUpdateProgress>? progress = null)
        {
            Downloaded = true;
            return Task.FromResult<string?>("0.2.0");
        }

        public void ApplyAndRestart() => Applied = true;
    }
}
