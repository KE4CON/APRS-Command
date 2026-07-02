using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Aprs.Desktop.Views;

public sealed partial class AudioConfigView : UserControl
{
    public AudioConfigView() { InitializeComponent(); }

    private void OnTestVoiceClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ViewModels.AudioConfigViewModel vm) return;

        // Temporarily override IsEnabled to allow testing even when voice is off
        var svc = new Services.VoiceAlertService
        {
            IsEnabled             = true,
            SpeakIncomingMessages = true,
            PreferredVoiceName    = vm.SelectedVoice?.VoiceName
        };
        svc.Speak("APRS Command voice test. This is how alerts will sound.", Services.VoiceAlertType.IncomingMessage);
        // Fire and forget — dispose after a generous delay
        _ = Task.Delay(TimeSpan.FromSeconds(10)).ContinueWith(_ => svc.Dispose());
    }
}
