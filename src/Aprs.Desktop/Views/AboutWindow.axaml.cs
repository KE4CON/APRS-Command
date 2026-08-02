using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Aprs.Desktop.Configuration;
using Aprs.Desktop.Services;
using Aprs.Services;

namespace Aprs.Desktop.Views;

public sealed partial class AboutWindow : Window
{
    private readonly UpdateCheckerService updateChecker = new();
    private readonly IDeviceIdDatabaseUpdateService? deviceDbUpdater;

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

        // Device-ID database status + manual refresh (the auto-refresh engine lives in the runtime).
        deviceDbUpdater = (Application.Current as App)?.Runtime?.GetService<IDeviceIdDatabaseUpdateService>();
        RefreshDeviceDbStatus();
        UpdateDeviceDbButton.IsEnabled = deviceDbUpdater is not null;
        UpdateDeviceDbButton.Click += async (_, _) => await UpdateDeviceDbAsync();
    }

    private void RefreshDeviceDbStatus()
    {
        DeviceDbStatusText.Text = deviceDbUpdater?.LastUpdatedUtc is { } stamp
            ? $"Updated {stamp.ToLocalTime():d}"
            : "Using the built-in copy";
    }

    private async System.Threading.Tasks.Task UpdateDeviceDbAsync()
    {
        if (deviceDbUpdater is null) return;

        UpdateDeviceDbButton.IsEnabled = false;
        DeviceDbStatusText.Text = "Updating…";
        try
        {
            var result = await deviceDbUpdater.UpdateAsync(force: true);
            DeviceDbStatusText.Text = result.Outcome == DeviceIdUpdateOutcome.Updated
                ? $"Updated just now ({result.PatternCount} device types)"
                : "Update failed — check your internet connection.";
            DeviceDbStatusText.Foreground = Avalonia.Media.Brush.Parse(
                result.Outcome == DeviceIdUpdateOutcome.Updated ? "#15803d" : "#b91c1b");
        }
        catch
        {
            DeviceDbStatusText.Text = "Update failed — check your internet connection.";
            DeviceDbStatusText.Foreground = Avalonia.Media.Brush.Parse("#b91c1b");
        }
        finally
        {
            UpdateDeviceDbButton.IsEnabled = true;
        }
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
