namespace Aprs.Desktop.Configuration;

public sealed record NetScript(
    Guid   Id,
    string Name,
    string Body);

public sealed record NetScriptSettings(
    IReadOnlyList<NetScript> Scripts,
    Guid? LastSelectedId)
{
    public static NetScriptSettings Default { get; } = new(
        Scripts:
        [
            new NetScript(
                Id:   Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890"),
                Name: "ARES Weekly Net",
                Body: """
                      This is {{callsign}}, net control for the {{net_name}}.
                      Today is {{date}} and the time is {{time}} local.

                      This net is open to all licensed amateur radio operators.
                      Please check in with your callsign, name, and location.

                      All stations stand by — I will call each station in turn.
                      If you have traffic, please indicate when you check in.

                      {{callsign}}, net control, QRV.
                      """.Trim()
            ),
            new NetScript(
                Id:   Guid.Parse("b2c3d4e5-f6a7-8901-bcde-f12345678901"),
                Name: "Event Check-In",
                Body: """
                      Good morning / afternoon / evening. This is {{callsign}} serving as
                      net control for today's event communications.

                      All stations please check in now. State your callsign,
                      your position, and confirm you are ready to copy traffic.

                      {{callsign}}, net control, standing by.
                      """.Trim()
            )
        ],
        LastSelectedId: null);
}
