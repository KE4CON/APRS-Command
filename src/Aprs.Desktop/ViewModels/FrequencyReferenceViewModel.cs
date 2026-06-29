using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aprs.Desktop.Configuration;

namespace Aprs.Desktop.ViewModels;

/// <summary>
/// Drives the frequency reference panel. Displays standard APRS and EmComm
/// frequencies alongside operator-added local entries. Entries can be added,
/// edited, deleted, and reordered. Persisted to AppSettings.
/// </summary>
public sealed class FrequencyReferenceViewModel : INotifyPropertyChanged
{
    private readonly IAppSettingsStore store;
    private FrequencyEntry? selectedEntry;
    private string editName         = string.Empty;
    private string editFrequency    = string.Empty;
    private string editMode         = string.Empty;
    private string editNotes        = string.Empty;
    private string statusText       = string.Empty;
    private bool isEditing;

    public FrequencyReferenceViewModel(IAppSettingsStore store)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));

        Entries       = [];
        AddCommand    = new DesktopCommand(BeginAdd);
        EditCommand   = new DesktopCommand(BeginEdit, () => SelectedEntry is not null);
        DeleteCommand = new DesktopCommand(DeleteSelected, () => SelectedEntry is not null);
        SaveCommand   = new DesktopCommand(CommitEdit, () => IsEditing);
        CancelCommand = new DesktopCommand(CancelEdit, () => IsEditing);
        ResetCommand  = new DesktopCommand(ResetToDefaults);
        CopyCommand   = new DesktopCommand(CopyToClipboard, () => SelectedEntry is not null);

        Load();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<FrequencyEntry> Entries { get; }

    public FrequencyEntry? SelectedEntry
    {
        get => selectedEntry;
        set
        {
            if (selectedEntry != value)
            {
                selectedEntry = value;
                OnPropertyChanged();
                RefreshEditFields();
            }
        }
    }

    public bool IsEditing
    {
        get => isEditing;
        private set { if (isEditing != value) { isEditing = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotEditing)); } }
    }

    public bool IsNotEditing => !isEditing;

    public string EditName
    {
        get => editName;
        set { if (editName != value) { editName = value; OnPropertyChanged(); } }
    }

    public string EditFrequency
    {
        get => editFrequency;
        set { if (editFrequency != value) { editFrequency = value; OnPropertyChanged(); } }
    }

    public string EditMode
    {
        get => editMode;
        set { if (editMode != value) { editMode = value; OnPropertyChanged(); } }
    }

    public string EditNotes
    {
        get => editNotes;
        set { if (editNotes != value) { editNotes = value; OnPropertyChanged(); } }
    }

    public string StatusText
    {
        get => statusText;
        private set { if (statusText != value) { statusText = value; OnPropertyChanged(); } }
    }

    public DesktopCommand AddCommand    { get; }
    public DesktopCommand EditCommand   { get; }
    public DesktopCommand DeleteCommand { get; }
    public DesktopCommand SaveCommand   { get; }
    public DesktopCommand CancelCommand { get; }
    public DesktopCommand ResetCommand  { get; }
    public DesktopCommand CopyCommand   { get; }

    private bool isAddMode; // true = adding new, false = editing existing

    private void Load()
    {
        Entries.Clear();
        var entries = store.Load().FrequencyReference.Entries;
        foreach (var e in entries) Entries.Add(e);
    }

    private void Persist()
    {
        var list = Entries.ToList();
        store.Update(s => s with
        {
            FrequencyReference = new FrequencyReferenceSettings(list)
        });
    }

    private void BeginAdd()
    {
        isAddMode    = true;
        IsEditing    = true;
        EditName     = string.Empty;
        EditFrequency = string.Empty;
        EditMode     = "FM Simplex";
        EditNotes    = string.Empty;
        StatusText   = "Enter details for the new frequency.";
    }

    private void BeginEdit()
    {
        if (SelectedEntry is null) return;
        isAddMode    = false;
        IsEditing    = true;
        RefreshEditFields();
        StatusText   = "Edit the frequency details, then click Save.";
    }

    private void CommitEdit()
    {
        var name = EditName.Trim();
        var freq = EditFrequency.Trim();
        var mode = EditMode.Trim();

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(freq))
        {
            StatusText = "Name and frequency are required.";
            return;
        }

        var entry = new FrequencyEntry(name, freq, mode, string.IsNullOrWhiteSpace(EditNotes) ? null : EditNotes.Trim());

        if (isAddMode)
        {
            Entries.Add(entry);
            SelectedEntry = entry;
        }
        else if (SelectedEntry is not null)
        {
            var idx = Entries.IndexOf(SelectedEntry);
            if (idx >= 0) Entries[idx] = entry;
            SelectedEntry = entry;
        }

        IsEditing  = false;
        StatusText = "Saved.";
        Persist();
    }

    private void CancelEdit()
    {
        IsEditing  = false;
        StatusText = string.Empty;
        RefreshEditFields();
    }

    private void DeleteSelected()
    {
        if (SelectedEntry is null) return;
        var idx = Entries.IndexOf(SelectedEntry);
        Entries.Remove(SelectedEntry);
        SelectedEntry = Entries.Count > 0
            ? Entries[Math.Clamp(idx, 0, Entries.Count - 1)]
            : null;
        StatusText = "Entry deleted.";
        Persist();
    }

    private void ResetToDefaults()
    {
        Entries.Clear();
        foreach (var e in FrequencyEntry.Defaults) Entries.Add(e);
        SelectedEntry = Entries.FirstOrDefault();
        StatusText = "Reset to default frequencies.";
        Persist();
    }

    private void CopyToClipboard()
    {
        if (SelectedEntry is null) return;
        var text = $"{SelectedEntry.FrequencyMhz} MHz  {SelectedEntry.Mode}  {SelectedEntry.Name}";
        if (Avalonia.Application.Current?.ApplicationLifetime is
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            _ = desktop.MainWindow?.Clipboard?.SetTextAsync(text);
        }
        StatusText = "Copied to clipboard.";
    }

    private void RefreshEditFields()
    {
        if (SelectedEntry is null) return;
        EditName      = SelectedEntry.Name;
        EditFrequency = SelectedEntry.FrequencyMhz;
        EditMode      = SelectedEntry.Mode;
        EditNotes     = SelectedEntry.Notes ?? string.Empty;
    }

    public static FrequencyReferenceViewModel CreateDesignTime()
        => new(new InMemoryAppSettingsStore());

    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
