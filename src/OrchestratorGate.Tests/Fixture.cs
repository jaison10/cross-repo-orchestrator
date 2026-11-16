using System.Text.Json.Nodes;
using OrchestratorGate;

namespace OrchestratorGate.Tests;

/// <summary>A throwaway orchestrator home with one gate, so tests never touch ~/.orchestrator.</summary>
public sealed class Fixture : IDisposable
{
    public string Home { get; } = Path.Combine(Path.GetTempPath(), "og-test-" + Guid.NewGuid().ToString("N")[..8]);
    public GateStore Store { get; }
    public GateRef Ref { get; } = GateRef.Create("demo-1a2b3c4d", "refund-2026-11-15", "gate-2");

    public Fixture(JsonObject? content = null)
    {
        Store = new GateStore(Home);
        Directory.CreateDirectory(Path.GetDirectoryName(Ref.ContentPath(Home))!);
        WriteContent(content ?? Default());
    }

    public static JsonObject Default() => JsonNode.Parse("""
        {
          "round": 1,
          "questions": [
            { "id": "q1", "text": "Partial refunds?", "options": ["Full only", "Partial too"] },
            { "id": "q2", "text": "Currency rules?" }
          ],
          "repos": [
            { "id": "api", "name": "api", "dirty": true },
            { "id": "web", "name": "web", "dirty": false }
          ],
          "needsWorktreeRoot": true
        }
        """)!.AsObject();

    public void WriteContent(JsonObject c) => File.WriteAllText(Ref.ContentPath(Home), c.ToJsonString());

    public JsonObject Patch(string json) => Store.Patch(Ref, JsonNode.Parse(json)!.AsObject());

    public void AnswerEverything()
    {
        Patch("""{"answer":{"id":"q1","choice":"Full only"}}""");
        Patch("""{"answer":{"id":"q2","text":"EUR"}}""");
        Patch("""{"repo":{"id":"api","dirtyChoice":"keep"}}""");
    }

    public void Dispose()
    {
        try { Directory.Delete(Home, true); } catch { }
    }
}
