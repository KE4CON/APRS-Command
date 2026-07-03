using System;
using Aprs.Desktop.ViewModels;
using Xunit;

namespace Aprs.Tests;

public sealed class ManualWeatherEntryViewModelTests
{
    private static ManualWeatherEntryViewModel ValidVm() => new()
    {
        SourceName              = "Manual Weather",
        TimestampUtc            = DateTimeOffset.UtcNow,
        TemperatureFahrenheit   = 72.0,
        HumidityPercent         = 55,
        WindDirectionDegrees    = 270,
        WindSpeedMph            = 8.0,
        WindGustMph             = 12.0,
        BarometricPressureMillibars = 1013.2
    };

    // ── Happy path ────────────────────────────────────────────────────────

    [Fact]
    public void Validate_ReturnsTrue_WhenAllRequiredFieldsPresent()
    {
        var vm = ValidVm();
        Assert.True(vm.Validate());
        Assert.Empty(vm.ValidationErrors);
        Assert.Equal("Valid", vm.ValidationStatus);
    }

    [Fact]
    public void Validate_AllowsNullOptionalFields()
    {
        var vm = ValidVm();
        vm.RainLastHourInches       = null;
        vm.RainLast24HoursInches    = null;
        vm.RainSinceMidnightInches  = null;
        Assert.True(vm.Validate());
    }

    // ── Required field validation ─────────────────────────────────────────

    [Fact]
    public void Validate_RequiresSourceName()
    {
        var vm = ValidVm();
        vm.SourceName = "";
        Assert.False(vm.Validate());
        Assert.Contains(vm.ValidationErrors, e => e.Contains("Source name"));
    }

    [Fact]
    public void Validate_RequiresTimestamp()
    {
        var vm = ValidVm();
        vm.TimestampUtc = null;
        Assert.False(vm.Validate());
        Assert.Contains(vm.ValidationErrors, e => e.Contains("Timestamp"));
    }

    [Fact]
    public void Validate_RequiresTemperature()
    {
        var vm = ValidVm();
        vm.TemperatureFahrenheit = null;
        Assert.False(vm.Validate());
        Assert.Contains(vm.ValidationErrors, e => e.Contains("Temperature"));
    }

    [Fact]
    public void Validate_RequiresHumidity()
    {
        var vm = ValidVm();
        vm.HumidityPercent = null;
        Assert.False(vm.Validate());
        Assert.Contains(vm.ValidationErrors, e => e.Contains("Humidity"));
    }

    [Fact]
    public void Validate_RequiresWindDirection()
    {
        var vm = ValidVm();
        vm.WindDirectionDegrees = null;
        Assert.False(vm.Validate());
        Assert.Contains(vm.ValidationErrors, e => e.Contains("Wind direction"));
    }

    [Fact]
    public void Validate_RequiresWindSpeed()
    {
        var vm = ValidVm();
        vm.WindSpeedMph = null;
        Assert.False(vm.Validate());
        Assert.Contains(vm.ValidationErrors, e => e.Contains("Wind speed"));
    }

    [Fact]
    public void Validate_RequiresWindGust()
    {
        var vm = ValidVm();
        vm.WindGustMph = null;
        Assert.False(vm.Validate());
        Assert.Contains(vm.ValidationErrors, e => e.Contains("Wind gust"));
    }

    [Fact]
    public void Validate_RequiresBarometricPressure()
    {
        var vm = ValidVm();
        vm.BarometricPressureMillibars = null;
        Assert.False(vm.Validate());
        Assert.Contains(vm.ValidationErrors, e => e.Contains("Barometric pressure"));
    }

    // ── Multiple errors at once ───────────────────────────────────────────

    [Fact]
    public void Validate_ReportsAllMissingFields_WhenMultipleMissing()
    {
        var vm = new ManualWeatherEntryViewModel
        {
            TemperatureFahrenheit = null,
            HumidityPercent = null,
            WindDirectionDegrees = null
        };
        vm.Validate();
        Assert.True(vm.ValidationErrors.Count >= 3);
    }

    // ── ValidationStatus ─────────────────────────────────────────────────

    [Fact]
    public void ValidationStatus_DefaultsToNotValidated()
    {
        var vm = new ManualWeatherEntryViewModel();
        Assert.Equal("Not validated", vm.ValidationStatus);
    }

    [Fact]
    public void ValidationStatus_SetsToValid_OnSuccess()
    {
        var vm = ValidVm();
        vm.Validate();
        Assert.Equal("Valid", vm.ValidationStatus);
    }

    [Fact]
    public void ValidationStatus_ContainsErrors_OnFailure()
    {
        var vm = ValidVm();
        vm.TemperatureFahrenheit = null;
        vm.Validate();
        Assert.NotEqual("Valid", vm.ValidationStatus);
        Assert.NotEqual("Not validated", vm.ValidationStatus);
        Assert.Contains("Temperature", vm.ValidationStatus);
    }

    // ── Default state ─────────────────────────────────────────────────────

    [Fact]
    public void DefaultSourceName_IsManualWeather()
    {
        var vm = new ManualWeatherEntryViewModel();
        Assert.Equal("Manual Weather", vm.SourceName);
    }

    [Fact]
    public void DefaultOptionalFields_AreNull()
    {
        var vm = new ManualWeatherEntryViewModel();
        Assert.Null(vm.RainLastHourInches);
        Assert.Null(vm.RainLast24HoursInches);
        Assert.Null(vm.RainSinceMidnightInches);
        Assert.Null(vm.TimestampUtc);
        Assert.Null(vm.TemperatureFahrenheit);
    }
}
