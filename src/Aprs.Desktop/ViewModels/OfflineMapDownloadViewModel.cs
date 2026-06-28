using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aprs.Mapping;

namespace Aprs.Desktop.ViewModels;

/// <summary>
/// Drives the offline map download window. Allows operators to pre-download
/// map tiles for a defined geographic area before going to a field location
/// without internet access.
/// </summary>
public sealed class OfflineMapDownloadViewModel : INotifyPropertyChanged
{
    private readonly IOfflineMapDownloadManager downloadManager;
    private CancellationTokenSource? downloadCts;

    // ── Area definition fields ────────────────────────────────────────
    private string areaName = "Field Operation Area";
    private double northLat = 34.0;
    private double southLat = 33.5;
    private double eastLon  = -84.0;
    private double westLon  = -85.0;
    private int minZoom = 8;
    private int maxZoom = 14;
    private int selectedProviderIndex;

    // ── Status fields ─────────────────────────────────────────────────
    private string statusText  = string.Empty;
    private string estimateText = string.Empty;
    private double progressPercent;
    private bool isDownloading;
    private bool hasEstimate;

    public OfflineMapDownloadViewModel(IOfflineMapDownloadManager downloadManager)
    {
        this.downloadManager = downloadManager;
        EstimateCommand  = new DesktopCommand(RunEstimate);
        DownloadCommand  = new DesktopCommand(async () => await StartDownloadAsync());
        CancelCommand    = new DesktopCommand(CancelDownload);

        Providers = [
            new MapTileProviderDefinition("OpenStreetMap", "https://tile.openstreetmap.org/{z}/{x}/{y}.png",
                0, 19, "© OpenStreetMap contributors", true, true),
            new MapTileProviderDefinition("USGS Topo", "https://basemap.nationalmap.gov/arcgis/rest/services/USGSTopo/MapServer/tile/{z}/{y}/{x}",
                0, 16, "USGS National Map", true, true),
        ];
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<MapTileProviderDefinition> Providers { get; }

    public string AreaName
    {
        get => areaName;
        set { if (areaName != value) { areaName = value; OnPropertyChanged(); ResetEstimate(); } }
    }

    public double NorthLat { get => northLat; set { if (northLat != value) { northLat = value; OnPropertyChanged(); ResetEstimate(); } } }
    public double SouthLat { get => southLat; set { if (southLat != value) { southLat = value; OnPropertyChanged(); ResetEstimate(); } } }
    public double EastLon  { get => eastLon;  set { if (eastLon  != value) { eastLon  = value; OnPropertyChanged(); ResetEstimate(); } } }
    public double WestLon  { get => westLon;  set { if (westLon  != value) { westLon  = value; OnPropertyChanged(); ResetEstimate(); } } }
    public int MinZoom { get => minZoom; set { if (minZoom != value) { minZoom = value; OnPropertyChanged(); ResetEstimate(); } } }
    public int MaxZoom { get => maxZoom; set { if (maxZoom != value) { maxZoom = value; OnPropertyChanged(); ResetEstimate(); } } }

    public int SelectedProviderIndex
    {
        get => selectedProviderIndex;
        set { if (selectedProviderIndex != value) { selectedProviderIndex = value; OnPropertyChanged(); ResetEstimate(); } }
    }

    public string StatusText
    {
        get => statusText;
        private set { if (statusText != value) { statusText = value; OnPropertyChanged(); } }
    }

    public string EstimateText
    {
        get => estimateText;
        private set { if (estimateText != value) { estimateText = value; OnPropertyChanged(); } }
    }

    public double ProgressPercent
    {
        get => progressPercent;
        private set { if (progressPercent != value) { progressPercent = value; OnPropertyChanged(); } }
    }

    public bool IsDownloading
    {
        get => isDownloading;
        private set { if (isDownloading != value) { isDownloading = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotDownloading)); } }
    }

    public bool IsNotDownloading => !isDownloading;

    public bool HasEstimate
    {
        get => hasEstimate;
        private set { if (hasEstimate != value) { hasEstimate = value; OnPropertyChanged(); } }
    }

    public DesktopCommand EstimateCommand  { get; }
    public DesktopCommand DownloadCommand  { get; }
    public DesktopCommand CancelCommand    { get; }

    // ── Active jobs list ──────────────────────────────────────────────
    public ObservableCollection<OfflineMapDownloadJob> ActiveJobs { get; } = [];

    // ── Methods ───────────────────────────────────────────────────────

    private void RunEstimate()
    {
        try
        {
            var area   = BuildArea();
            var est    = downloadManager.EstimateJob(area, allowLargeArea: false);
            var sizeMb = est.EstimatedSizeBytes / 1024 / 1024;
            EstimateText = $"~{est.TotalTiles:N0} tiles  ·  ~{sizeMb:N0} MB  ·  Ready to download";
            if (est.Warnings.Count > 0)
                EstimateText += $"  ·  ⚠ {est.Warnings[0]}";
            HasEstimate = true;
            StatusText = string.Empty;
        }
        catch (Exception ex)
        {
            EstimateText = string.Empty;
            StatusText = $"Estimate failed: {ex.Message}";
            HasEstimate = false;
        }
    }

    private async Task StartDownloadAsync()
    {
        if (IsDownloading) return;

        try
        {
            var area = BuildArea();
            var job  = downloadManager.CreateJob(area);

            ActiveJobs.Add(job);
            IsDownloading  = true;
            ProgressPercent = 0;
            StatusText = "Starting download…";

            downloadCts = new CancellationTokenSource();

            // Progress polling timer.
            var progressTimer = new System.Timers.Timer(500);
            progressTimer.Elapsed += (_, _) =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    ProgressPercent = job.ProgressPercentage;
                    StatusText = $"Downloading… {job.CompletedTiles:N0} / {job.TotalTiles:N0} tiles " +
                                 $"({job.ProgressPercentage:F0}%)  ·  {job.FailedTiles} failed  ·  {job.SkippedExistingTiles} skipped";
                });
            };
            progressTimer.Start();

            var completed = await downloadManager.StartJobAsync(job, area, false, downloadCts.Token)
                                                 .ConfigureAwait(false);

            progressTimer.Stop();

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                ProgressPercent = 100;
                StatusText = completed.CurrentStatus switch
                {
                    OfflineMapDownloadStatus.Completed  => $"✓ Download complete — {completed.CompletedTiles:N0} tiles downloaded, {completed.SkippedExistingTiles:N0} already cached.",
                    OfflineMapDownloadStatus.Cancelled  => "Download cancelled.",
                    OfflineMapDownloadStatus.Failed     => $"Download finished with errors: {completed.FailedTiles:N0} tiles failed.",
                    _                                   => completed.CurrentStatus.ToString()
                };
            });
        }
        catch (OperationCanceledException)
        {
            StatusText = "Download cancelled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Download failed: {ex.Message}";
        }
        finally
        {
            IsDownloading = false;
            downloadCts?.Dispose();
            downloadCts = null;
        }
    }

    private void CancelDownload()
    {
        downloadCts?.Cancel();
        foreach (var job in ActiveJobs)
            downloadManager.CancelJob(job);
    }

    private OfflineMapDownloadArea BuildArea()
    {
        var provider = Providers[Math.Clamp(SelectedProviderIndex, 0, Providers.Count - 1)];
        return new OfflineMapDownloadArea(
            AreaName:             AreaName.Trim(),
            NorthLatitude:        NorthLat,
            SouthLatitude:        SouthLat,
            EastLongitude:        EastLon,
            WestLongitude:        WestLon,
            MinimumZoom:          MinZoom,
            MaximumZoom:          MaxZoom,
            SelectedMapProvider:  provider,
            CreatedAtUtc:         DateTimeOffset.UtcNow,
            UpdatedAtUtc:         DateTimeOffset.UtcNow,
            Notes:                null);
    }

    private void ResetEstimate()
    {
        HasEstimate  = false;
        EstimateText = string.Empty;
    }

    public static OfflineMapDownloadViewModel CreateDesignTime()
    {
        var vm = new OfflineMapDownloadViewModel(new NullOfflineMapDownloadManager());
        vm.EstimateText = "~2,400 tiles  ·  ~60 MB  ·  Ready to download";
        vm.HasEstimate  = true;
        return vm;
    }

    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

/// <summary>No-op download manager for design-time preview.</summary>
file sealed class NullOfflineMapDownloadManager : IOfflineMapDownloadManager
{
    public OfflineMapDownloadJob CreateJob(OfflineMapDownloadArea area)
        => new(Guid.NewGuid(), area);
    public OfflineMapDownloadEstimate EstimateJob(OfflineMapDownloadArea area, bool allowLargeArea = false)
        => new(area, [], [], 2400, 60_000_000, false, []);
    public Task<OfflineMapDownloadJob> StartJobAsync(OfflineMapDownloadJob job, OfflineMapDownloadArea area, bool allowLargeArea, CancellationToken cancellationToken)
        => Task.FromResult(job);
    public void CancelJob(OfflineMapDownloadJob job) { }
}
