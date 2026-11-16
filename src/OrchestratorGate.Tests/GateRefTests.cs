using OrchestratorGate;
using Xunit;

namespace OrchestratorGate.Tests;

public class GateRefTests
{
    static readonly string Home = Path.Combine(Path.GetTempPath(), "og-home");

    static string Gate(params string[] parts) => Path.Combine([Home, .. parts]);

    [Fact]
    public void Reads_key_task_and_gate_from_a_file_path()
    {
        var r = GateRef.FromFile(Home, Gate("demo-1a2b3c4d", "tasks", "refund-2026-11-15", "gates", "gate-2.json"));
        Assert.Equal(new GateRef("demo-1a2b3c4d", "refund-2026-11-15", "gate-2"), r);
        Assert.Equal("demo-1a2b3c4d/refund-2026-11-15/gate-2", r.Fragment);
    }

    [Fact]
    public void Rejects_a_path_outside_the_home()
    {
        var other = Path.Combine(Path.GetTempPath(), "elsewhere", "k", "tasks", "t", "gates", "gate-1.json");
        Assert.Equal(400, Assert.Throws<GateException>(() => GateRef.FromFile(Home, other)).Code);
    }

    [Fact]
    public void Rejects_dot_dot_segments_that_escape_the_home()
    {
        var sneaky = Gate("k", "tasks", "t", "gates", "..", "..", "..", "..", "..", "x", "tasks", "t", "gates", "gate-1.json");
        Assert.Throws<GateException>(() => GateRef.FromFile(Home, sneaky));
    }

    [Theory]
    [InlineData("k", "tasks", "t", "gates", "gate-1.state.json")]
    [InlineData("k", "tasks", "t", "gates", "notes.json")]
    [InlineData("k", "tasks", "t", "gate-1.json")]
    [InlineData("k", "other", "t", "gates", "gate-1.json")]
    public void Rejects_the_wrong_shape(params string[] parts)
    {
        Assert.Throws<GateException>(() => GateRef.FromFile(Home, Gate(parts)));
    }

    [Theory]
    [InlineData("..", "t", "gate-1")]
    [InlineData("k", ".", "gate-1")]
    [InlineData("k/x", "t", "gate-1")]
    [InlineData("k", "t", "gate-x")]
    [InlineData("k", "t", "gate-1000")]
    [InlineData("", "t", "gate-1")]
    public void Create_rejects_bad_segments(string key, string task, string gate)
    {
        Assert.Throws<GateException>(() => GateRef.Create(key, task, gate));
    }

    [Fact]
    public void Turns_git_bash_drive_paths_into_windows_paths()
    {
        if (!OperatingSystem.IsWindows()) return;
        Assert.Equal(@"C:\Users\me\x", GateRef.NormalizeShellPath("/c/Users/me/x"));
        Assert.Equal(@"C:\Users\me\x", GateRef.NormalizeShellPath(@"C:\Users\me\x"));
    }
}
