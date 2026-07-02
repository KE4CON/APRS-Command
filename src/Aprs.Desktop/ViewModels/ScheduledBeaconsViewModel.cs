using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

/// <summary>ViewModel for the Scheduled Beacons window.</summary>
public sealed class ScheduledBeaconsViewModel : INotifyPropertyChanged
{
    private readonly ScheduledBeaconService service;
    private ScheduledBeaconEntryEditViewModel? selectedEntry;
    private bool isEditing;

    public ScheduledBeaconsViewModel(ScheduledBeaconService service)
    {
        this.service = service;
        Entries      = new ObservableCollection<ScheduledBeaconEntryEditViewModel>(
            service.Entries.Select(e => new ScheduledBeaconEntryEditViewModel(e)));

        AddCommand    = new DesktopCommand(AddEntry);
        RemoveCommand = new DesktopCommand(RemoveSelected);
        SaveCommand   = new DesktopCommand(SaveEntry);
        CancelCommand = new DesktopCommand(CancelEdit);
    }

    public ObservableCollection<ScheduledBeaconEntryEditViewModel> Entries { get; }

    public ScheduledBeaconEntryEditViewModel? SelectedEntry
    {
        get => selectedEntry;
        set { selectedEntry = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSelection)); }
    }

    public bool HasSelection  => selectedEntry is not null;
    public bool IsEditing     { get => isEditing;  set { isEditing = value; OnPropertyChanged(); } }
    public string StatusText  => service.StatusSummary;

    public DesktopCommand AddCommand    { get; }
    public DesktopCommand RemoveCommand { get; }
    public DesktopCommand SaveCommand   { get; }
    public DesktopCommand CancelCommand { get; }

    private void AddEntry()
    {
        var entry = ScheduledBeaconEntry.CreateDefault();
        var vm    = new ScheduledBeaconEntryEditViewModel(entry);
        Entries.Add(vm);
        SelectedEntry = vm;
        IsEditing     = true;
    }

    private void RemoveSelected()
    {
        if (selectedEntry is null) return;
        service.Remove(selectedEntry.Id);
        Entries.Remove(selectedEntry);
        SelectedEntry = Entries.FirstOrDefault();
        IsEditing     = false;
        OnPropertyChanged(nameof(StatusText));
    }

    private void SaveEntry()
    {
        if (selectedEntry is null) return;
        var model = selectedEntry.ToModel();
        service.Add(model);
        selectedEntry.Load(model);
        IsEditing = false;
        OnPropertyChanged(nameof(StatusText));
    }

    private void CancelEdit() => IsEditing = false;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Editable row for a single scheduled beacon entry.</summary>
public sealed class ScheduledBeaconEntryEditViewModel : INotifyPropertyChanged
{
    public ScheduledBeaconEntryEditViewModel(ScheduledBeaconEntry model) => Load(model);

    public Guid    Id            { get; private set; }
    public string  Label         { get; set; } = string.Empty;
    public int     FireAtHour    { get; set; } = 12;
    public int     FireAtMinute  { get; set; } = 0;
    public bool    Monday        { get; set; } = true;
    public bool    Tuesday       { get; set; } = true;
    public bool    Wednesday     { get; set; } = true;
    public bool    Thursday      { get; set; } = true;
    public bool    Friday        { get; set; } = true;
    public bool    Saturday      { get; set; }
    public bool    Sunday        { get; set; }
    public bool    Enabled       { get; set; } = true;
    public string  CustomComment { get; set; } = string.Empty;

    public string DisplayTime  => $"{FireAtHour:D2}:{FireAtMinute:D2}";
    public string DaysDisplay  => ToModel().DaysDisplay;

    public void Load(ScheduledBeaconEntry m)
    {
        Id            = m.Id;
        Label         = m.Label;
        FireAtHour    = m.FireAt.Hour;
        FireAtMinute  = m.FireAt.Minute;
        Monday        = m.Monday;
        Tuesday       = m.Tuesday;
        Wednesday     = m.Wednesday;
        Thursday      = m.Thursday;
        Friday        = m.Friday;
        Saturday      = m.Saturday;
        Sunday        = m.Sunday;
        Enabled       = m.Enabled;
        CustomComment = m.CustomComment ?? string.Empty;
    }

    public ScheduledBeaconEntry ToModel() => new(
        Id:            Id,
        Label:         Label,
        FireAt:        new TimeOnly(Math.Clamp(FireAtHour, 0, 23), Math.Clamp(FireAtMinute, 0, 59)),
        Monday:        Monday,
        Tuesday:       Tuesday,
        Wednesday:     Wednesday,
        Thursday:      Thursday,
        Friday:        Friday,
        Saturday:      Saturday,
        Sunday:        Sunday,
        Enabled:       Enabled,
        CustomComment: string.IsNullOrWhiteSpace(CustomComment) ? null : CustomComment);

    public event PropertyChangedEventHandler? PropertyChanged;
}
