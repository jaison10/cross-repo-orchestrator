using System.Text.Json.Nodes;

namespace OrchestratorGate;

/// <summary>
/// Two files per gate, so nothing ever writes the same file:
///   gate-N.json        content, written by Claude (understanding, repos, plan, questions, thread)
///   gate-N.state.json  state, written only through this class by the page (answers, concerns, submit)
/// </summary>
public sealed class GateStore(string home)
{
    public const int MaxText = 10_000;
    static readonly string[] Actions = ["approve", "revise", "ask", "console"];
    static readonly string[] DirtyChoices = ["keep", "stash", "worktree"];
    readonly object _lock = new();

    public string Home => home;

    public JsonObject Read(GateRef r)
    {
        var cp = r.ContentPath(home);
        if (!File.Exists(cp)) throw new GateException(404, $"no gate file {r.Fragment}");
        var content = Json.ParseObject(File.ReadAllText(cp), cp);
        var state = LoadState(r);
        return new JsonObject { ["content"] = content, ["state"] = state, ["version"] = Version(r) };
    }

    public string Version(GateRef r) =>
        $"{File.GetLastWriteTimeUtc(r.ContentPath(home)).Ticks}-{File.GetLastWriteTimeUtc(r.StatePath(home)).Ticks}";

    public JsonObject LoadState(GateRef r)
    {
        var sp = r.StatePath(home);
        var state = File.Exists(sp) ? Json.ParseObject(File.ReadAllText(sp), sp) : new JsonObject();
        return Normalize(state);
    }

    static JsonObject Normalize(JsonObject s)
    {
        if (s["answers"] is not JsonObject) s["answers"] = new JsonObject();
        if (s["repos"] is not JsonObject) s["repos"] = new JsonObject();
        if (s["asked"] is not JsonArray) s["asked"] = new JsonArray();
        if (s["concerns"] is not JsonValue) s["concerns"] = "";
        if (!s.ContainsKey("worktreeRoot")) s["worktreeRoot"] = null;
        if (!s.ContainsKey("submitted")) s["submitted"] = null;
        return s;
    }

    /// <summary>Applies exactly one patch to the page-owned state and returns the new state.</summary>
    public JsonObject Patch(GateRef r, JsonObject patch)
    {
        if (patch.Count != 1) throw new GateException(400, "a patch has exactly one key");
        lock (_lock)
        {
            var content = Json.ParseObject(File.Exists(r.ContentPath(home))
                ? File.ReadAllText(r.ContentPath(home))
                : throw new GateException(404, $"no gate file {r.Fragment}"), r.ContentPath(home));
            var state = LoadState(r);
            var round = Json.Int(content["round"]) ?? 1;

            var submitted = state["submitted"] as JsonObject;
            if (submitted is not null && Json.Int(submitted["round"]) == round)
                throw new GateException(409, "this round was already submitted; wait for Claude's next round");

            var (kind, value) = patch.First();
            switch (kind)
            {
                case "answer": SetAnswer(state, value); break;
                case "repo": SetRepo(state, value); break;
                case "concerns": state["concerns"] = Text(value, "concerns", allowEmpty: true); break;
                case "worktreeRoot":
                    var wr = Text(value, "worktreeRoot", allowEmpty: true);
                    state["worktreeRoot"] = wr.Length == 0 ? null : wr.Replace('\\', '/');
                    break;
                case "ask": Ask(state, value, round); break;
                case "submit": Submit(state, content, value, round); break;
                default: throw new GateException(400, $"unknown patch '{kind}'");
            }

            Json.WriteAtomic(r.StatePath(home), state);
            return state;
        }
    }

    static string Text(JsonNode? n, string name, bool allowEmpty = false, int max = MaxText)
    {
        var s = Json.Str(n) ?? throw new GateException(400, $"{name} must be text");
        if (s.Length > max) throw new GateException(400, $"{name} is longer than {max} characters");
        if (!allowEmpty && string.IsNullOrWhiteSpace(s)) throw new GateException(400, $"{name} must not be empty");
        return s;
    }

    static string Id(JsonNode? n) =>
        Json.Str(n) is { Length: > 0 and <= 64 } id ? id : throw new GateException(400, "id required");

    static void SetAnswer(JsonObject state, JsonNode? v)
    {
        if (v is not JsonObject o) throw new GateException(400, "bad answer");
        var id = Id(o["id"]);
        var choice = o["choice"] is null ? "" : Text(o["choice"], "choice", true, 500);
        var text = o["text"] is null ? "" : Text(o["text"], "text", true);
        var answers = (JsonObject)state["answers"]!;
        if (choice.Length == 0 && text.Trim().Length == 0) answers.Remove(id);
        else answers[id] = new JsonObject { ["choice"] = choice, ["text"] = text };
    }

    static void SetRepo(JsonObject state, JsonNode? v)
    {
        if (v is not JsonObject o) throw new GateException(400, "bad repo");
        var id = Id(o["id"]);
        var repos = (JsonObject)state["repos"]!;
        var cur = repos[id] as JsonObject ?? new JsonObject();
        if (o.ContainsKey("include"))
            cur["include"] = Json.Bool(o["include"]) ?? throw new GateException(400, "include must be true or false");
        if (o.ContainsKey("dirtyChoice"))
        {
            var c = o["dirtyChoice"] is null ? null : Json.Str(o["dirtyChoice"]);
            if (c is not null && !DirtyChoices.Contains(c)) throw new GateException(400, "bad dirtyChoice");
            cur["dirtyChoice"] = c;
        }
        repos[id] = cur;
    }

    static void Ask(JsonObject state, JsonNode? v, int round)
    {
        if (v is not JsonObject o) throw new GateException(400, "bad question");
        var text = Text(o["text"], "text").Trim();
        var asked = (JsonArray)state["asked"]!;
        asked.Add((JsonNode)new JsonObject
        {
            ["id"] = "u" + (asked.Count + 1),
            ["text"] = text,
            ["round"] = round,
            ["at"] = DateTime.UtcNow.ToString("o"),
        });
    }

    static void Submit(JsonObject state, JsonObject content, JsonNode? v, int round)
    {
        if (v is not JsonObject o) throw new GateException(400, "bad submit");
        var action = Json.Str(o["action"]);
        if (action is null || !Actions.Contains(action)) throw new GateException(400, "bad action");
        if (Json.Int(o["round"]) != round)
            throw new GateException(409, "the page is out of date (round changed); reload it");

        if (action == "approve")
        {
            var problems = Problems(state, content);
            if (problems.Count > 0) throw new GateException(400, string.Join("; ", problems));
        }
        if (action == "ask" && !((JsonArray)state["asked"]!).Any(a => Json.Int(a?["round"]) == round))
            throw new GateException(400, "write a question first");

        state["submitted"] = new JsonObject { ["action"] = action, ["round"] = round, ["at"] = DateTime.UtcNow.ToString("o") };
    }

    /// <summary>What still blocks an approval: unanswered questions, no repo chosen, a dirty repo with no decision.</summary>
    public static List<string> Problems(JsonObject state, JsonObject content)
    {
        var problems = new List<string>();
        var answers = (JsonObject)state["answers"]!;
        var open = (content["questions"] as JsonArray ?? [])
            .Select(q => Json.Str(q?["id"])).Where(id => id is not null)
            .Where(id => answers[id!] is not JsonObject a
                || (Json.Str(a["choice"]) ?? "").Length + (Json.Str(a["text"]) ?? "").Trim().Length == 0)
            .ToList();
        if (open.Count > 0) problems.Add($"{open.Count} unanswered question(s)");

        if (content["repos"] is JsonArray { Count: > 0 } repos)
        {
            var overrides = (JsonObject)state["repos"]!;
            var included = 0;
            var needRoot = false;
            foreach (var repo in repos.OfType<JsonObject>())
            {
                var id = Json.Str(repo["id"]) ?? "";
                var ov = overrides[id] as JsonObject;
                var on = Json.Bool(ov?["include"]) ?? Json.Bool(repo["include"]) ?? true;
                if (!on) continue;
                included++;
                if (Json.Bool(repo["dirty"]) == true)
                {
                    var choice = Json.Str(ov?["dirtyChoice"]);
                    if (choice is null) problems.Add($"choose what to do with uncommitted changes in {Json.Str(repo["name"]) ?? id}");
                    needRoot |= choice == "worktree";
                }
            }
            if (included == 0) problems.Add("select at least one repo");
            if (needRoot && Json.Bool(content["needsWorktreeRoot"]) == true && string.IsNullOrWhiteSpace(Json.Str(state["worktreeRoot"])))
                problems.Add("enter the worktree folder");
        }
        return problems;
    }

    /// <summary>Read-only text of a task report, for the page's "open full report" links.</summary>
    public string ReadReport(GateRef r, string name)
    {
        if (!GateRef.Reports.Contains(name)) throw new GateException(404, "unknown report");
        var p = Path.Combine(r.TaskDir(home), name);
        return File.Exists(p) ? File.ReadAllText(p) : throw new GateException(404, $"{name} does not exist yet");
    }
}
