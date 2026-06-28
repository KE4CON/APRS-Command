using Avalonia.Controls;
using Aprs.Desktop.ViewModels;
using Aprs.Desktop.Configuration;

namespace Aprs.Desktop.Views;

public sealed partial class MessageCenterView : UserControl
{
    public MessageCenterView()
    {
        InitializeComponent();
    }

    private void TemplateCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox combo) return;
        if (combo.SelectedItem is not MessageTemplate template) return;
        if (DataContext is not MessageCenterViewModel vm) return;

        vm.ApplyTemplate(template);

        // Reset selection so the same template can be picked again.
        combo.SelectedIndex = -1;
    }
}
