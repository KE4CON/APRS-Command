using Avalonia.Controls;
namespace Aprs.Desktop.Views;
public sealed partial class FrequencyReferenceWindow : Window
{
    public FrequencyReferenceWindow()
    {
        InitializeComponent();
        CloseButton.Click += (_, _) => Close();
    }
}
