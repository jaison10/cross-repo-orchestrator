using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;

namespace OrchestratorGate;

public enum ServerState { Down, Running, Stranger }

/// <summary>Starts, probes and stops the one persistent page server, found by port and /health.</summary>
public sealed class Lifecycle(int port, string home)
{
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(2) };

    public string BaseUrl => $"http://127.0.0.1:{port}/";

    public async Task<(ServerState State, string? Version)> Probe()
    {
        // Free to bind means nobody is listening. (Connecting to a closed port takes ~2s to be refused on Windows.)
        try
        {
            var l = new TcpListener(IPAddress.Loopback, port);
            l.Start();
            l.Stop();
            return (ServerState.Down, null);
        }
        catch (SocketException) { /* something holds the port: ours or a stranger */ }

        try
        {
            var text = await Http.GetStringAsync(BaseUrl + "health");
            var h = JsonNode.Parse(text) as JsonObject;
            return Json.Str(h?["app"]) == AppInfo.Name ? (ServerState.Running, Json.Str(h?["version"])) : (ServerState.Stranger, null);
        }
        catch { return (ServerState.Stranger, null); }
    }

    string PortTaken() =>
        $"port {port} is in use by another program; set \"port\" in {new ConfigFile(home).Path}";

    async Task<bool> WaitFor(ServerState want)
    {
        for (var until = DateTime.UtcNow.AddSeconds(10); DateTime.UtcNow < until; await Task.Delay(100))
            if ((await Probe()).State == want) return true;
        return false;
    }

    public async Task<string> Start()
    {
        var (st, v) = await Probe();
        if (st == ServerState.Stranger) throw new GateException(500, PortTaken());
        if (st == ServerState.Running && v == AppInfo.Version) return BaseUrl;
        if (st == ServerState.Running) await Stop();

        Directory.CreateDirectory(home);
        var exe = Environment.ProcessPath ?? throw new GateException(500, "cannot find own executable");
        Proc.StartDetached(exe, "serve");
        if (!await WaitFor(ServerState.Running))
            throw new GateException(500, $"the server did not start; see {Path.Combine(home, "server.log")}");
        return BaseUrl;
    }

    public async Task Stop()
    {
        var (st, _) = await Probe();
        if (st == ServerState.Down) return;
        if (st == ServerState.Stranger) throw new GateException(500, PortTaken());
        try
        {
            using var content = new StringContent("{}", Encoding.UTF8, "application/json");
            (await Http.PostAsync(BaseUrl + "shutdown", content)).Dispose();
        }
        catch { /* it may close the connection while shutting down */ }
        if (!await WaitFor(ServerState.Down)) throw new GateException(500, $"the server on port {port} did not stop");
    }

    public async Task<string> Status()
    {
        var (st, v) = await Probe();
        return st switch
        {
            ServerState.Running => $"running {v} at {BaseUrl}",
            ServerState.Stranger => PortTaken(),
            _ => "not running",
        };
    }

    public static void OpenBrowser(string url)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            else
                Process.Start(new ProcessStartInfo(OperatingSystem.IsMacOS() ? "open" : "xdg-open") { ArgumentList = { url } });
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"could not open a browser ({e.Message}); open {url} yourself");
        }
    }
}
