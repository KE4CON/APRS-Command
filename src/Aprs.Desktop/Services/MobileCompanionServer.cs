using System.Net;
using System.Text;
using System.Text.Json;
using Aprs.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aprs.Desktop.Services;

/// <summary>
/// Lightweight embedded HTTP server that serves a mobile-friendly companion web app.
/// Operators open the URL (shown in the app) on their phone's browser — no app install needed.
///
/// Endpoints:
///   GET /          → single-page HTML app (map + stations + net + messages + stats)
///   GET /api/stations  → JSON array of visible stations with position data
///   GET /api/stats     → JSON packet statistics summary
///   GET /api/messages  → JSON recent messages (inbox + sent)
///   GET /api/net       → JSON net roster
///   GET /api/status    → JSON connection status and station info
/// </summary>
public sealed class MobileCompanionServer : IAsyncDisposable
{
    private readonly HttpListener listener = new();
    private readonly CancellationTokenSource cts = new();
    private readonly IServiceProvider services;
    private readonly Func<string> getCallsign;
    private readonly Func<bool> getConnected;
    private readonly Func<IReadOnlyList<(string Callsign, int Count)>> getTopStations;
    private readonly Func<int> getTotalPackets;
    private readonly Func<double> getPacketsPerHour;
    private readonly Func<IReadOnlyList<MessageSummary>> getMessages;
    private readonly Func<IReadOnlyList<NetRosterEntry>> getNetRoster;
    private Task? listenerTask;

    public int Port { get; private set; }
    public bool IsRunning { get; private set; }
    public string Url => $"http://localhost:{Port}/";

    public MobileCompanionServer(
        IServiceProvider services,
        Func<string> getCallsign,
        Func<bool> getConnected,
        Func<IReadOnlyList<(string, int)>> getTopStations,
        Func<int> getTotalPackets,
        Func<double> getPacketsPerHour,
        Func<IReadOnlyList<MessageSummary>> getMessages,
        Func<IReadOnlyList<NetRosterEntry>> getNetRoster)
    {
        this.services        = services;
        this.getCallsign     = getCallsign;
        this.getConnected    = getConnected;
        this.getTopStations  = getTopStations;
        this.getTotalPackets = getTotalPackets;
        this.getPacketsPerHour = getPacketsPerHour;
        this.getMessages     = getMessages;
        this.getNetRoster    = getNetRoster;
    }

    public void Start(int port = 0)
    {
        Port = port == 0 ? FindFreePort() : port;
        listener.Prefixes.Add($"http://localhost:{Port}/");
        listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
        listener.Start();
        IsRunning = true;
        listenerTask = Task.Run(ListenAsync, cts.Token);
    }

    private static int FindFreePort()
    {
        using var sock = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        sock.Start();
        var port = ((IPEndPoint)sock.LocalEndpoint).Port;
        sock.Stop();
        return port;
    }

    private async Task ListenAsync()
    {
        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                var ctx = await listener.GetContextAsync().WaitAsync(cts.Token);
                _ = Task.Run(() => HandleRequestAsync(ctx), cts.Token);
            }
            catch (OperationCanceledException) { break; }
            catch (HttpListenerException) { break; }
            catch { /* continue */ }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext ctx)
    {
        var req  = ctx.Request;
        var resp = ctx.Response;

        try
        {
            var path = req.Url?.AbsolutePath.TrimEnd('/') ?? "/";

            switch (path)
            {
                case "" or "/":
                    await ServeHtmlAsync(resp);
                    break;
                case "/api/stations":
                    await ServeJsonAsync(resp, BuildStationsPayload());
                    break;
                case "/api/stats":
                    await ServeJsonAsync(resp, BuildStatsPayload());
                    break;
                case "/api/messages":
                    await ServeJsonAsync(resp, BuildMessagesPayload());
                    break;
                case "/api/net":
                    await ServeJsonAsync(resp, BuildNetPayload());
                    break;
                case "/api/status":
                    await ServeJsonAsync(resp, BuildStatusPayload());
                    break;
                default:
                    resp.StatusCode = 404;
                    resp.Close();
                    break;
            }
        }
        catch { resp.StatusCode = 500; resp.Close(); }
    }

    // ── JSON payloads ─────────────────────────────────────────────────────────

    private object BuildStationsPayload()
    {
        var db = services.GetRequiredService<IStationDatabase>();
        return db.GetVisibleStations()
            .Where(s => s.Latitude.HasValue && s.Longitude.HasValue)
            .OrderByDescending(s => s.LastHeardUtc)
            .Take(200)
            .Select(s => new
            {
                callsign  = s.Callsign,
                lat       = s.Latitude,
                lng       = s.Longitude,
                comment   = s.Comment ?? string.Empty,
                symbol    = $"{s.SymbolTableIdentifier}{s.SymbolCode}",
                lastHeard = s.LastHeardUtc.ToUnixTimeSeconds(),
                speed     = s.SpeedKnots,
                course    = s.CourseDegrees,
            });
    }

    private object BuildStatsPayload()
    {
        var stats = services.GetRequiredService<PacketStatisticsService>();
        return new
        {
            total         = stats.TotalPackets,
            unique        = stats.UniqueStations,
            perHour       = Math.Round(stats.PacketsPerHour, 1),
            position      = stats.PositionPackets,
            message       = stats.MessagePackets,
            weather       = stats.WeatherPackets,
            topStations   = getTopStations().Take(10).Select(t => new { t.Callsign, t.Count }),
        };
    }

    private object BuildMessagesPayload()
    {
        return new { messages = getMessages().Take(50) };
    }

    private object BuildNetPayload()
    {
        return new { roster = getNetRoster() };
    }

    private object BuildStatusPayload()
    {
        return new
        {
            callsign  = getCallsign(),
            connected = getConnected(),
            serverTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        };
    }

    // ── HTTP helpers ──────────────────────────────────────────────────────────

    private static async Task ServeJsonAsync(HttpListenerResponse resp, object payload)
    {
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var bytes = Encoding.UTF8.GetBytes(json);
        resp.ContentType     = "application/json; charset=utf-8";
        resp.ContentLength64 = bytes.Length;
        resp.Headers.Add("Access-Control-Allow-Origin", "*");
        resp.Headers.Add("Cache-Control", "no-cache");
        await resp.OutputStream.WriteAsync(bytes);
        resp.Close();
    }

    private static async Task ServeHtmlAsync(HttpListenerResponse resp)
    {
        var html = BuildHtml();
        var bytes = Encoding.UTF8.GetBytes(html);
        resp.ContentType     = "text/html; charset=utf-8";
        resp.ContentLength64 = bytes.Length;
        await resp.OutputStream.WriteAsync(bytes);
        resp.Close();
    }

    // ── HTML page ─────────────────────────────────────────────────────────────

    private static string BuildHtml() => """
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1, maximum-scale=1">
<meta name="apple-mobile-web-app-capable" content="yes">
<meta name="apple-mobile-web-app-status-bar-style" content="black-translucent">
<title>APRS Command</title>
<link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css"/>
<script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"></script>
<style>
*{box-sizing:border-box;margin:0;padding:0}
body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif;background:#0f172a;color:#e2e8f0;height:100vh;display:flex;flex-direction:column}
#header{background:#1e293b;padding:10px 16px;display:flex;align-items:center;justify-content:space-between;border-bottom:1px solid #334155;flex-shrink:0}
#header h1{font-size:16px;font-weight:700;color:#60a5fa}
#status-dot{width:8px;height:8px;border-radius:50%;background:#ef4444;margin-right:6px;display:inline-block}
#status-dot.connected{background:#22c55e}
#status-text{font-size:12px;color:#94a3b8}
#tabs{display:flex;background:#1e293b;border-bottom:1px solid #334155;flex-shrink:0}
.tab{flex:1;padding:10px 4px;text-align:center;font-size:12px;font-weight:500;color:#64748b;cursor:pointer;border-bottom:2px solid transparent;transition:color .2s,border-color .2s}
.tab.active{color:#60a5fa;border-bottom-color:#60a5fa}
#content{flex:1;overflow:hidden;position:relative}
.panel{display:none;height:100%;overflow-y:auto}
.panel.active{display:block}
#map-panel{display:none;height:100%}
#map-panel.active{display:block}
#map{height:100%;width:100%}
.station-row{padding:10px 16px;border-bottom:1px solid #1e293b;display:flex;align-items:flex-start;gap:10px}
.station-row:active{background:#1e293b}
.callsign{font-weight:700;font-size:14px;color:#60a5fa;min-width:90px}
.comment{font-size:12px;color:#94a3b8;margin-top:2px;word-break:break-word}
.meta{font-size:11px;color:#475569;margin-top:2px}
.badge{display:inline-block;padding:2px 8px;border-radius:12px;font-size:11px;font-weight:600;margin-right:4px}
.badge-blue{background:#1d4ed8;color:#fff}
.badge-green{background:#166534;color:#fff}
.badge-red{background:#991b1b;color:#fff}
.stat-card{background:#1e293b;border:1px solid #334155;border-radius:10px;padding:14px 16px;margin:10px 12px}
.stat-num{font-size:30px;font-weight:700;color:#60a5fa}
.stat-label{font-size:12px;color:#64748b;margin-top:2px}
.stats-grid{display:grid;grid-template-columns:1fr 1fr;gap:0}
.msg-row{padding:10px 16px;border-bottom:1px solid #1e293b}
.msg-from{font-size:12px;font-weight:600;color:#60a5fa}
.msg-body{font-size:13px;color:#e2e8f0;margin-top:3px}
.msg-time{font-size:11px;color:#475569;margin-top:2px}
.net-row{padding:10px 16px;border-bottom:1px solid #1e293b;display:flex;align-items:center;gap:10px}
.empty{color:#475569;text-align:center;padding:40px 20px;font-size:14px}
.bar-wrap{height:4px;background:#1e293b;border-radius:2px;margin-top:4px}
.bar{height:4px;background:#60a5fa;border-radius:2px}
</style>
</head>
<body>
<div id="header">
  <h1>📡 APRS Command</h1>
  <div style="display:flex;align-items:center">
    <span id="status-dot"></span>
    <span id="status-text">Connecting...</span>
  </div>
</div>
<div id="tabs">
  <div class="tab active" onclick="showTab('map')">🗺 Map</div>
  <div class="tab" onclick="showTab('stations')">📡 Stations</div>
  <div class="tab" onclick="showTab('net')">📻 Net</div>
  <div class="tab" onclick="showTab('messages')">💬 Messages</div>
  <div class="tab" onclick="showTab('stats')">📊 Stats</div>
</div>
<div id="content">
  <div id="map-panel" class="panel active">
    <div id="map"></div>
  </div>
  <div id="stations-panel" class="panel">
    <div id="stations-list"></div>
  </div>
  <div id="net-panel" class="panel">
    <div id="net-list"></div>
  </div>
  <div id="messages-panel" class="panel">
    <div id="messages-list"></div>
  </div>
  <div id="stats-panel" class="panel">
    <div id="stats-content"></div>
  </div>
</div>
<script>
const map = L.map('map', {zoomControl:true}).setView([39.5, -98.35], 8);
L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',{
  attribution:'© OpenStreetMap contributors', maxZoom:18
}).addTo(map);
const markers = {};
let currentTab = 'map';
let stationsData = [];
let userLat = null, userLng = null;

function showTab(tab) {
  currentTab = tab;
  document.querySelectorAll('.tab').forEach((t,i) => {
    const tabs = ['map','stations','net','messages','stats'];
    t.classList.toggle('active', tabs[i] === tab);
  });
  document.querySelectorAll('.panel').forEach(p => p.classList.remove('active'));
  document.getElementById(tab + '-panel').classList.add('active');
  if (tab === 'map') setTimeout(() => map.invalidateSize(), 50);
}

function timeAgo(unix) {
  const d = Math.floor(Date.now()/1000 - unix);
  if (d < 60) return d + 's ago';
  if (d < 3600) return Math.floor(d/60) + 'm ago';
  if (d < 86400) return Math.floor(d/3600) + 'h ago';
  return Math.floor(d/86400) + 'd ago';
}

function aprsSymbolEmoji(sym) {
  if (!sym) return '📍';
  const code = sym[1] || sym[0];
  const map2 = {'-':'🏠','>':'🚗','R':'🚌','Y':'⛵','k':'🚚','f':'🚒',
    'u':'🚌','*':'❄️','_':'🌡️','r':'📡','I':'💻','D':'📡','S':'🛰️','O':'🎈'};
  return map2[code] || '📍';
}

async function fetchStatus() {
  try {
    const r = await fetch('/api/status');
    const d = await r.json();
    const dot = document.getElementById('status-dot');
    const txt = document.getElementById('status-text');
    dot.className = 'status-dot' + (d.connected ? ' connected' : '');
    txt.textContent = d.connected ? d.callsign + ' • Connected' : d.callsign + ' • Offline';
  } catch(e) {}
}

async function fetchStations() {
  try {
    const r = await fetch('/api/stations');
    const stations = await r.json();
    stationsData = stations;

    // Update map markers
    const seen = new Set();
    for (const s of stations) {
      seen.add(s.callsign);
      const icon = L.divIcon({
        className:'',
        html:`<div style="background:#1d4ed8;color:#fff;padding:2px 5px;border-radius:4px;font-size:10px;font-weight:700;white-space:nowrap;box-shadow:0 1px 3px rgba(0,0,0,.5)">${aprsSymbolEmoji(s.symbol)} ${s.callsign}</div>`,
        iconAnchor:[0,0]
      });
      if (markers[s.callsign]) {
        markers[s.callsign].setLatLng([s.lat, s.lng]).setIcon(icon);
      } else {
        markers[s.callsign] = L.marker([s.lat, s.lng], {icon})
          .bindPopup(`<b>${s.callsign}</b><br>${s.comment}<br><small>${timeAgo(s.lastHeard)}</small>`)
          .addTo(map);
      }
    }
    // Remove stale markers
    for (const k of Object.keys(markers)) {
      if (!seen.has(k)) { markers[k].remove(); delete markers[k]; }
    }

    // Update stations list
    if (currentTab === 'stations') renderStations(stations);
  } catch(e) {}
}

function renderStations(stations) {
  const el = document.getElementById('stations-list');
  if (!stations.length) { el.innerHTML = '<div class="empty">No stations heard yet.</div>'; return; }
  el.innerHTML = stations.slice(0, 100).map(s =>
    `<div class="station-row" onclick="focusStation(${s.lat},${s.lng})">
      <div>${aprsSymbolEmoji(s.symbol)}</div>
      <div style="flex:1">
        <div class="callsign">${s.callsign}</div>
        ${s.comment ? `<div class="comment">${s.comment}</div>` : ''}
        <div class="meta">${timeAgo(s.lastHeard)}${s.speed ? ' · ' + s.speed + ' kts' : ''}</div>
      </div>
    </div>`
  ).join('');
}

function focusStation(lat, lng) {
  showTab('map');
  map.setView([lat, lng], 13);
}

async function fetchNet() {
  try {
    const r = await fetch('/api/net');
    const d = await r.json();
    const el = document.getElementById('net-list');
    if (!d.roster || !d.roster.length) {
      el.innerHTML = '<div class="empty">No stations checked in.</div>'; return;
    }
    el.innerHTML = d.roster.map(s =>
      `<div class="net-row">
        <div style="font-size:18px">${s.statusEmoji || '📍'}</div>
        <div>
          <div class="callsign">${s.callsign}</div>
          <div class="meta">${s.checkInStatus || ''} · ${s.resourceStatus || ''}</div>
        </div>
      </div>`
    ).join('');
  } catch(e) {}
}

async function fetchMessages() {
  try {
    const r = await fetch('/api/messages');
    const d = await r.json();
    const el = document.getElementById('messages-list');
    if (!d.messages || !d.messages.length) {
      el.innerHTML = '<div class="empty">No messages this session.</div>'; return;
    }
    el.innerHTML = d.messages.map(m =>
      `<div class="msg-row">
        <div class="msg-from">${m.direction === 'in' ? '← ' : '→ '}${m.other}</div>
        <div class="msg-body">${m.body}</div>
        <div class="msg-time">${m.time}</div>
      </div>`
    ).join('');
  } catch(e) {}
}

async function fetchStats() {
  if (currentTab !== 'stats') return;
  try {
    const r = await fetch('/api/stats');
    const d = await r.json();
    const maxCount = d.topStations && d.topStations.length ? d.topStations[0].count : 1;
    document.getElementById('stats-content').innerHTML = `
      <div class="stats-grid">
        <div class="stat-card"><div class="stat-num">${d.total.toLocaleString()}</div><div class="stat-label">Total Packets</div></div>
        <div class="stat-card"><div class="stat-num">${d.unique}</div><div class="stat-label">Unique Stations</div></div>
        <div class="stat-card"><div class="stat-num">${d.perHour}</div><div class="stat-label">Packets / Hour</div></div>
        <div class="stat-card"><div class="stat-num">${d.position}</div><div class="stat-label">Position</div></div>
      </div>
      <div style="padding:12px 16px;font-size:13px;font-weight:600;color:#94a3b8;margin-top:4px">Top Stations</div>
      ${(d.topStations || []).map(s =>
        `<div class="station-row">
          <div class="callsign">${s.callsign}</div>
          <div style="flex:1">
            <div class="bar-wrap"><div class="bar" style="width:${Math.round(s.count/maxCount*100)}%"></div></div>
          </div>
          <div style="font-size:12px;color:#60a5fa;font-weight:600;min-width:40px;text-align:right">${s.count}</div>
        </div>`
      ).join('')}
    `;
  } catch(e) {}
}

async function refresh() {
  await Promise.all([fetchStatus(), fetchStations()]);
  if (currentTab === 'net')      await fetchNet();
  if (currentTab === 'messages') await fetchMessages();
  if (currentTab === 'stats')    await fetchStats();
}

// Initial load then poll every 5 seconds
refresh();
setInterval(refresh, 5000);

// Also refresh when switching tabs
const origShowTab = showTab;
window.showTab = function(tab) {
  origShowTab(tab);
  refresh();
};
</script>
</body>
</html>
""";

    public async ValueTask DisposeAsync()
    {
        cts.Cancel();
        try { listener.Stop(); } catch { }
        if (listenerTask is not null)
            try { await listenerTask.ConfigureAwait(false); } catch { }
        cts.Dispose();
        IsRunning = false;
    }
}

// ── Data transfer types used by the web server ─────────────────────────────────

public sealed record MessageSummary(
    string Direction,   // "in" or "out"
    string Other,       // the other callsign
    string Body,
    string Time);

public sealed record NetRosterEntry(
    string Callsign,
    string CheckInStatus,
    string ResourceStatus,
    string StatusEmoji);
