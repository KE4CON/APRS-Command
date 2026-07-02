using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Aprs.Desktop.Configuration;
using Aprs.Desktop.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class RepeaterDirectoryViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IAppSettingsStore store;
    private readonly RepeaterBookService service = new();
    private CancellationTokenSource? searchCts;

    public RepeaterDirectoryViewModel(IAppSettingsStore store)
    {
        this.store = store;
        SearchCommand = new RelayCommand2(_ => _ = SearchAsync(), _ => !IsSearching);
        CopyFrequencyCommand = new RelayCommand2(
            o => { if (o is RepeaterEntry r) CopyToClipboard(r.Frequency); });
    }

    // ── State ─────────────────────────────────────────────────────────────────
    private bool isSearching;
    public bool IsSearching
    {
        get => isSearching;
        private set { isSearching = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotSearching)); }
    }
    public bool IsNotSearching => !isSearching;

    private string statusMessage = string.Empty;
    public string StatusMessage
    {
        get => statusMessage;
        private set { statusMessage = value; OnPropertyChanged(); }
    }

    private bool hasError;
    public bool HasError
    {
        get => hasError;
        private set { hasError = value; OnPropertyChanged(); }
    }

    private bool hasResults;
    public bool HasResults
    {
        get => hasResults;
        private set { hasResults = value; OnPropertyChanged(); }
    }

    private bool needsToken;
    public bool NeedsToken
    {
        get => needsToken;
        private set { needsToken = value; OnPropertyChanged(); }
    }

    // ── Filters ───────────────────────────────────────────────────────────────
    private string filterText = string.Empty;
    public string FilterText
    {
        get => filterText;
        set
        {
            filterText = value;
            OnPropertyChanged();
            ApplyFilter();
        }
    }

    private bool showOperationalOnly = true;
    public bool ShowOperationalOnly
    {
        get => showOperationalOnly;
        set { showOperationalOnly = value; OnPropertyChanged(); ApplyFilter(); }
    }

    // ── Data ──────────────────────────────────────────────────────────────────
    private IReadOnlyList<RepeaterEntry> allRepeaters = [];
    public ObservableCollection<RepeaterEntry> Repeaters { get; } = [];

    private RepeaterEntry? selectedRepeater;
    public RepeaterEntry? SelectedRepeater
    {
        get => selectedRepeater;
        set { selectedRepeater = value; OnPropertyChanged(); }
    }

    // ── Commands ──────────────────────────────────────────────────────────────
    public ICommand SearchCommand       { get; }
    public ICommand CopyFrequencyCommand { get; }

    // ── Search ────────────────────────────────────────────────────────────────
    private async Task SearchAsync()
    {
        searchCts?.Cancel();
        searchCts = new CancellationTokenSource();
        var ct = searchCts.Token;

        IsSearching   = true;
        HasError      = false;
        HasResults    = false;
        NeedsToken    = false;
        StatusMessage = "Searching RepeaterBook...";
        Repeaters.Clear();

        try
        {
            var settings = store.Load();
            var token    = settings.RepeaterBook.ApiToken;
            var lat      = settings.Station.Latitude;
            var lng      = settings.Station.Longitude;
            var radius   = settings.RepeaterBook.SearchRadiusMiles;

            var result = await service.SearchProximityAsync(token ?? string.Empty, lat, lng, radius, ct)
                                      .ConfigureAwait(true);

            switch (result.Status)
            {
                case RepeaterBookStatus.Success:
                    allRepeaters = result.Repeaters;
                    ApplyFilter();
                    StatusMessage = $"{result.Repeaters.Count} repeaters found within {radius} miles.";
                    HasResults = Repeaters.Count > 0;
                    break;

                case RepeaterBookStatus.NoToken:
                    NeedsToken    = true;
                    StatusMessage = result.Message;
                    break;

                case RepeaterBookStatus.Empty:
                    StatusMessage = result.Message;
                    HasResults    = false;
                    break;

                default:
                    HasError      = true;
                    StatusMessage = result.Message;
                    break;
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            IsSearching = false;
        }
    }

    private void ApplyFilter()
    {
        Repeaters.Clear();
        var q = filterText.Trim();

        foreach (var r in allRepeaters)
        {
            if (showOperationalOnly && !r.Operational) continue;

            if (!string.IsNullOrEmpty(q))
            {
                var q2 = q.ToUpperInvariant();
                if (!r.Callsign.Contains(q2, StringComparison.OrdinalIgnoreCase)
                 && !r.Frequency.Contains(q, StringComparison.OrdinalIgnoreCase)
                 && !r.City.Contains(q, StringComparison.OrdinalIgnoreCase)
                 && !r.Notes.Contains(q, StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            Repeaters.Add(r);
        }

        HasResults = Repeaters.Count > 0;
    }

    private static void CopyToClipboard(string text)
    {
        try
        {
            // Avalonia clipboard access requires the main thread
            Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
            {
                var clipboard = Avalonia.Application.Current?.ApplicationLifetime is
                    Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime lifetime
                    ? lifetime.MainWindow?.Clipboard
                    : null;
                if (clipboard is not null)
                    await clipboard.SetTextAsync(text);
            });
        }
        catch { }
    }

    public void Dispose()
    {
        searchCts?.Cancel();
        searchCts?.Dispose();
        service.Dispose();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

// Relay command (reuse the file-scoped one from session templates in a shared location)
file sealed class RelayCommand2(Action<object?> execute, Func<object?, bool>? canExecute = null)
    : ICommand
{
    public bool CanExecute(object? p) => canExecute?.Invoke(p) ?? true;
    public void Execute(object? p) => execute(p);
    public event EventHandler? CanExecuteChanged { add { } remove { } }
}
