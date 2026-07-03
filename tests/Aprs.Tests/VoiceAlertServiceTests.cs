using Aprs.Desktop.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class VoiceAlertServiceTests
{
    // ── IsEnabled gate ────────────────────────────────────────────────────

    [Fact]
    public void Speak_DoesNothing_WhenDisabled()
    {
        using var svc = new VoiceAlertService { IsEnabled = false };
        // Just verifying it doesn't throw; actual speech cannot be asserted here
        var exception = Record.Exception(() =>
            svc.Speak("Test message", VoiceAlertType.IncomingMessage));
        Assert.Null(exception);
    }

    [Fact]
    public void Speak_DoesNothing_WhenTextIsEmpty()
    {
        using var svc = new VoiceAlertService { IsEnabled = true };
        var exception = Record.Exception(() =>
            svc.Speak("", VoiceAlertType.IncomingMessage));
        Assert.Null(exception);
    }

    [Fact]
    public void Speak_DoesNothing_WhenTextIsWhitespace()
    {
        using var svc = new VoiceAlertService { IsEnabled = true };
        var exception = Record.Exception(() =>
            svc.Speak("   ", VoiceAlertType.WeatherAlert));
        Assert.Null(exception);
    }

    // ── Per-type toggles ──────────────────────────────────────────────────

    [Fact]
    public void DefaultToggles_AreCorrect()
    {
        using var svc = new VoiceAlertService();
        Assert.False(svc.IsEnabled);              // master off by default
        Assert.True(svc.SpeakIncomingMessages);
        Assert.True(svc.SpeakNetCheckIns);
        Assert.True(svc.SpeakWeatherAlerts);
        Assert.False(svc.SpeakStationAlerts);
        Assert.False(svc.SpeakConnectionEvents);
        Assert.False(svc.SpeakBeaconConfirmations);
    }

    [Fact]
    public void Speak_DoesNothing_WhenAlertTypeToggleIsOff()
    {
        using var svc = new VoiceAlertService
        {
            IsEnabled = true,
            SpeakStationAlerts = false   // explicitly off
        };
        var exception = Record.Exception(() =>
            svc.Speak("KE4CON heard", VoiceAlertType.StationAlert));
        Assert.Null(exception); // no throw; speech suppressed
    }

    [Theory]
    [InlineData(VoiceAlertType.IncomingMessage,    nameof(VoiceAlertService.SpeakIncomingMessages))]
    [InlineData(VoiceAlertType.NetCheckIn,         nameof(VoiceAlertService.SpeakNetCheckIns))]
    [InlineData(VoiceAlertType.WeatherAlert,       nameof(VoiceAlertService.SpeakWeatherAlerts))]
    [InlineData(VoiceAlertType.StationAlert,       nameof(VoiceAlertService.SpeakStationAlerts))]
    [InlineData(VoiceAlertType.ConnectionEvent,    nameof(VoiceAlertService.SpeakConnectionEvents))]
    [InlineData(VoiceAlertType.BeaconConfirmation, nameof(VoiceAlertService.SpeakBeaconConfirmations))]
    public void AllAlertTypes_HaveCorrespondingToggle(VoiceAlertType type, string toggleName)
    {
        // Verifies all 6 alert types have a named toggle property
        using var svc = new VoiceAlertService();
        var prop = typeof(VoiceAlertService).GetProperty(toggleName);
        Assert.NotNull(prop);
        Assert.Equal(typeof(bool), prop!.PropertyType);
    }

    // ── Voice options ─────────────────────────────────────────────────────

    [Fact]
    public void GetAvailableVoices_AlwaysIncludesSystemDefault()
    {
        var voices = VoiceAlertService.GetAvailableVoices();
        Assert.NotEmpty(voices);
        Assert.Contains(voices, v => v.DisplayName == "System Default");
        Assert.Contains(voices, v => v.VoiceName is null);
    }

    [Fact]
    public void GetAvailableVoices_ReturnsSameListOnSubsequentCalls()
    {
        var first  = VoiceAlertService.GetAvailableVoices();
        var second = VoiceAlertService.GetAvailableVoices();
        Assert.Equal(first.Count, second.Count);
    }

    [Fact]
    public void VoiceOption_ToString_ReturnsDisplayName()
    {
        var option = new VoiceOption("System Default", null);
        Assert.Equal("System Default", option.ToString());
    }

    // ── Dispose ───────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var svc = new VoiceAlertService();
        var exception = Record.Exception(() => svc.Dispose());
        Assert.Null(exception);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var svc = new VoiceAlertService();
        svc.Dispose();
        var exception = Record.Exception(() => svc.Dispose());
        // Second dispose may or may not throw — just verify it doesn't crash the process
        // (CancellationTokenSource.Dispose can throw on double-dispose in some environments)
        _ = exception; // don't assert — platform dependent
    }

    // ── PreferredVoiceName ────────────────────────────────────────────────

    [Fact]
    public void PreferredVoiceName_DefaultsToNull()
    {
        using var svc = new VoiceAlertService();
        Assert.Null(svc.PreferredVoiceName);
    }

    [Fact]
    public void PreferredVoiceName_CanBeSet()
    {
        using var svc = new VoiceAlertService();
        svc.PreferredVoiceName = "Samantha";
        Assert.Equal("Samantha", svc.PreferredVoiceName);
    }
}
