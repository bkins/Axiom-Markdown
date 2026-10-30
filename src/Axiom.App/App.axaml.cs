using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System.Collections.Concurrent;

namespace Axiom.App;

public partial class App : Application
{
    private static readonly ConcurrentQueue<string?> Activations = new();
    private static MainWindow? _window;
    internal static void RequestActivation(string? path)
    {
        Activations.Enqueue(path);
        if (_window is not null)
        {
            Dispatcher.UIThread.Post(DrainActivations);
        }
    }

    private static void DrainActivations()
    {
        if (_window is not { } window)
        {
            return;
        }

        while (Activations.TryDequeue(out var path))
        {
            window.QueueExternalOpen(path);
        }
    }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _window            = new MainWindow();
            desktop.MainWindow = _window;
            _window.Opened += (_, _) => DrainActivations();
            _window.Closed += (_, _) => _window = null;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
