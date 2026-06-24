using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class LocalStationProfileServiceTests
{
    private static readonly DateTimeOffset TestNow = new(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DefaultProfile_IsSafeAndTransmitDisabled()
    {
        var service = new LocalStationProfileService(TestNow);

        var profile = service.GetCurrentProfile();
        var validation = service.ValidateProfile(profile);

        Assert.True(validation.IsValid);
        Assert.False(profile.TransmitEnabled);
        Assert.False(profile.AprsIsTransmitEnabled);
        Assert.False(profile.RfTransmitEnabled);
        Assert.False(validation.IsSafeToTransmit);
        Assert.Contains(validation.Warnings, warning => warning.Contains("Transmit is disabled", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateProfile_ValidCallsignPassesValidation()
    {
        var service = new LocalStationProfileService(TestNow);
        var profile = service.GetCurrentProfile() with { Callsign = "KD8ABC" };

        var validation = service.ValidateProfile(profile);

        Assert.True(validation.IsValid);
        Assert.Empty(validation.Errors);
    }

    [Fact]
    public void ValidateProfile_InvalidCallsignFailsValidation()
    {
        var service = new LocalStationProfileService(TestNow);
        var profile = service.GetCurrentProfile() with { Callsign = "TOOLONG7" };

        var validation = service.ValidateProfile(profile);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Contains("Callsign", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateProfile_InvalidSsidFailsValidation()
    {
        var service = new LocalStationProfileService(TestNow);
        var profile = service.GetCurrentProfile() with { Callsign = "KD8ABC", Ssid = 16 };

        var validation = service.ValidateProfile(profile);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Contains("SSID", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateProfile_InvalidLatitudeLongitudeFailsValidation()
    {
        var service = new LocalStationProfileService(TestNow);
        var profile = service.GetCurrentProfile() with
        {
            Callsign = "KD8ABC",
            FixedLatitude = 91,
            FixedLongitude = -181
        };

        var validation = service.ValidateProfile(profile);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Contains("Latitude", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(validation.Errors, error => error.Contains("Longitude", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetFullStationIdentifier_FormatsSsidCorrectly()
    {
        var service = new LocalStationProfileService(TestNow);
        var profile = service.GetCurrentProfile() with { Callsign = "kd8abc", Ssid = 7 };

        Assert.Equal("KD8ABC-7", service.GetFullStationIdentifier(profile));
    }

    [Fact]
    public void ValidateProfile_TransmitEnabledWithoutValidCallsignFailsValidation()
    {
        var service = new LocalStationProfileService(TestNow);
        var profile = service.GetCurrentProfile() with
        {
            Callsign = string.Empty,
            TransmitEnabled = true
        };

        var validation = service.ValidateProfile(profile);

        Assert.False(validation.IsValid);
        Assert.False(validation.IsSafeToTransmit);
        Assert.Contains(validation.Errors, error => error.Contains("valid callsign", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateProfile_AprsIsTransmitAndRfTransmitAreSeparateFlags()
    {
        var service = new LocalStationProfileService(TestNow);
        var profile = service.GetCurrentProfile() with
        {
            Callsign = "KD8ABC",
            TransmitEnabled = true,
            AprsIsTransmitEnabled = true,
            RfTransmitEnabled = false
        };

        var validation = service.ValidateProfile(profile);

        Assert.True(profile.AprsIsTransmitEnabled);
        Assert.False(profile.RfTransmitEnabled);
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Contains("APRS-IS transmit is not configured", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(validation.Errors, error => error.Contains("RF transmit is not configured", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateProfile_RfTransmitRequiresBeaconPathAndTransport()
    {
        var service = new LocalStationProfileService(TestNow);
        var profile = service.GetCurrentProfile() with
        {
            Callsign = "KD8ABC",
            TransmitEnabled = true,
            RfTransmitEnabled = true,
            BeaconPath = string.Empty
        };

        var validation = service.ValidateProfile(profile);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Contains("Beacon path", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(validation.Errors, error => error.Contains("RF transmit is not configured", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void UpdateProfile_ChangesUpdatedTimestamp()
    {
        var service = new LocalStationProfileService(TestNow);
        var updatedAt = TestNow.AddMinutes(5);
        var profile = service.GetCurrentProfile() with { Callsign = "kd8abc" };

        var updated = service.UpdateProfile(profile, updatedAt);

        Assert.Equal("KD8ABC", updated.Callsign);
        Assert.Equal(TestNow, updated.CreatedAtUtc);
        Assert.Equal(updatedAt, updated.UpdatedAtUtc);
    }

    [Fact]
    public void ResetToDefaults_RestoresSafeDefaults()
    {
        var service = new LocalStationProfileService(TestNow);
        service.UpdateProfile(service.GetCurrentProfile() with { Callsign = "KD8ABC", TransmitEnabled = true }, TestNow.AddMinutes(1));

        var reset = service.ResetToDefaults(TestNow.AddHours(1));

        Assert.Equal(string.Empty, reset.Callsign);
        Assert.False(reset.TransmitEnabled);
        Assert.False(reset.AprsIsTransmitEnabled);
        Assert.False(reset.RfTransmitEnabled);
        Assert.Equal(TestNow.AddHours(1), reset.CreatedAtUtc);
        Assert.Equal(TestNow.AddHours(1), reset.UpdatedAtUtc);
    }
}
