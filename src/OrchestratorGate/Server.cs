using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OrchestratorGate;

public static class AppInfo
{
    public const string Name = "orchestrator-gate";

    public static string Version { get; } =
        (typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "dev");

    public static string DefaultHome() =>
        Environment.GetEnvironmentVariable("ORCHESTRATOR_HOME") is { Length: > 0 } h
            ? h
            : System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".orchestrator");
}

public static class Server
{
    static readonly TimeSpan IdleExit = TimeSpan.FromHours(4);

    public static async Task<int> Run(string home, int port)
    {
        Directory.CreateDirectory(home);
        var logPath = System.IO.Path.Combine(home, "server.log");
        TrimLog(logPath);
        var logLock = new object();
        void Log(string line)
        {
            try { lock (logLock) File.AppendAllText(logPath, $"{DateTime.Now:s} {line}{Environment.NewLine}"); }
            catch { /* logging must never take the server down */ }
        }

        using var pageStream = typeof(Server).Assembly.GetManifestResourceStream("index.html")
            ?? throw new InvalidOperationException("page is not embedded");
        using var reader = new StreamReader(pageStream);
        var page = await reader.ReadToEndAsync();

        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { ContentRootPath = AppContext.BaseDirectory });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(o =>
        {
            o.Listen(IPAddress.Loopback, port);
            o.Limits.MaxRequestBodySize = 1 << 20;
        });
        var app = builder.Build();

        var pid = System.IO.Path.Combine(home, "server.pid");
        var api = new Api(new GateStore(home), new ConfigFile(home), port, AppInfo.Version, page,
            () => app.Lifetime.StopApplication(), Log);
        app.Run(api.Handle);

        try { File.WriteAllText(pid, Environment.ProcessId.ToString()); } catch { }
        using var idle = new Timer(_ =>
        {
            if (DateTime.UtcNow - api.LastRequest > IdleExit)
            {
                Log("idle, stopping");
                app.Lifetime.StopApplication();
            }
        }, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

        Log($"serving http://127.0.0.1:{port}/ version {AppInfo.Version}");
        try
        {
            await app.RunAsync();
        }
        finally
        {
            try { File.Delete(pid); } catch { }
            Log("stopped");
        }
        return 0;
    }

    static void TrimLog(string path)
    {
        try { if (File.Exists(path) && new FileInfo(path).Length > 1 << 20) File.Delete(path); } catch { }
    }
}
