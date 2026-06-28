using Avalonia.Controls;
using Avalonia.Input;

namespace Aprs.Desktop.Views;

public sealed partial class TacticalLabelDialog : Window
{
    public string? ResultLabel { get; private set; }
    public bool Cleared { get; private set; }

    public TacticalLabelDialog() { InitializeComponent(); }

    public TacticalLabelDialog(string callsign, string? currentLabel)
    {
        InitializeComponent();
        CallsignLabel.Text = $"Station: {callsign}";
        LabelInput.Text    = currentLabel ?? string.Empty;

        OkButton.Click     += (_, _) => { ResultLabel = LabelInput.Text?.Trim(); Cleared = false; Close(); };
        CancelButton.Click += (_, _) => Close();
        ClearButton.Click  += (_, _) => { Cleared = true; ResultLabel = null; Close(); };

        LabelInput.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Return) { ResultLabel = LabelInput.Text?.Trim(); Close(); }
            if (e.Key == Key.Escape) Close();
        };

        Opened += (_, _) =>
        {
            LabelInput.Focus();
            LabelInput.SelectAll();
        };
    }
}
