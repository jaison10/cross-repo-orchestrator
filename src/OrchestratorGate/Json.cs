using System.Text.Json;
using System.Text.Json.Nodes;

namespace OrchestratorGate;

static class Json
{
    public static JsonObject ParseObject(string text, string what)
    {
        try
        {
            return JsonNode.Parse(text) as JsonObject ?? throw new GateException(500, $"{what} is not a JSON object");
        }
        catch (JsonException e)
        {
            throw new GateException(500, $"{what}: {e.Message}");
        }
    }

    public static string? Str(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
    public static bool? Bool(JsonNode? n) => n is JsonValue v && v.TryGetValue<bool>(out var b) ? b : null;

    public static int? Int(JsonNode? n)
    {
        if (n is not JsonValue v) return null;
        if (v.TryGetValue<int>(out var i)) return i;
        if (v.TryGetValue<long>(out var l) && l is >= int.MinValue and <= int.MaxValue) return (int)l;
        return null;
    }

    /// <summary>Write via a temp file so a reader never sees half a file.</summary>
    public static void WriteAtomic(string path, JsonNode node)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + "." + Environment.ProcessId + ".tmp";
        File.WriteAllText(tmp, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tmp, path, overwrite: true);
    }
}
