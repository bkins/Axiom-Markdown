using Avalonia;
using Axiom.Core;
using Axiom.Infrastructure;

namespace Axiom.App;

internal static class Program
{
    internal static string? StartupDocument { get; private set; }
    internal static string? ProbeRoot { get; private set; }

    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--review-probe")
        {
            ProbeRoot = Path.GetFullPath(args[1]);
            Environment.SetEnvironmentVariable("AXIOM_MARKDOWN_STATE_ROOT", Path.Combine(ProbeRoot, "state"));
        }

        var paths = new AppDataPaths();
        AppDomain.CurrentDomain.UnhandledException += (_, failure) =>
        {
            Directory.CreateDirectory(paths.Root);
            File.WriteAllText(Path.Combine(paths.Root, "unhandled-error.txt"), failure.ExceptionObject.ToString());
        };
        var policy = new FeaturePolicyStore(paths.Root);
        FeatureAccess.Initialize(policy.Evaluate);
        Velopack.VelopackApp.Build().SetAutoApplyOnStartup(false).Run();
        if (args.Length == 1 && !args[0].StartsWith('-'))
        {
            StartupDocument = Path.GetFullPath(args[0]);
        }

        using var instance = new SingleInstanceCoordinator(paths.Root
                                                         , ApplicationBuildIdentity.FromAssembly(typeof(Program).Assembly).InformationalVersion);
        if (!instance.IsPrimary)
        {
            return instance.ForwardAsync(StartupDocument).GetAwaiter().GetResult() ? 0 : 2;
        }

        instance.Listen(App.RequestActivation);
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}
