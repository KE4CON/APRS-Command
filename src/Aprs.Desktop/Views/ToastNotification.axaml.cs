using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace Aprs.Desktop.Views;

public sealed partial class ToastNotification : Window
{
    private const int DisplaySeconds = 5;
    private DispatcherTimer? timer;

    public event EventHandler? Clicked;

    public ToastNotification() : this("Notification", string.Empty) { }

    public ToastNotification(string title, string body)
    {
        InitializeComponent();
        TitleText.Text = title;
        BodyText.Text = body;
        timer = new DispatcherTimer(
            TimeSpan.FromSeconds(DisplaySeconds),
            DispatcherPriority.Background,
            (_, _) => Close());
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        var screen = Screens.Primary;
        if (screen is not null)
        {
            var bounds = screen.WorkingArea;
            var scaling = screen.Scaling;
            Position = new PixelPoint(
                (int)(bounds.Right / scaling - Width - 16),
                (int)(bounds.Bottom / scaling - Height - 16));
        }
        timer?.Start();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Clicked?.Invoke(this, EventArgs.Empty);
        Close();
    }

    private void CloseButton_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        timer?.Stop();
        timer = null;
    }
}
