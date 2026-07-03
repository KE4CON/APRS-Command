using Avalonia;
using Velopack;

namespace Aprs.Desktop;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        
       VelopackApp.Build().Run();
       try
        {
            AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
            {
                if (eventArgs.ExceptionObject is Exception exception)
                {
                    StartupDiagnostics.WriteFatalStartupError(exception);
                }
            };

            TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
            {
                StartupDiagnostics.WriteFatalStartupError(eventArgs.Exception);
            };

            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception exception)
        {
            var logPath = StartupDiagnostics.WriteFatalStartupError(exception);
            Console.Error.WriteLine("APRS Command failed to start.");
            Console.Error.WriteLine($"Startup error log: {logPath}");
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
