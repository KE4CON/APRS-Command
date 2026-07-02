using Aprs.Transport;
using Xunit;

namespace Aprs.Tests;

public sealed class Ax25AprsFrameEncoderTests
{
    [Fact]
    public void Encode_SimplePacket_ReturnsNonEmptyBytes()
    {
        var result = Ax25AprsFrameEncoder.Encode("KE4CON-1>APRS,WIDE1-1,WIDE2-1:!3956.00N/08255.00W-Test");
        Assert.NotNull(result);
        Assert.True(result.Length > 0);
    }

    [Fact]
    public void Encode_DestinationIsFirstSevenBytes()
    {
        var result = Ax25AprsFrameEncoder.Encode("KE4CON>APRS:>Status");
        Assert.NotNull(result);
        // Destination "APRS  " — each char left-shifted by 1
        Assert.Equal((byte)('A' << 1), result[0]);
        Assert.Equal((byte)('P' << 1), result[1]);
        Assert.Equal((byte)('R' << 1), result[2]);
        Assert.Equal((byte)('S' << 1), result[3]);
        Assert.Equal((byte)(' ' << 1), result[4]);
        Assert.Equal((byte)(' ' << 1), result[5]);
    }

    [Fact]
    public void Encode_SourceIsSecondSevenBytes()
    {
        var result = Ax25AprsFrameEncoder.Encode("KE4CON>APRS:>Status");
        Assert.NotNull(result);
        Assert.Equal((byte)('K' << 1), result[7]);
        Assert.Equal((byte)('E' << 1), result[8]);
        Assert.Equal((byte)('4' << 1), result[9]);
        Assert.Equal((byte)('C' << 1), result[10]);
        Assert.Equal((byte)('O' << 1), result[11]);
        Assert.Equal((byte)('N' << 1), result[12]);
    }

    [Fact]
    public void Encode_LastAddressField_HasEndBitSet()
    {
        // No digis → source is last address (byte 13)
        var result = Ax25AprsFrameEncoder.Encode("KE4CON>APRS:>Status");
        Assert.NotNull(result);
        Assert.Equal(1, result[13] & 0x01);
    }

    [Fact]
    public void Encode_WithSsid1_EncodesCorrectly()
    {
        var result = Ax25AprsFrameEncoder.Encode("KE4CON-1>APRS:>Status");
        Assert.NotNull(result);
        // SSID byte: 0x60 | (1<<1) | 1 = 0x63
        Assert.Equal(0x63, result[13] & 0xFF);
    }

    [Fact]
    public void Encode_WithDigipeater_DigiIsThirdAddressBlock()
    {
        var result = Ax25AprsFrameEncoder.Encode("KE4CON>APRS,WIDE1-1:>Status");
        Assert.NotNull(result);
        // Digi "WIDE1 " starts at offset 14
        Assert.Equal((byte)('W' << 1), result[14]);
        Assert.Equal((byte)('I' << 1), result[15]);
        Assert.Equal((byte)('D' << 1), result[16]);
        Assert.Equal((byte)('E' << 1), result[17]);
        Assert.Equal((byte)('1' << 1), result[18]);
        Assert.Equal((byte)(' ' << 1), result[19]);
        // Digi is last → end bit set; SSID=1 → 0x63
        Assert.Equal(0x63, result[20] & 0xFF);
        // Source must NOT have end bit set
        Assert.Equal(0, result[13] & 0x01);
    }

    [Fact]
    public void Encode_ControlAndPid_CorrectAfterTwoAddressFields()
    {
        var result = Ax25AprsFrameEncoder.Encode("KE4CON>APRS:>Status");
        Assert.NotNull(result);
        // 14 address bytes → control at 14, PID at 15
        Assert.Equal(0x03, result[14]);
        Assert.Equal(0xF0, result[15]);
    }

    [Fact]
    public void Encode_InformationField_IsAsciiAfterControlPid()
    {
        var result = Ax25AprsFrameEncoder.Encode("KE4CON>APRS:>Test Status");
        Assert.NotNull(result);
        var info = System.Text.Encoding.ASCII.GetString(result, 16, result.Length - 16);
        Assert.Equal(">Test Status", info);
    }

    [Fact]
    public void Encode_NullOrEmpty_ReturnsNull()
    {
        Assert.Null(Ax25AprsFrameEncoder.Encode(""));
        Assert.Null(Ax25AprsFrameEncoder.Encode(null));
    }

    [Fact]
    public void Encode_MissingColon_ReturnsNull()
    {
        Assert.Null(Ax25AprsFrameEncoder.Encode("KE4CON>APRS no colon here"));
    }

    [Fact]
    public void Encode_MissingArrow_ReturnsNull()
    {
        Assert.Null(Ax25AprsFrameEncoder.Encode("KE4CON:>Status"));
    }

    [Fact]
    public void Encode_InformationDecodesCorrectly()
    {
        var packet = "KE4CON-5>APRS:!3956.00N/08255.00W-Test";
        var ax25   = Ax25AprsFrameEncoder.Encode(packet);
        Assert.NotNull(ax25);
        // The information field starts after 14 address bytes + 2 control/pid bytes
        var info = System.Text.Encoding.ASCII.GetString(ax25, 16, ax25.Length - 16);
        Assert.Equal("!3956.00N/08255.00W-Test", info);
    }
}
