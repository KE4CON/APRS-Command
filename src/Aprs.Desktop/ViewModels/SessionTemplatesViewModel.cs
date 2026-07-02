using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Aprs.Desktop.Configuration;

namespace Aprs.Desktop.ViewModels;

public sealed class SessionTemplatesViewModel : INotifyPropertyChanged
{
    private readonly IAppSettingsStore store;
    private readonly Action<string> applyBeaconSettings;

    public SessionTemplatesViewModel(IAppSettingsStore store, Action<string> applyBeaconSettings)
    {
        this.store = store;
        this.applyBeaconSettings = applyBeaconSettings;
        Load();

        ApplyCommand        = new RelayCommand(_ => ApplySelected(),   _ => SelectedTemplate is not null);
        SaveCustomCommand   = new RelayCommand(_ => SaveCustom(),      _ => CanSaveCustom);
        DeleteCustomCommand = new RelayCommand(_ => DeleteSelected(),  _ => SelectedTemplate?.IsBuiltIn == false);
        NewCustomCommand    = new RelayCommand(_ => StartNewCustom());
    }

    // ── Template list ─────────────────────────────────────────────────────────
    public ObservableCollection<SessionTemplateRow> Templates { get; } = [];

    private SessionTemplateRow? selectedTemplate;
    public SessionTemplateRow? SelectedTemplate
    {
        get => selectedTemplate;
        set
        {
            selectedTemplate = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(CanEditCustom));
            LoadSelectedIntoEditor();
        }
    }

    public bool HasSelection => selectedTemplate is not null;
    public bool CanEditCustom => selectedTemplate?.IsBuiltIn == false;

    // ── Custom template editor ────────────────────────────────────────────────
    private bool isEditingCustom;
    public bool IsEditingCustom
    {
        get => isEditingCustom;
        set { isEditingCustom = value; OnPropertyChanged(); }
    }

    private string customName = string.Empty;
    public string CustomName
    {
        get => customName;
        set { customName = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanSaveCustom)); }
    }

    private string customDescription = string.Empty;
    public string CustomDescription
    {
        get => customDescription;
        set { customDescription = value; OnPropertyChanged(); }
    }

    private string customBeaconPath = "WIDE1-1,WIDE2-1";
    public string CustomBeaconPath
    {
        get => customBeaconPath;
        set { customBeaconPath = value; OnPropertyChanged(); }
    }

    private string customBeaconComment = string.Empty;
    public string CustomBeaconComment
    {
        get => customBeaconComment;
        set { customBeaconComment = value; OnPropertyChanged(); }
    }

    private int customBeaconInterval = 10;
    public int CustomBeaconInterval
    {
        get => customBeaconInterval;
        set { customBeaconInterval = value; OnPropertyChanged(); }
    }

    private int customFilterRadius = 75;
    public int CustomFilterRadius
    {
        get => customFilterRadius;
        set { customFilterRadius = value; OnPropertyChanged(); }
    }

    private string customOperatorNotes = string.Empty;
    public string CustomOperatorNotes
    {
        get => customOperatorNotes;
        set { customOperatorNotes = value; OnPropertyChanged(); }
    }

    private string? editingId;

    public bool CanSaveCustom => !string.IsNullOrWhiteSpace(CustomName);

    // ── Status ────────────────────────────────────────────────────────────────
    private string statusText = string.Empty;
    public string StatusText
    {
        get => statusText;
        set { statusText = value; OnPropertyChanged(); }
    }

    // ── Commands ──────────────────────────────────────────────────────────────
    public ICommand ApplyCommand        { get; }
    public ICommand SaveCustomCommand   { get; }
    public ICommand DeleteCustomCommand { get; }
    public ICommand NewCustomCommand    { get; }

    // ── Load ──────────────────────────────────────────────────────────────────
    private void Load()
    {
        Templates.Clear();
        var settings = store.Load();
        var all = SessionTemplateSettings.BuiltInTemplates
            .Concat(settings.SessionTemplates.Templates.Where(t => !t.IsBuiltIn))
            .ToList();

        foreach (var t in all)
            Templates.Add(new SessionTemplateRow(t));
    }

    private void LoadSelectedIntoEditor()
    {
        if (selectedTemplate is null)
        {
            IsEditingCustom = false;
            return;
        }

        var t = selectedTemplate.Template;
        if (!t.IsBuiltIn)
        {
            editingId            = t.Id;
            CustomName           = t.Name;
            CustomDescription    = t.Description;
            CustomBeaconPath     = t.BeaconPath ?? "WIDE1-1,WIDE2-1";
            CustomBeaconComment  = t.BeaconComment ?? string.Empty;
            CustomBeaconInterval = t.AprsIsBeaconMinutes ?? 10;
            CustomFilterRadius   = t.FilterRadiusMiles ?? 75;
            CustomOperatorNotes  = t.OperatorNotes ?? string.Empty;
            IsEditingCustom      = true;
        }
        else
        {
            IsEditingCustom = false;
        }
    }

    // ── Apply ─────────────────────────────────────────────────────────────────
    private void ApplySelected()
    {
        if (selectedTemplate is null) return;
        var t = selectedTemplate.Template;
        var settings = store.Load();
        var station  = settings.Station;

        var updated = station with
        {
            BeaconPath           = t.BeaconPath ?? station.BeaconPath,
            StationComment       = t.BeaconComment ?? station.StationComment,
            AprsIsBeaconMinutes  = t.AprsIsBeaconMinutes ?? station.AprsIsBeaconMinutes,
            FilterRadiusKm       = t.FilterRadiusMiles.HasValue
                                       ? (int)Math.Round(t.FilterRadiusMiles.Value / 0.621371)
                                       : station.FilterRadiusKm,
        };

        store.Save(settings with { Station = updated });
        applyBeaconSettings($"Template '{t.Name}' applied. Beacon path, comment, interval, and filter radius updated. Save is not required — settings took effect immediately.");
        StatusText = $"Applied: {t.Name}";
    }

    // ── Custom template editing ───────────────────────────────────────────────
    public void StartNewCustom()
    {
        editingId            = null;
        CustomName           = string.Empty;
        CustomDescription    = string.Empty;
        CustomBeaconPath     = "WIDE1-1,WIDE2-1";
        CustomBeaconComment  = string.Empty;
        CustomBeaconInterval = 10;
        CustomFilterRadius   = 75;
        CustomOperatorNotes  = string.Empty;
        IsEditingCustom      = true;
        SelectedTemplate     = null;
    }

    private void SaveCustom()
    {
        var settings  = store.Load();
        var existing  = settings.SessionTemplates.Templates.Where(t => !t.IsBuiltIn).ToList();

        if (editingId is not null)
            existing.RemoveAll(t => t.Id == editingId);

        var newTemplate = SessionTemplate.Create(
            name:                CustomName.Trim(),
            description:         CustomDescription.Trim(),
            eventType:           SessionEventType.Custom,
            isBuiltIn:           false,
            beaconPath:          string.IsNullOrWhiteSpace(CustomBeaconPath) ? null : CustomBeaconPath.Trim(),
            beaconComment:       string.IsNullOrWhiteSpace(CustomBeaconComment) ? null : CustomBeaconComment.Trim(),
            aprsIsBeaconMinutes: CustomBeaconInterval,
            filterRadiusMiles:   CustomFilterRadius,
            operatorNotes:       string.IsNullOrWhiteSpace(CustomOperatorNotes) ? null : CustomOperatorNotes.Trim());

        if (editingId is not null)
            newTemplate = newTemplate with { Id = editingId };

        existing.Add(newTemplate);
        store.Save(settings with
        {
            SessionTemplates = new SessionTemplateSettings(existing)
        });

        editingId   = newTemplate.Id;
        StatusText  = $"Saved: {newTemplate.Name}";
        Load();

        // Re-select the saved template
        SelectedTemplate = Templates.FirstOrDefault(t => t.Template.Id == newTemplate.Id);
    }

    private void DeleteSelected()
    {
        if (selectedTemplate?.IsBuiltIn != false) return;
        var id = selectedTemplate.Template.Id;
        var settings = store.Load();
        var remaining = settings.SessionTemplates.Templates
            .Where(t => t.Id != id)
            .ToList();
        store.Save(settings with { SessionTemplates = new SessionTemplateSettings(remaining) });
        Load();
        SelectedTemplate = null;
        IsEditingCustom = false;
        StatusText = "Template deleted.";
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class SessionTemplateRow(SessionTemplate template)
{
    public SessionTemplate Template { get; } = template;
    public string Name        => Template.Name;
    public string Description => Template.Description;
    public string EventTypeLabel => Template.EventType switch
    {
        SessionEventType.PublicService    => "🎪 Public Service",
        SessionEventType.EmergencyComms   => "🚨 EmComm",
        SessionEventType.Net              => "📻 Net",
        SessionEventType.PortableOperation=> "🏔 Portable",
        SessionEventType.Skywarn          => "⛈ Skywarn",
        _                                 => "⚙ Custom"
    };
    public bool IsBuiltIn     => Template.IsBuiltIn;
    public string OperatorNotes => Template.OperatorNotes ?? string.Empty;
    public string BeaconSummary =>
        $"Path: {Template.BeaconPath ?? "(unchanged)"}  ·  " +
        $"Comment: {(string.IsNullOrEmpty(Template.BeaconComment) ? "(unchanged)" : Template.BeaconComment)}  ·  " +
        $"Interval: {(Template.AprsIsBeaconMinutes.HasValue ? $"{Template.AprsIsBeaconMinutes} min" : "(unchanged)")}  ·  " +
        $"Filter: {(Template.FilterRadiusMiles.HasValue ? $"{Template.FilterRadiusMiles} mi" : "(unchanged)")}";
}

// Minimal relay command implementation
file sealed class RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null) : ICommand
{
    public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => execute(parameter);
    public event EventHandler? CanExecuteChanged;
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
