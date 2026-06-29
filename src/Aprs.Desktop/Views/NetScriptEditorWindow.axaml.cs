using Avalonia.Controls;
namespace Aprs.Desktop.Views;
public sealed partial class NetScriptEditorWindow : Window
{
    public NetScriptEditorWindow() { InitializeComponent(); CloseButton.Click += (_, _) => Close(); }
}
