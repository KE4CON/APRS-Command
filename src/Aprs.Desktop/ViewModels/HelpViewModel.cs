using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Aprs.Desktop.ViewModels;

public sealed class HelpViewModel : INotifyPropertyChanged
{
    private readonly IReadOnlyList<HelpTopic> allTopics;
    private HelpTopic? selectedTopic;
    private string searchText = string.Empty;

    public HelpViewModel(HelpDocumentService documentService)
    {
        DocumentationLocation = documentService.DocsFolderPath;
        allTopics = documentService.LoadTopics();
        FilteredTopics = new ObservableCollection<HelpTopic>();
        ApplyFilter();
        SelectedTopic = FilteredTopics.FirstOrDefault();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<HelpTopic> FilteredTopics { get; }

    public string DocumentationLocation { get; }

    public string SearchText
    {
        get => searchText;
        set
        {
            if (searchText == value)
            {
                return;
            }

            searchText = value;
            ApplyFilter();
            OnPropertyChanged();
        }
    }

    public HelpTopic? SelectedTopic
    {
        get => selectedTopic;
        set
        {
            if (Equals(selectedTopic, value))
            {
                return;
            }

            selectedTopic = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedTitle));
            OnPropertyChanged(nameof(SelectedContent));
            OnPropertyChanged(nameof(SelectedAvailability));
        }
    }

    public string SelectedTitle => selectedTopic?.Title ?? "Help";

    public string SelectedContent => selectedTopic?.Content ?? "Choose a help topic.";

    public string SelectedAvailability => selectedTopic?.IsAvailable == false ? "Missing from this build" : "Available offline";

    public int TopicCount => FilteredTopics.Count;

    public IReadOnlyList<HelpTopic> AllTopics => allTopics;

    public static HelpViewModel CreateDefault()
    {
        return new HelpViewModel(new HelpDocumentService());
    }

    public static HelpViewModel CreateDesignTime()
    {
        return CreateDefault();
    }

    private void ApplyFilter()
    {
        var query = searchText.Trim();
        var filtered = string.IsNullOrWhiteSpace(query)
            ? allTopics
            : allTopics.Where(topic =>
                topic.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                || topic.Content.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToArray();

        FilteredTopics.Clear();
        foreach (var topic in filtered)
        {
            FilteredTopics.Add(topic);
        }

        if (SelectedTopic is null || !FilteredTopics.Contains(SelectedTopic))
        {
            SelectedTopic = FilteredTopics.FirstOrDefault();
        }

        OnPropertyChanged(nameof(TopicCount));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
