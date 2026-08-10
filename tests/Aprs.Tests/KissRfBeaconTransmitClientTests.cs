using Aprs.Desktop.Runtime;
using Xunit;

namespace Aprs.Tests;

/// <summary>
/// Transmit-safety: the RF chokepoint must never key up with a placeholder callsign (FCC §97.119).
/// Unlike APRS-IS (blocked by the missing passcode), RF has no passcode backstop, so the identity gate
/// lives here.
/// </summary>
public sealed class KissRfBeaconTransmitClientTests
{
    [Theory]
    [InlineData("N0CALL>APRS,WIDE1-1:!3903.50N/07201.75W-Test")]
    [InlineData("N0CALL-9>APRS,WIDE1-1:!3903.50N/07201.75W-Test")]
    [InlineData("NOCALL>APRS:!3903.50N/07201.75W-Test")]
    [InlineData("MYCALL>APRS:!3903.50N/07201.75W-Test")]
    public async Task SendBeaconAsync_WithPlaceholderCallsign_IsBlockedAndNotTransmitted(string rawPacket)
    {
        var client = new KissRfBeaconTransmitClient(); // no ports wired
        var result = await client.SendBeaconAsync(rawPacket, CancellationToken.None);

        Assert.False(result.Transmitted);
        Assert.False(result.TransmitAttempted);
        Assert.True(result.Blocked);
        Assert.Contains("callsign", result.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendBeaconAsync_WithValidCallsign_PassesIdentityGate()
    {
        // With a real callsign and no ports wired, it clears the identity gate and reaches the
        // "no ports connected" outcome — proving the gate blocks ONLY placeholders, not valid stations.
        var client = new KissRfBeaconTransmitClient();
        var result = await client.SendBeaconAsync("KE4CON-9>APRS,WIDE1-1:!3903.50N/07201.75W-Test", CancellationToken.None);

        Assert.False(result.Transmitted);
        Assert.DoesNotContain("callsign", result.Message ?? "", System.StringComparison.OrdinalIgnoreCase);
    }
}
