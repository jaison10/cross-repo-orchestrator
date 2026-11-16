using System.Text.RegularExpressions;

namespace OrchestratorGate;

public sealed class GateException(int code, string message) : Exception(message)
{
    public int Code { get; } = code;
}

/// <summary>
/// Identifies one gate: &lt;home&gt;/&lt;key&gt;/tasks/&lt;task&gt;/gates/&lt;gate&gt;.json.
/// Every segment is validated, so a reference can never point outside the orchestrator home.
/// </summary>
public sealed record GateRef(string Key, string Task, string Gate)
{
    static readonly Regex Segment = new(@"^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$", RegexOptions.Compiled);
    static readonly Regex GateName = new(@"^gate-\d{1,3}$", RegexOptions.Compiled);
    static readonly Regex MsysDrive = new(@"^/([A-Za-z])/(.*)$", RegexOptions.Compiled);

    public static readonly string[] Reports = ["analysis-report.md", "plan.md", "verification-report.md"];

    public static GateRef Create(string key, string task, string gate)
    {
        if (!Segment.IsMatch(key) || !Segment.IsMatch(task) || !GateName.IsMatch(gate))
            throw new GateException(400, "bad gate reference");
        return new GateRef(key, task, gate);
    }

    public static GateRef FromFile(string home, string file)
    {
        var full = Path.GetFullPath(NormalizeShellPath(file));
        var cmp = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var root = Path.GetFullPath(home).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(root, cmp))
            throw new GateException(400, $"{file} is not under {root}");
        var parts = full[root.Length..].Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (parts.Length != 5 || parts[1] != "tasks" || parts[3] != "gates"
            || !parts[4].EndsWith(".json", StringComparison.Ordinal) || parts[4].EndsWith(".state.json", StringComparison.Ordinal))
            throw new GateException(400, "expected <home>/<key>/tasks/<task>/gates/gate-N.json");
        return Create(parts[0], parts[2], parts[4][..^".json".Length]);
    }

    /// <summary>Git Bash hands native programs paths like /c/Users/me; turn those into C:\Users\me on Windows.</summary>
    public static string NormalizeShellPath(string p)
    {
        if (!OperatingSystem.IsWindows()) return p;
        var m = MsysDrive.Match(p);
        return m.Success ? $"{m.Groups[1].Value.ToUpperInvariant()}:\\{m.Groups[2].Value.Replace('/', '\\')}" : p;
    }

    public string TaskDir(string home) => Path.Combine(home, Key, "tasks", Task);
    public string ContentPath(string home) => Path.Combine(TaskDir(home), "gates", Gate + ".json");
    public string StatePath(string home) => Path.Combine(TaskDir(home), "gates", Gate + ".state.json");
    public string Fragment => $"{Key}/{Task}/{Gate}";
}
