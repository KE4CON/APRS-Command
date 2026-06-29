using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aprs.Desktop.Configuration;
using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

/// <summary>
/// Net script editor — operators pre-write net preambles and announcements
/// that they can copy to clipboard and read at the start of a net or event.
///
/// Supports template variables resolved at copy time:
///   {{callsign}}  — operator's callsign from station settings
///   {{date}}      — current date (MM/dd/yyyy)
///   {{time}}      — current local time (HH:mm)
///   {{net_name}}  — name of the currently selected script
/// </summary>
public sealed class NetScriptEditorViewModel : INotifyPropertyChanged
{
    private readonly IAppSettingsStore store;
    private NetScriptRowViewModel? selectedScript;
    private string editName = string.Empty;
    private string editBody = string.Empty;
    private string statusText = string.Empty;
    private bool isEditing;

    public NetScriptEditorViewModel(IAppSettingsStore store)
    {
        this.store = store;
        Scripts    = [];
        NewCommand    = new DesktopCommand(NewScript);
        SaveCommand   = new DesktopCommand(SaveScript);
        DeleteCommand = new DesktopCommand(DeleteScript, () => SelectedScript is not null);
        CopyCommand   = new DesktopCommand(CopyToClipboard, () => SelectedScript is not null);
        Load();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<NetScriptRowViewModel> Scripts { get; }

    public NetScriptRowViewModel? SelectedScript
    {
        get => selectedScript;
        set
        {
            if (selectedScript != value)
            {
                selectedScript = value;
                OnPropertyChanged();
                LoadSelectedIntoEditor();
            }
        }
    }

    public string EditName
    {
        get => editName;
        set { if (editName != value) { editName = value; OnPropertyChanged(); } }
    }

    public string EditBody
    {
        get => editBody;
        set { if (editBody != value) { editBody = value; OnPropertyChanged(); } }
    }

    public string StatusText
    {
        get => statusText;
        private set { if (statusText != value) { statusText = value; OnPropertyChanged(); } }
    }

    public bool IsEditing
    {
        get => isEditing;
        private set { if (isEditing != value) { isEditing = value; OnPropertyChanged(); } }
    }

    public DesktopCommand NewCommand    { get; }
    public DesktopCommand SaveCommand   { get; }
    public DesktopCommand DeleteCommand { get; }
    public DesktopCommand CopyCommand   { get; }

    private void NewScript()
    {
        SelectedScript = null;
        EditName = "New Script";
        EditBody = "This is {{callsign}}, net control.\nDate: {{date}}  Time: {{time}}\n\n";
        IsEditing = true;
        StatusText = "Enter a name and script body, then click Save.";
    }

    private void SaveScript()
    {
        var name = EditName.Trim();
        if (string.IsNullOrWhiteSpace(name)) { StatusText = "Name is required."; return; }

        var settings = store.Load().NetScripts;
        var scripts  = settings.Scripts.ToList();

        if (selectedScript is not null)
        {
            // Update existing
            var idx = scripts.FindIndex(s => s.Id == selectedScript.Id);
            if (idx >= 0) scripts[idx] = new NetScript(selectedScript.Id, name, EditBody);
        }
        else
        {
            // Add new
            scripts.Add(new NetScript(Guid.NewGuid(), name, EditBody));
        }

        store.Update(s => s with { NetScripts = new NetScriptSettings(scripts, selectedScript?.Id) });
        Load();
        StatusText = $"Saved '{name}'.";
        IsEditing = false;
    }

    private void DeleteScript()
    {
        if (selectedScript is null) return;
        var name     = selectedScript.Name;
        var settings = store.Load().NetScripts;
        var scripts  = settings.Scripts.Where(s => s.Id != selectedScript.Id).ToList();
        store.Update(s => s with { NetScripts = new NetScriptSettings(scripts, null) });
        Load();
        StatusText = $"Deleted '{name}'.";
    }

    private void CopyToClipboard()
    {
        if (selectedScript is null) return;
        var resolved = ResolveTemplates(EditBody);
        if (Avalonia.Application.Current?.ApplicationLifetime is
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            _ = desktop.MainWindow?.Clipboard?.SetTextAsync(resolved);
        }
        StatusText = "Copied to clipboard — ready to paste into message or read on air.";
    }

    private string ResolveTemplates(string body)
    {
        var settings = store.Load();
        var callsign = settings.Station.Callsign ?? "KE4CON";
        var now      = DateTime.Now;
        return body
            .Replace("{{callsign}}", callsign)
            .Replace("{{date}}", now.ToString("MM/dd/yyyy"))
            .Replace("{{time}}", now.ToString("HH:mm"))
            .Replace("{{net_name}}", EditName);
    }

    private void Load()
    {
        var selected = SelectedScript?.Id;
        Scripts.Clear();
        var settings = store.Load().NetScripts;
        foreach (var s in settings.Scripts)
            Scripts.Add(new NetScriptRowViewModel(s));
        SelectedScript = Scripts.FirstOrDefault(s => s.Id == selected)
                      ?? Scripts.FirstOrDefault();
    }

    private void LoadSelectedIntoEditor()
    {
        if (selectedScript is null) return;
        EditName  = selectedScript.Name;
        EditBody  = selectedScript.Body;
        IsEditing = true;
    }

    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public sealed class NetScriptRowViewModel
{
    public NetScriptRowViewModel(NetScript s)
    {
        Id   = s.Id;
        Name = s.Name;
        Body = s.Body;
        Preview = s.Body.Length > 60 ? s.Body[..57] + "…" : s.Body;
    }

    public Guid   Id      { get; }
    public string Name    { get; }
    public string Body    { get; }
    public string Preview { get; }
}
