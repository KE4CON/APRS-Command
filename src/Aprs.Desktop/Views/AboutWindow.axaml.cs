using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Aprs.Desktop.Configuration;
using Aprs.Desktop.Services;

namespace Aprs.Desktop.Views;

public sealed partial class AboutWindow : Window
{
    private readonly UpdateCheckerService updateChecker = new();

    public AboutWindow()
    {
        InitializeComponent();

        // Version from assembly metadata.
        VersionText.Text = $"Version {Services.AppVersion.Informational}";

        // Runtime info.
        RuntimeText.Text = $".NET {Environment.Version}";

        // Platform.
        var os = RuntimeInformation.OSDescription;
        var arch = RuntimeInformation.OSArchitecture;
        PlatformText.Text = $"{os} ({arch})";

        // Operator callsign from settings.
        var callsign = JsonAppSettingsStore.Default.Load().Station.Callsign;
        CallsignText.Text = string.IsNullOrWhiteSpace(callsign) ? "Not configured" : callsign.ToUpperInvariant();

        CheckUpdateButton.Click += async (_, _) => await CheckForUpdatesAsync();
    }

    private async System.Threading.Tasks.Task CheckForUpdatesAsync()
    {
        CheckUpdateButton.IsEnabled = false;
        UpdateStatusText.Text = "Checking…";
        UpdateDetailText.IsVisible = false;

        try
        {
            var result = await updateChecker.CheckAsync();

            if (result.UpdateAvailable)
            {
                UpdateStatusText.Text = $"✓ Update available: v{result.LatestVersion}";
                UpdateStatusText.Foreground = Avalonia.Media.Brush.Parse("#15803d");
                UpdateDetailText.Text = $"{result.ReleaseName}\nVisit github.com/KE4CON/APRS-Command/releases to download.";
                UpdateDetailText.IsVisible = true;
            }
            else if (result.LatestVersion is not null)
            {
                UpdateStatusText.Text = $"You are up to date (latest: v{result.LatestVersion})";
                UpdateStatusText.Foreground = Avalonia.Media.Brush.Parse("#475569");
                UpdateDetailText.IsVisible = false;
            }
            else
            {
                UpdateStatusText.Text = result.ReleaseName ?? "Could not check for updates.";
                UpdateStatusText.Foreground = Avalonia.Media.Brush.Parse("#b91c1b");
            }
        }
        catch
        {
            UpdateStatusText.Text = "Update check failed — check your internet connection.";
            UpdateStatusText.Foreground = Avalonia.Media.Brush.Parse("#b91c1b");
        }
        finally
        {
            CheckUpdateButton.IsEnabled = true;
        }
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
