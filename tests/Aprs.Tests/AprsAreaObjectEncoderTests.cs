using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

/// <summary>
/// APRS area-object encoding (WB4APR PROTOCOL.TXT): the 7-byte "Tyy/Cxx" descriptor with √-scaled
/// offsets (offset_degrees = value² / 100) and the non-sequential shape Type codes.
/// </summary>
public sealed class AprsAreaObjectEncoderTests
{
    [Fact]
    public void Encode_ProducesTyyCxxDescriptor_WithSqrtScaledOffsets()
    {
        // Box (Type 4), colour 1. Height 1° (111.32 km) → yy = 10·√1 = 10. Width 4° (445.28 km) → xx = 20.
        var result = AprsAreaObjectEncoder.Encode(
            AprsAreaObjectShape.OpenBlueRectangle, colorCode: 1, widthKm: 445.28, heightKm: 111.32);

        Assert.Equal("410/120", result); // T=4, yy=10, '/', C=1, xx=20
    }

    [Theory]
    [InlineData(AprsAreaObjectShape.OpenBlackCircle, '0')]
    [InlineData(AprsAreaObjectShape.OpenRedLine, '1')]
    [InlineData(AprsAreaObjectShape.OpenBlueTriangle, '3')]   // triangle = 3 (not the enum's 2)
    [InlineData(AprsAreaObjectShape.OpenBlueRectangle, '4')]  // box = 4 (not the enum's 3)
    [InlineData(AprsAreaObjectShape.FilledBlackCircle, '5')]
    [InlineData(AprsAreaObjectShape.FilledBlueTriangle, '8')] // filled triangle = 8
    [InlineData(AprsAreaObjectShape.FilledBlueRectangle, '9')] // filled box = 9
    public void Encode_UsesSpecShapeTypeCode(AprsAreaObjectShape shape, char expectedTypeChar)
    {
        var result = AprsAreaObjectEncoder.Encode(shape, colorCode: 0, widthKm: 1, heightKm: 1);

        Assert.Equal(expectedTypeChar, result[0]);
    }

    [Fact]
    public void Encode_ZeroSize_ProducesZeroOffsets()
    {
        var result = AprsAreaObjectEncoder.Encode(
            AprsAreaObjectShape.OpenBlackCircle, colorCode: 0, widthKm: 0, heightKm: 0);

        Assert.Equal("000/000", result);
    }

    [Fact]
    public void Encode_AppendsDescriptionAfterDescriptor()
    {
        var result = AprsAreaObjectEncoder.Encode(
            AprsAreaObjectShape.OpenBlackCircle, colorCode: 0, widthKm: 0, heightKm: 0, description: "Search area");

        Assert.Equal("000/000Search area", result);
        Assert.StartsWith("000/000", result); // the 7-byte descriptor stays first
    }
}
