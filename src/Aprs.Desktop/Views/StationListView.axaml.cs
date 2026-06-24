using Avalonia.Controls;
using Aprs.Desktop.ViewModels;

namespace Aprs.Desktop.Views;

public sealed partial class StationListView : UserControl
{
    public StationListView()
    {
        InitializeComponent();
    }

    private void SearchTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (DataContext is StationListViewModel viewModel && sender is TextBox textBox)
        {
            viewModel.SearchText = textBox.Text ?? string.Empty;
        }
    }

    private void ActiveOnlyCheckBox_Changed(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is StationListViewModel viewModel && sender is CheckBox checkBox)
        {
            viewModel.ShowActiveOnly = checkBox.IsChecked == true;
        }
    }

    private void ShowExpiredCheckBox_Changed(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is StationListViewModel viewModel && sender is CheckBox checkBox)
        {
            viewModel.ShowExpiredStations = checkBox.IsChecked == true;
        }
    }

    private void PacketSourceComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is StationListViewModel viewModel && sender is ComboBox comboBox && comboBox.SelectedItem is string selected)
        {
            viewModel.SelectedPacketSourceFilter = selected;
        }
    }

    private void StationRowsListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is StationListViewModel viewModel && sender is ListBox listBox && listBox.SelectedItem is StationListRowViewModel row)
        {
            viewModel.SelectRow(row);
        }
    }

    private void SortByCallsign_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SortBy(StationListSortField.Callsign);
    }

    private void SortByDisplayName_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SortBy(StationListSortField.DisplayName);
    }

    private void SortByLastHeard_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SortBy(StationListSortField.LastHeard);
    }

    private void SortByAgeState_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SortBy(StationListSortField.AgeState);
    }

    private void SortByPacketSource_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SortBy(StationListSortField.PacketSource);
    }

    private void SortBy(StationListSortField field)
    {
        if (DataContext is StationListViewModel viewModel)
        {
            viewModel.SortBy(field);
        }
    }
}
