using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aprs.Desktop.Configuration;

namespace Aprs.Desktop.ViewModels;

/// <summary>
/// Manages the message template library and exposes it to the Message Center
/// compose area. Templates can be selected from a dropdown to pre-fill the
/// recipient and body fields, and can be created, edited, and deleted in
/// Settings → Message Templates.
/// </summary>
public sealed class MessageTemplatesViewModel : INotifyPropertyChanged
{
    private readonly IAppSettingsStore store;
    private MessageTemplate? selectedTemplate;
    private string editName = string.Empty;
    private string editRecipient = string.Empty;
    private string editBody = string.Empty;
    private bool isEditing;
    private string? editingId;
    private string statusText = string.Empty;

    public MessageTemplatesViewModel(IAppSettingsStore store)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        Templates = new ObservableCollection<MessageTemplate>(
            store.Load().MessageTemplates.Templates);

        NewTemplateCommand    = new DesktopCommand(StartNew);
        EditTemplateCommand   = new DesktopCommand(StartEdit);
        SaveTemplateCommand   = new DesktopCommand(SaveTemplate);
        CancelEditCommand     = new DesktopCommand(CancelEdit);
        DeleteTemplateCommand = new DesktopCommand(DeleteTemplate);
        MoveUpCommand         = new DesktopCommand(MoveUp);
        MoveDownCommand       = new DesktopCommand(MoveDown);
        RestoreDefaultsCommand = new DesktopCommand(RestoreDefaults);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The full list of templates — bound to the editor list and the compose dropdown.</summary>
    public ObservableCollection<MessageTemplate> Templates { get; }

    public MessageTemplate? SelectedTemplate
    {
        get => selectedTemplate;
        set { if (selectedTemplate != value) { selectedTemplate = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSelection)); } }
    }

    public bool HasSelection => selectedTemplate is not null;

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

    public string EditRecipient
    {
        get => editRecipient;
        set { if (editRecipient != value) { editRecipient = value; OnPropertyChanged(); } }
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

    public DesktopCommand NewTemplateCommand     { get; }
    public DesktopCommand EditTemplateCommand    { get; }
    public DesktopCommand SaveTemplateCommand    { get; }
    public DesktopCommand CancelEditCommand      { get; }
    public DesktopCommand DeleteTemplateCommand  { get; }
    public DesktopCommand MoveUpCommand          { get; }
    public DesktopCommand MoveDownCommand        { get; }
    public DesktopCommand RestoreDefaultsCommand { get; }

    // ── Apply a template to compose fields ────────────────────────────

    /// <summary>
    /// Applies the selected template to the given compose fields.
    /// Called from MessageCenterViewModel when the operator picks a template.
    /// </summary>
    public (string Recipient, string Body) ApplyTemplate(MessageTemplate template)
        => (template.Recipient, template.Body);

    // ── Editor ────────────────────────────────────────────────────────

    private void StartNew()
    {
        editingId    = null;
        EditName     = string.Empty;
        EditRecipient = string.Empty;
        EditBody     = string.Empty;
        IsEditing    = true;
    }

    private void StartEdit()
    {
        if (selectedTemplate is null) return;
        editingId     = selectedTemplate.Id;
        EditName      = selectedTemplate.Name;
        EditRecipient = selectedTemplate.Recipient;
        EditBody      = selectedTemplate.Body;
        IsEditing     = true;
    }

    private void SaveTemplate()
    {
        if (string.IsNullOrWhiteSpace(EditName)) { StatusText = "Template name is required."; return; }
        if (string.IsNullOrWhiteSpace(EditBody))  { StatusText = "Message body is required.";  return; }

        if (editingId is not null)
        {
            // Update existing.
            var idx = Templates.IndexOf(Templates.First(t => t.Id == editingId));
            Templates[idx] = new MessageTemplate(editingId, EditName.Trim(),
                EditRecipient.Trim().ToUpperInvariant(), EditBody.Trim());
            StatusText = $"Template \"{EditName.Trim()}\" updated.";
        }
        else
        {
            // Add new.
            var t = MessageTemplate.Create(EditName.Trim(),
                EditRecipient.Trim().ToUpperInvariant(), EditBody.Trim());
            Templates.Add(t);
            SelectedTemplate = t;
            StatusText = $"Template \"{EditName.Trim()}\" added.";
        }

        Persist();
        IsEditing = false;
    }

    private void CancelEdit() => IsEditing = false;

    private void DeleteTemplate()
    {
        if (selectedTemplate is null) return;
        var name = selectedTemplate.Name;
        Templates.Remove(selectedTemplate);
        SelectedTemplate = Templates.FirstOrDefault();
        Persist();
        StatusText = $"Template \"{name}\" deleted.";
    }

    private void MoveUp()
    {
        if (selectedTemplate is null) return;
        var idx = Templates.IndexOf(selectedTemplate);
        if (idx <= 0) return;
        Templates.Move(idx, idx - 1);
        Persist();
    }

    private void MoveDown()
    {
        if (selectedTemplate is null) return;
        var idx = Templates.IndexOf(selectedTemplate);
        if (idx >= Templates.Count - 1) return;
        Templates.Move(idx, idx + 1);
        Persist();
    }

    private void RestoreDefaults()
    {
        Templates.Clear();
        foreach (var t in MessageTemplatesSettings.Default.Templates)
            Templates.Add(t);
        SelectedTemplate = Templates.FirstOrDefault();
        Persist();
        StatusText = "Default templates restored.";
    }

    private void Persist()
    {
        var settings = new MessageTemplatesSettings([.. Templates]);
        store.Update(s => s with { MessageTemplates = settings });
    }

    public static MessageTemplatesViewModel CreateDesignTime()
        => new(new InMemoryAppSettingsStore());

    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
