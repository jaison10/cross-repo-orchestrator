using System.Text.Json.Nodes;

namespace OrchestratorGate;

/// <summary>
/// Blocks until the page submits the given round. Run by the orchestrator with run_in_background:
/// the process exiting is what resumes Claude. Closing the tab does nothing; only a submit (or the idle timeout) ends the wait.
/// </summary>
public static class Waiter
{
    public static async Task<JsonObject> WaitAsync(GateStore store, GateRef r, int round, TimeSpan idle, CancellationToken ct, TimeSpan? poll = null)
    {
        var home = store.Home;
        while (true)
        {
            var cp = r.ContentPath(home);
            if (!File.Exists(cp)) throw new GateException(404, $"no gate file {r.Fragment}");

            var state = store.LoadState(r);
            if (state["submitted"] is JsonObject s && (Json.Int(s["round"]) ?? 0) >= round)
            {
                return new JsonObject
                {
                    ["status"] = "submitted",
                    ["action"] = Json.Str(s["action"]),
                    ["round"] = Json.Int(s["round"]),
                    ["at"] = Json.Str(s["at"]),
                    ["state"] = state,
                };
            }

            var last = File.GetLastWriteTimeUtc(cp);
            var sp = r.StatePath(home);
            if (File.Exists(sp)) last = Max(last, File.GetLastWriteTimeUtc(sp));
            if (DateTime.UtcNow - last > idle)
                return new JsonObject { ["status"] = "timeout", ["idleMinutes"] = (int)idle.TotalMinutes, ["state"] = state };

            await Task.Delay(poll ?? TimeSpan.FromMilliseconds(500), ct);
        }
    }

    static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;
}
