using Aprs.Transport;
using Xunit;

namespace Aprs.Tests;

public sealed class AprsIsServerManagerTests
{
    private static readonly DateTimeOffset TestNow = new(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_CreatesDefaultServerList()
    {
        var manager = new AprsIsServerManager(TestNow);

        var servers = manager.GetAllServers();

        Assert.Equal(6, servers.Count);
        Assert.Contains(servers, server => server.HostName == "rotate.aprs2.net" && server.Port == 14580);
        Assert.Contains(servers, server => server.HostName == "noam.aprs2.net" && server.Port == 14580);
        Assert.Contains(servers, server => server.HostName == "euro.aprs2.net" && server.Port == 14580);
        Assert.Contains(servers, server => server.HostName == "soam.aprs2.net" && server.Port == 14580);
        Assert.Contains(servers, server => server.HostName == "aunz.aprs2.net" && server.Port == 14580);
        Assert.Contains(servers, server => server.HostName == "asia.aprs2.net" && server.Port == 14580);
    }

    [Fact]
    public void GetDefaultServer_ReturnsRotateServer()
    {
        var manager = new AprsIsServerManager(TestNow);

        var defaultServer = manager.GetDefaultServer();

        Assert.Equal("rotate.aprs2.net", defaultServer.HostName);
        Assert.True(defaultServer.IsDefault);
        Assert.True(defaultServer.IsEnabled);
    }

    [Fact]
    public void SetDefaultServer_ChangesDefaultServer()
    {
        var manager = new AprsIsServerManager(TestNow);

        var selected = manager.SetDefaultServer("North America APRS2", TestNow.AddMinutes(1));

        Assert.Equal("noam.aprs2.net", selected.HostName);
        Assert.True(selected.IsDefault);
        Assert.Equal("noam.aprs2.net", manager.GetDefaultServer().HostName);
        Assert.Single(manager.GetAllServers(), server => server.IsDefault);
    }

    [Fact]
    public void AddCustomServer_AddsEnabledCustomServer()
    {
        var manager = new AprsIsServerManager(TestNow);

        var server = manager.AddCustomServer(
            "Local Filter",
            "aprs.example.test",
            14580,
            "Local test server",
            "Local",
            "For offline tests",
            TestNow.AddMinutes(2));

        Assert.True(server.IsCustom);
        Assert.True(server.IsEnabled);
        Assert.False(server.IsDefault);
        Assert.Contains(manager.GetAllServers(), item => item.Name == "Local Filter");
    }

    [Fact]
    public void UpdateServer_ChangesServerFields()
    {
        var manager = new AprsIsServerManager(TestNow);

        manager.AddCustomServer("Local Filter", "aprs.example.test", 14580, "Old", null, null, TestNow);
        var updated = manager.UpdateServer(
            "Local Filter",
            "aprs2.example.test",
            10152,
            "Updated description",
            isEnabled: true,
            region: "Test",
            notes: "Updated notes",
            updatedAtUtc: TestNow.AddMinutes(5));

        Assert.Equal("aprs2.example.test", updated.HostName);
        Assert.Equal(10152, updated.Port);
        Assert.Equal("Updated description", updated.Description);
        Assert.Equal("Test", updated.Region);
        Assert.Equal("Updated notes", updated.Notes);
        Assert.Equal(TestNow.AddMinutes(5), updated.UpdatedAtUtc);
    }

    [Fact]
    public void SetServerEnabled_DisabledServerIsExcludedFromEnabledList()
    {
        var manager = new AprsIsServerManager(TestNow);

        var disabled = manager.SetServerEnabled("Europe APRS2", isEnabled: false, TestNow.AddMinutes(1));

        Assert.False(disabled.IsEnabled);
        Assert.DoesNotContain(manager.GetEnabledServers(), server => server.Name == "Europe APRS2");
    }

    [Fact]
    public void AddCustomServer_RejectsDuplicateHostAndPort()
    {
        var manager = new AprsIsServerManager(TestNow);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            manager.AddCustomServer("Duplicate Rotate", "ROTATE.APRS2.NET", 14580, "Duplicate", null, null, TestNow));

        Assert.Contains("same host and port", exception.Message);
    }

    [Fact]
    public void AddCustomServer_RejectsInvalidPort()
    {
        var manager = new AprsIsServerManager(TestNow);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            manager.AddCustomServer("Invalid", "aprs.example.test", 0, "Invalid", null, null, TestNow));
    }

    [Fact]
    public void ResetToDefaults_RestoresBuiltInDefaultList()
    {
        var manager = new AprsIsServerManager(TestNow);
        manager.AddCustomServer("Local Filter", "aprs.example.test", 14580, "Local", null, null, TestNow);
        manager.SetDefaultServer("North America APRS2", TestNow);

        manager.ResetToDefaults(TestNow.AddHours(1));

        Assert.Equal(6, manager.GetAllServers().Count);
        Assert.DoesNotContain(manager.GetAllServers(), server => server.Name == "Local Filter");
        Assert.Equal("rotate.aprs2.net", manager.GetDefaultServer().HostName);
    }

    [Fact]
    public void ConfigurationWithServer_UsesSelectedServer()
    {
        var manager = new AprsIsServerManager(TestNow);
        var selected = manager.SetDefaultServer("Asia APRS2", TestNow.AddMinutes(1));

        var configuration = AprsIsClientConfiguration.Default.WithServer(selected);

        Assert.Equal("asia.aprs2.net", configuration.ServerHost);
        Assert.Equal(14580, configuration.ServerPort);
        Assert.True(configuration.ReceiveOnly);
    }
}
