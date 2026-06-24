using Aprs.Desktop.ViewModels;
using Aprs.Transport;
using Xunit;

namespace Aprs.Tests;

public sealed class DirewolfProfileServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DefaultProfile_UsesSafeDirewolfDefaults()
    {
        var profile = DirewolfProfile.CreateDefault(Now);

        Assert.Equal("Local Direwolf", profile.ProfileName);
        Assert.Equal("127.0.0.1", profile.Host);
        Assert.Equal(8001, profile.KissPort);
        Assert.False(profile.Enabled);
        Assert.True(profile.ReceiveEnabled);
        Assert.False(profile.TransmitEnabled);
        Assert.True(profile.AutoReconnect);
        Assert.Equal("Direwolf", profile.SourceName);
    }

    [Fact]
    public void DefaultProfile_HasTransmitDisabled()
    {
        var service = new DirewolfProfileService(Now);

        var validation = service.ValidateProfile(service.GetCurrentProfile(), rfTransmitSafetyEnabled: true);

        Assert.False(service.GetCurrentProfile().TransmitEnabled);
        Assert.False(validation.IsSafeForTransmit);
        Assert.Contains(validation.Warnings, warning => warning.Contains("transmit is disabled", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidProfile_ConvertsToTcpKissConfiguration()
    {
        var service = new DirewolfProfileService(Now);
        var profile = service.GetCurrentProfile() with
        {
            Enabled = true,
            Host = "127.0.0.1",
            KissPort = 8001,
            ReceiveEnabled = true,
            TransmitEnabled = false,
            SourceName = "Direwolf"
        };

        var configuration = service.ToTcpKissConfiguration(profile);

        Assert.Equal("127.0.0.1", configuration.Host);
        Assert.Equal(8001, configuration.Port);
        Assert.True(configuration.Enabled);
        Assert.True(configuration.ReceiveEnabled);
        Assert.False(configuration.TransmitEnabled);
        Assert.True(configuration.ReconnectEnabled);
        Assert.Equal("Direwolf", configuration.SourceName);
    }

    [Fact]
    public void InvalidHost_FailsValidation()
    {
        var service = new DirewolfProfileService(Now);
        var profile = service.GetCurrentProfile() with { Host = " " };

        var validation = service.ValidateProfile(profile);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Contains("host", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void InvalidPort_FailsValidation(int port)
    {
        var service = new DirewolfProfileService(Now);
        var profile = service.GetCurrentProfile() with { KissPort = port };

        var validation = service.ValidateProfile(profile);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Contains("port", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ReceiveSafeProfile_WorksWithoutTransmit()
    {
        var service = new DirewolfProfileService(Now);
        var profile = service.GetCurrentProfile() with { Enabled = true, ReceiveEnabled = true, TransmitEnabled = false };

        var validation = service.ValidateProfile(profile);

        Assert.True(validation.IsValid);
        Assert.True(validation.IsSafeForReceive);
        Assert.False(validation.IsSafeForTransmit);
    }

    [Fact]
    public void TransmitSafeProfile_RequiresExplicitTransmitAndRfSafety()
    {
        var service = new DirewolfProfileService(Now);
        var profile = service.GetCurrentProfile() with { Enabled = true, TransmitEnabled = true };

        var blocked = service.ValidateProfile(profile, rfTransmitSafetyEnabled: false);
        var allowed = service.ValidateProfile(profile, rfTransmitSafetyEnabled: true);

        Assert.False(blocked.IsSafeForTransmit);
        Assert.Contains(blocked.Errors, error => error.Contains("RF transmit safety", StringComparison.OrdinalIgnoreCase));
        Assert.True(allowed.IsSafeForTransmit);
    }

    [Fact]
    public void Reset_RestoresDefaults()
    {
        var service = new DirewolfProfileService(Now);
        service.UpdateProfile(service.GetCurrentProfile() with { Host = "direwolf.local", KissPort = 9000, Enabled = true }, Now.AddMinutes(1));

        var reset = service.ResetToDefaults(Now.AddMinutes(2));

        Assert.Equal("127.0.0.1", reset.Host);
        Assert.Equal(8001, reset.KissPort);
        Assert.False(reset.Enabled);
        Assert.Equal(Now.AddMinutes(2), reset.UpdatedUtc);
    }

    [Fact]
    public async Task ConnectionTest_ReportsSuccessFromProbe()
    {
        var service = new DirewolfProfileService(Now);
        var profile = service.GetCurrentProfile() with { Enabled = true };
        var tester = new DirewolfConnectionTestService(new FakeConnectionProbe());

        var result = await tester.TestAsync(profile, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.FailureReason);
    }

    [Fact]
    public async Task ConnectionTest_ReportsFailureFromProbe()
    {
        var service = new DirewolfProfileService(Now);
        var profile = service.GetCurrentProfile() with { Enabled = true };
        var tester = new DirewolfConnectionTestService(new FakeConnectionProbe(new InvalidOperationException("connection refused")));

        var result = await tester.TestAsync(profile, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("connection refused", result.FailureReason);
    }

    [Fact]
    public void SetupNotes_AreAvailable()
    {
        Assert.Contains(DirewolfSetupNotes.Notes, note => note.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(DirewolfSetupNotes.Notes, note => note.Contains("RF transmit remains disabled", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DirewolfViewModel_ExposesProfileAndNotes()
    {
        var viewModel = DirewolfProfileViewModel.CreateDesignTime();

        Assert.Equal("127.0.0.1", viewModel.Host);
        Assert.Equal(8001, viewModel.KissPort);
        Assert.Contains(viewModel.SetupNotes, note => note.Contains("Direwolf", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class FakeConnectionProbe : IDirewolfConnectionProbe
    {
        private readonly Exception? exception;

        public FakeConnectionProbe(Exception? exception = null)
        {
            this.exception = exception;
        }

        public Task ProbeAsync(string host, int port, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (exception is not null)
            {
                throw exception;
            }

            return Task.CompletedTask;
        }
    }
}
