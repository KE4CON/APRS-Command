namespace Aprs.Transport;

public static class DirewolfSetupNotes
{
    public static IReadOnlyList<string> Notes { get; } =
    [
        "Direwolf usually listens for KISS TCP on 127.0.0.1 port 8001.",
        "Direwolf must be running before the APRS app can connect.",
        "RF transmit remains disabled in this app until explicitly enabled.",
        "Verify audio, PTT, and radio setup in Direwolf separately."
    ];
}
