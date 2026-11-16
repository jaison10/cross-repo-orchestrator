using System.Text.Json.Nodes;
using OrchestratorGate;
using Xunit;

namespace OrchestratorGate.Tests;

public class WaiterTests
{
    static readonly TimeSpan Fast = TimeSpan.FromMilliseconds(20);

    [Fact]
    public async Task Returns_at_once_when_the_round_was_already_submitted()
    {
        using var f = new Fixture();
        f.Patch("""{"submit":{"action":"console","round":1}}""");
        var r = await Waiter.WaitAsync(f.Store, f.Ref, 1, TimeSpan.FromMinutes(5), CancellationToken.None, Fast);
        Assert.Equal("submitted", (string)r["status"]!);
        Assert.Equal("console", (string)r["action"]!);
    }

    [Fact]
    public async Task Blocks_until_the_page_submits_and_returns_the_answers()
    {
        using var f = new Fixture();
        var wait = Waiter.WaitAsync(f.Store, f.Ref, 1, TimeSpan.FromMinutes(5), CancellationToken.None, Fast);
        await Task.Delay(150);
        Assert.False(wait.IsCompleted);

        f.AnswerEverything();
        f.Patch("""{"submit":{"action":"approve","round":1}}""");

        var r = await wait.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("approve", (string)r["action"]!);
        Assert.Equal("Full only", (string)r["state"]!["answers"]!["q1"]!["choice"]!);
    }

    [Fact]
    public async Task A_submit_for_an_earlier_round_does_not_satisfy_a_later_wait()
    {
        using var f = new Fixture();
        f.Patch("""{"submit":{"action":"revise","round":1}}""");
        var r = await Waiter.WaitAsync(f.Store, f.Ref, 2, TimeSpan.FromMilliseconds(1), CancellationToken.None, Fast);
        Assert.Equal("timeout", (string)r["status"]!);
    }

    [Fact]
    public async Task Gives_up_after_the_idle_time()
    {
        using var f = new Fixture();
        File.SetLastWriteTimeUtc(f.Ref.ContentPath(f.Home), DateTime.UtcNow.AddHours(-1));
        var r = await Waiter.WaitAsync(f.Store, f.Ref, 1, TimeSpan.FromMinutes(30), CancellationToken.None, Fast);
        Assert.Equal("timeout", (string)r["status"]!);
    }

    [Fact]
    public async Task Activity_in_the_page_keeps_the_wait_alive()
    {
        using var f = new Fixture();
        File.SetLastWriteTimeUtc(f.Ref.ContentPath(f.Home), DateTime.UtcNow.AddHours(-1));
        f.Patch("""{"concerns":"still thinking"}"""); // fresh state file = recent activity
        var wait = Waiter.WaitAsync(f.Store, f.Ref, 1, TimeSpan.FromMinutes(30), CancellationToken.None, Fast);
        await Task.Delay(150);
        Assert.False(wait.IsCompleted);
        f.Patch("""{"submit":{"action":"console","round":1}}""");
        Assert.Equal("submitted", (string)(await wait.WaitAsync(TimeSpan.FromSeconds(5)))["status"]!);
    }

    [Fact]
    public async Task A_missing_gate_is_an_error()
    {
        using var f = new Fixture();
        var ghost = GateRef.Create("k", "t", "gate-9");
        var e = await Assert.ThrowsAsync<GateException>(() => Waiter.WaitAsync(f.Store, ghost, 1, TimeSpan.FromMinutes(1), CancellationToken.None, Fast));
        Assert.Equal(404, e.Code);
    }
}

public class ConfigTests : IDisposable
{
    readonly string _home = Path.Combine(Path.GetTempPath(), "og-cfg-" + Guid.NewGuid().ToString("N")[..8]);
    ConfigFile Cfg => new(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, true); } catch { }
    }

    [Fact]
    public void Without_a_file_the_defaults_apply_and_nothing_is_created()
    {
        var e = Cfg.Effective();
        Assert.Equal(4402, (int)e["port"]!);
        Assert.Equal("browser", (string)e["ui"]!);
        Assert.True((bool)e["browser"]!);
        Assert.Equal(30, (int)e["idleMinutes"]!);
        Assert.Null(e["username"]);
        Assert.False(Directory.Exists(_home));
    }

    [Theory]
    [InlineData("jdsouza", true)]
    [InlineData("j.dsouza-1", true)]
    [InlineData("team/jd", true)]
    [InlineData("has space", false)]
    [InlineData("-lead", false)]
    [InlineData("a..b", false)]
    [InlineData("a//b", false)]
    [InlineData("trail/", false)]
    [InlineData("trail.", false)]
    [InlineData("x.lock", false)]
    [InlineData("", false)]
    [InlineData("semi;colon", false)]
    public void Username_must_be_safe_in_a_branch_name(string name, bool ok) =>
        Assert.Equal(ok, ConfigFile.ValidUsername(name));

    [Fact]
    public void Save_merges_and_keeps_keys_it_does_not_know()
    {
        Directory.CreateDirectory(_home);
        File.WriteAllText(Cfg.Path, """{"custom":"keep me","port":4500}""");
        var e = Cfg.Save(JsonNode.Parse("""{"username":"jdsouza","worktree-root":"C:\\wt"}""")!.AsObject());
        Assert.Equal("jdsouza", (string)e["username"]!);
        Assert.Equal("C:/wt", (string)e["worktree-root"]!);
        Assert.Equal(4500, (int)e["port"]!);
        var raw = JsonNode.Parse(File.ReadAllText(Cfg.Path))!;
        Assert.Equal("keep me", (string)raw["custom"]!);
    }

    [Theory]
    [InlineData("""{"username":"bad name"}""")]
    [InlineData("""{"worktree-root":"relative/dir"}""")]
    [InlineData("""{"ui":"tv"}""")]
    [InlineData("""{"port":80}""")]
    [InlineData("""{"idleMinutes":0}""")]
    [InlineData("""{"browser":"yes"}""")]
    [InlineData("""{"surprise":1}""")]
    public void Save_refuses_bad_values(string json)
    {
        var e = Assert.Throws<GateException>(() => Cfg.Save(JsonNode.Parse(json)!.AsObject()));
        Assert.Equal(400, e.Code);
        Assert.False(File.Exists(Cfg.Path));
    }

    [Fact]
    public void A_corrupt_file_falls_back_to_defaults_for_reading()
    {
        Directory.CreateDirectory(_home);
        File.WriteAllText(Cfg.Path, "{ not json");
        Assert.Equal(4402, Cfg.Port);
    }

    [Fact]
    public void An_empty_worktree_root_clears_it()
    {
        Cfg.Save(JsonNode.Parse("""{"worktree-root":"C:/wt"}""")!.AsObject());
        var e = Cfg.Save(JsonNode.Parse("""{"worktree-root":""}""")!.AsObject());
        Assert.Null(e["worktree-root"]);
    }
}
