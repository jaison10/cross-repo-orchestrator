using System.Text.Json.Nodes;

namespace OrchestratorGate;

public static class Program
{
    const string Usage = """
        usage:
          orchestrator-gate open <gate-file> [--no-browser]      show a gate in the browser, print its URL
          orchestrator-gate wait <gate-file> [--round N] [--timeout MIN]
                                                                 block until the page submits that round, print the result
          orchestrator-gate setup [--no-browser]                 first-run form for ~/.orchestrator/config.json, blocks until saved
          orchestrator-gate config                               print the effective config
          orchestrator-gate set <key> <value>                    change one config value (validated), e.g. set worktree-root C:/wt
          orchestrator-gate start | stop | status | serve | version
        exit codes: 0 ok, 1 error, 2 wait timed out
        """;

    public static async Task<int> Main(string[] args)
    {
        try
        {
            return await Run(args);
        }
        catch (GateException e)
        {
            Console.Error.WriteLine($"orchestrator-gate: {e.Message}");
            return 1;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"orchestrator-gate: {e.Message}");
            return 1;
        }
    }

    static async Task<int> Run(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine(Usage);
            return 0;
        }

        var home = AppInfo.DefaultHome();
        var cfg = new ConfigFile(home);
        var life = new Lifecycle(cfg.Port, home);
        var opts = Options.Parse(args[1..]);

        switch (args[0])
        {
            case "version":
                Console.WriteLine(AppInfo.Version);
                return 0;
            case "config":
                Console.WriteLine(cfg.Effective().ToJsonString());
                return 0;
            case "set":
            {
                // orchestrator-gate set <key> <value>: same validation as the setup page. An empty value clears worktree-root.
                if (opts.Count < 2) throw new GateException(1, "usage: orchestrator-gate set <key> <value>");
                var key = opts.At(0);
                var raw = opts.At(1);
                JsonNode? value = key switch
                {
                    "port" or "idleMinutes" => int.TryParse(raw, out var n) ? JsonValue.Create(n) : throw new GateException(1, $"{key} needs a number"),
                    "browser" => bool.TryParse(raw, out var b) ? JsonValue.Create(b) : throw new GateException(1, "browser needs true or false"),
                    _ => JsonValue.Create(raw),
                };
                Console.WriteLine(cfg.Save(new JsonObject { [key] = value }).ToJsonString());
                return 0;
            }
            case "serve":
                return await Server.Run(home, cfg.Port);
            case "start":
                Console.WriteLine(await life.Start());
                return 0;
            case "stop":
                await life.Stop();
                return 0;
            case "status":
                Console.WriteLine(await life.Status());
                return 0;
            case "open":
            {
                var r = GateRef.FromFile(home, opts.Positional("gate file"));
                var store = new GateStore(home);
                store.Read(r); // fails early when the gate file is missing or not valid JSON
                var url = await life.Start() + "#" + r.Fragment;
                Console.WriteLine(url);
                MaybeOpen(url, cfg, opts);
                return 0;
            }
            case "setup":
            {
                var url = await life.Start() + "#setup";
                Console.WriteLine(url);
                MaybeOpen(url, cfg, opts);
                return await WaitForSetup(cfg);
            }
            case "wait":
            {
                var r = GateRef.FromFile(home, opts.Positional("gate file"));
                var store = new GateStore(home);
                var round = opts.Int("--round") ?? 1;
                var minutes = opts.Int("--timeout") ?? Json.Int(cfg.Effective()["idleMinutes"]) ?? 30;
                var result = await Waiter.WaitAsync(store, r, round, TimeSpan.FromMinutes(minutes), CancellationToken.None);
                Console.WriteLine(result.ToJsonString());
                return Json.Str(result["status"]) == "submitted" ? 0 : 2;
            }
            default:
                Console.Error.WriteLine($"unknown command '{args[0]}'\n{Usage}");
                return 1;
        }
    }

    static void MaybeOpen(string url, ConfigFile cfg, Options opts)
    {
        if (!opts.Flag("--no-browser") && Json.Bool(cfg.Effective()["browser"]) != false)
            Lifecycle.OpenBrowser(url);
    }

    /// <summary>Waits until config.json has a username, i.e. the page's first-run form was saved.</summary>
    static async Task<int> WaitForSetup(ConfigFile cfg)
    {
        var idle = TimeSpan.FromMinutes(Json.Int(cfg.Effective()["idleMinutes"]) ?? 30);
        var start = DateTime.UtcNow;
        while (DateTime.UtcNow - start < idle)
        {
            if (Json.Str(cfg.Effective()["username"]) is { Length: > 0 })
            {
                Console.WriteLine(cfg.Effective().ToJsonString());
                return 0;
            }
            await Task.Delay(500);
        }
        Console.WriteLine(new JsonObject { ["status"] = "timeout" }.ToJsonString());
        return 2;
    }
}

public sealed class Options
{
    readonly List<string> _positional = [];
    readonly Dictionary<string, string?> _named = [];

    public static Options Parse(string[] args)
    {
        var o = new Options();
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--")) { o._positional.Add(args[i]); continue; }
            var hasValue = i + 1 < args.Length && !args[i + 1].StartsWith("--");
            o._named[args[i]] = hasValue ? args[++i] : null;
        }
        return o;
    }

    public bool Flag(string name) => _named.ContainsKey(name);

    public int? Int(string name) =>
        _named.TryGetValue(name, out var v) && int.TryParse(v, out var n) && n > 0
            ? n
            : _named.ContainsKey(name) ? throw new GateException(1, $"{name} needs a positive number") : null;

    public int Count => _positional.Count;
    public string At(int i) => _positional[i];

    public string Positional(string what) =>
        _positional.Count > 0 ? _positional[0] : throw new GateException(1, $"a {what} is required");
}
