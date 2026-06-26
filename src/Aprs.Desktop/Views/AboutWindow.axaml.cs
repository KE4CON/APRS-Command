using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Aprs.Desktop.Configuration;

namespace Aprs.Desktop.Views;

public sealed partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        // Version from assembly metadata.
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "0.1.0";
        VersionText.Text = $"Version {version}";

        // Runtime info.
        RuntimeText.Text = $".NET {Environment.Version}";

        // Platform.
        var os = RuntimeInformation.OSDescription;
        var arch = RuntimeInformation.OSArchitecture;
        PlatformText.Text = $"{os} ({arch})";

        // Operator callsign from settings.
        var callsign = JsonAppSettingsStore.Default.Load().Station.Callsign;
        CallsignText.Text = string.IsNullOrWhiteSpace(callsign) ? "Not configured" : callsign.ToUpperInvariant();
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();

    private void RepoLink_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        try
        {
            var url = "https://github.com/KE4CON/APRS-Command";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                Process.Start("open", url);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                Process.Start("xdg-open", url);
            else
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { /* ignore — browser may not be available */ }
    }
}
