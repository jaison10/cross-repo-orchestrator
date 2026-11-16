using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace OrchestratorGate;

/// <summary>
/// ~/.orchestrator/config.json. Optional: it is never needed to exist, defaults live here.
/// Keys: username, worktree-root (used by branch-prep), ui, port, browser, idleMinutes.
/// </summary>
public sealed class ConfigFile(string home)
{
    public const int DefaultPort = 4402;
    static readonly Regex Username = new(@"^[A-Za-z0-9][A-Za-z0-9._/-]{0,63}$", RegexOptions.Compiled);

    public string Path => System.IO.Path.Combine(home, "config.json");

    public JsonObject Load()
    {
        if (!File.Exists(Path)) return new JsonObject();
        return Json.ParseObject(File.ReadAllText(Path), Path);
    }

    /// <summary>The stored config with defaults filled in. A bad file falls back to defaults rather than blocking a run.</summary>
    public JsonObject Effective()
    {
        JsonObject c;
        try { c = Load(); } catch (GateException) { c = new JsonObject(); }
        var e = new JsonObject
        {
            ["username"] = Json.Str(c["username"]),
            ["worktree-root"] = Json.Str(c["worktree-root"]),
            ["ui"] = Json.Str(c["ui"]) == "console" ? "console" : "browser",
            ["port"] = Json.Int(c["port"]) is >= 1024 and <= 65535 and var p ? p : DefaultPort,
            ["browser"] = Json.Bool(c["browser"]) ?? true,
            ["idleMinutes"] = Json.Int(c["idleMinutes"]) is >= 1 and <= 1440 and var m ? m : 30,
        };
        return e;
    }

    public int Port => Json.Int(Effective()["port"]) ?? DefaultPort;

    public static bool ValidUsername(string s) =>
        Username.IsMatch(s) && !s.Contains("..") && !s.Contains("//") && !s.EndsWith('/') && !s.EndsWith('.') && !s.EndsWith(".lock");

    /// <summary>Merges the given keys into the file, keeping any keys it does not know.</summary>
    public JsonObject Save(JsonObject patch)
    {
        var cur = Load();
        foreach (var (k, v) in patch)
        {
            switch (k)
            {
                case "username":
                    var u = (Json.Str(v) ?? "").Trim();
                    if (!ValidUsername(u)) throw new GateException(400, "username must be usable in a git branch name: letters, digits, . _ - /, no spaces, no '..', not starting with '-'");
                    cur[k] = u;
                    break;
                case "worktree-root":
                    var w = (Json.Str(v) ?? "").Trim().Replace('\\', '/');
                    if (w.Length == 0) cur.Remove(k);
                    else if (w.Length > 400 || !(w.StartsWith('/') || w.Length > 2 && w[1] == ':'))
                        throw new GateException(400, "worktree-root must be an absolute folder path");
                    else cur[k] = w;
                    break;
                case "ui":
                    var ui = Json.Str(v);
                    if (ui is not ("browser" or "console")) throw new GateException(400, "ui must be browser or console");
                    cur[k] = ui;
                    break;
                case "port":
                    cur[k] = Json.Int(v) is >= 1024 and <= 65535 and var p ? p : throw new GateException(400, "port must be 1024-65535");
                    break;
                case "browser":
                    cur[k] = Json.Bool(v) ?? throw new GateException(400, "browser must be true or false");
                    break;
                case "idleMinutes":
                    cur[k] = Json.Int(v) is >= 1 and <= 1440 and var m ? m : throw new GateException(400, "idleMinutes must be 1-1440");
                    break;
                default:
                    throw new GateException(400, $"unknown setting '{k}'");
            }
        }
        Json.WriteAtomic(Path, cur);
        return Effective();
    }
}
