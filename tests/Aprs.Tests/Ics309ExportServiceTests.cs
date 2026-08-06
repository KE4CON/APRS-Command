using System;
using System.Collections.Generic;
using Aprs.Desktop.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class Ics309ExportServiceTests
{
    private static readonly DateTimeOffset From = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2026, 8, 5, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Generates_Header_IncidentName_And_EmptyState()
    {
        var text = Ics309ExportService.GenerateIcs309(
            "Hurricane Prep Exercise", "James Rospopo", "KE4CON", "Net Control",
            From, To, new List<AprsMessageSnapshot>());

        Assert.Contains("ICS 309 — COMMUNICATIONS LOG", text);
        Assert.Contains("Hurricane Prep Exercise", text);
        Assert.Contains("KE4CON", text);
        Assert.Contains("No messages recorded", text);
        Assert.Contains("Total messages logged: 0", text);
    }

    [Fact]
    public void Lists_Messages_In_Period_Chronologically()
    {
        var messages = new List<AprsMessageSnapshot>
        {
            new(new DateTimeOffset(2026, 8, 5, 13, 0, 0, TimeSpan.Zero), "KE4CON", "W1AW", "Second", true),
            new(new DateTimeOffset(2026, 8, 5, 12, 30, 0, TimeSpan.Zero), "W1AW", "KE4CON", "First", false),
        };

        var text = Ics309ExportService.GenerateIcs309(
            "Field Day", "Op", "KE4CON", "NCS", From, To, messages);

        Assert.Contains("Total messages logged: 2", text);
        Assert.True(
            text.IndexOf("First", StringComparison.Ordinal) < text.IndexOf("Second", StringComparison.Ordinal),
            "Messages should be listed in chronological order.");
    }

    [Fact]
    public void Excludes_Messages_Outside_The_Operational_Period()
    {
        var messages = new List<AprsMessageSnapshot>
        {
            new(new DateTimeOffset(2026, 8, 5, 11, 0, 0, TimeSpan.Zero), "X", "Y", "TooEarly", false),
            new(new DateTimeOffset(2026, 8, 5, 14, 0, 0, TimeSpan.Zero), "X", "Y", "InWindow", false),
            new(new DateTimeOffset(2026, 8, 5, 19, 0, 0, TimeSpan.Zero), "X", "Y", "TooLate", false),
        };

        var text = Ics309ExportService.GenerateIcs309(
            "Drill", "Op", "KE4CON", "NCS", From, To, messages);

        Assert.Contains("Total messages logged: 1", text);
        Assert.Contains("InWindow", text);
        Assert.DoesNotContain("TooEarly", text);
        Assert.DoesNotContain("TooLate", text);
    }
}
