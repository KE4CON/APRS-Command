using Aprs.Core;
using Xunit;

namespace Aprs.Tests;

/// <summary>
/// Phase-5 conformance completion: spec-cited vector coverage for the packet types that had
/// scattered unit tests but were missing from the dedicated APRS-spec conformance suite —
/// telemetry (T#), item (')'), query (?), and station capabilities (&lt;). Together with
/// <see cref="AprsSpec101ConformanceTests"/> (positions, compressed, objects, weather, messages,
/// status, headers) and the type-specific suites (MIC-E, third-party, DAO, NMEA, user-defined),
/// every APRS data type now has spec-vector conformance coverage.
/// </summary>
public sealed class AprsSpecConformanceCompletionTests
{
    private static readonly DateTimeOffset TestTime = new(2000, 8, 29, 0, 0, 0, TimeSpan.Zero);

    private static AprsPacket Parse(string raw) => new AprsParser().Parse(raw, TestTime);

    // ── §13 — Telemetry ──────────────────────────────────────────────────

    /// <summary>
    /// §13 Telemetry Report (spec p.68): "T#005,199,000,255,073,123,01101001" — sequence 5,
    /// five analog channels, and eight digital bits.
    /// </summary>
    [Fact]
    public void Spec_TelemetryReport_SequenceAnalogAndDigital_Decoded()
    {
        var telemetry = Assert.IsType<TelemetryAprsPacket>(Parse("N0CALL>APRS:T#005,199,000,255,073,123,01101001"));

        Assert.Equal(5, telemetry.SequenceNumber);
        Assert.Equal(new[] { 199, 0, 255, 73, 123 }, telemetry.AnalogValues);
        Assert.Equal(8, telemetry.DigitalValues.Count);
        Assert.Equal(new[] { false, true, true, false, true, false, false, true }, telemetry.DigitalValues);
    }

    /// <summary>
    /// §13 Telemetry parameter-name metadata (spec p.69): "PARM." names the analog/digital channels.
    /// The parser recognizes the bare info-field form directly as telemetry metadata.
    /// </summary>
    [Fact]
    public void Spec_TelemetryParameterNames_BareForm_DecodedAsMetadata()
    {
        var metadata = Assert.IsType<TelemetryMetadataAprsPacket>(
            Parse("N0CALL>APRS:PARM.Battery,Temp,Load,Alt,Count,B1,B2,B3"));

        Assert.Equal("PARM", metadata.MetadataKind);
        Assert.Contains("Battery", metadata.Values);
    }

    /// <summary>
    /// KNOWN LIMITATION (residual, tracked in APRS_SPEC_CONFORMANCE_PLAN.md): the spec-standard
    /// telemetry metadata form is message-embedded — sent as a message addressed to the station,
    /// e.g. ":N0CALL   :PARM.…". That form is currently classified as a plain message rather than
    /// extracted as TelemetryMetadataAprsPacket. This test pins the current behavior so a future
    /// change is noticed.
    /// </summary>
    [Fact]
    public void MessageEmbeddedTelemetryMetadata_CurrentlyParsesAsMessage()
    {
        Assert.IsType<MessageAprsPacket>(
            Parse("N0CALL>APRS::N0CALL   :PARM.Battery,Temp,Load,Alt,Count,B1,B2,B3"));
    }

    // ── §11 — Item Reports ───────────────────────────────────────────────

    /// <summary>
    /// §11 Item Report (spec p.59): ")AIDV#2!4903.50N/07201.75WA" — a live item named AIDV#2 at
    /// 49°03.50'N 072°01.75'W. The '!' marks it live.
    /// </summary>
    [Fact]
    public void Spec_ItemReport_LiveItem_NameAndPositionDecoded()
    {
        var item = Assert.IsType<ItemAprsPacket>(Parse("N0CALL>APRS:)AIDV#2!4903.50N/07201.75WA"));

        Assert.Equal("AIDV#2", item.ItemName);
        Assert.Equal(49.058333, item.Latitude!.Value, 4);
        Assert.Equal(-72.029167, item.Longitude!.Value, 4);
        Assert.True(item.IsValid);
    }

    /// <summary>
    /// §11 Killed Item: ")AIDV#2_4903.50N/07201.75WA" — the '_' marks the item killed. (Same
    /// live/kill convention as objects; see the primer §7.3 correction.)
    /// </summary>
    [Fact]
    public void Spec_ItemReport_KilledItem_NameStillDecoded()
    {
        var item = Assert.IsType<ItemAprsPacket>(Parse("N0CALL>APRS:)AIDV#2_4903.50N/07201.75WA"));

        Assert.Equal("AIDV#2", item.ItemName);
        Assert.Equal(49.058333, item.Latitude!.Value, 4);
    }

    // ── §15 — General Queries ────────────────────────────────────────────

    /// <summary>
    /// §15 General Query (spec p.77): "?APRS?" is a general query directed at all stations.
    /// </summary>
    [Fact]
    public void Spec_GeneralQuery_APRS_Decoded()
    {
        var query = Assert.IsType<QueryAprsPacket>(Parse("N0CALL>APRS:?APRS?"));

        Assert.Equal(AprsQueryType.General, query.QueryType);
    }

    // ── §17 — Station Capabilities ───────────────────────────────────────

    /// <summary>
    /// §17 Station Capabilities (spec p.83): "&lt;IGATE,MSG_CNT=1,LOC_CNT=25" reports the station's
    /// capabilities. The '&lt;' DTI captures the capability text.
    /// </summary>
    [Fact]
    public void Spec_StationCapabilities_Decoded()
    {
        var capability = Assert.IsType<CapabilityAprsPacket>(Parse("N0CALL>APRS:<IGATE,MSG_CNT=1,LOC_CNT=25"));

        Assert.Contains("IGATE", capability.CapabilityText);
    }
}
