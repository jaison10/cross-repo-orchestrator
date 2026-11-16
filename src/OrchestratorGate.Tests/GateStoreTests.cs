using System.Text.Json.Nodes;
using OrchestratorGate;
using Xunit;

namespace OrchestratorGate.Tests;

public class GateStoreTests
{
    static GateException Fails(Action a) => Assert.Throws<GateException>(a);

    [Fact]
    public void Read_returns_content_and_a_normalized_empty_state()
    {
        using var f = new Fixture();
        var g = f.Store.Read(f.Ref);
        Assert.Equal(1, (int)g["content"]!["round"]!);
        Assert.Empty(g["state"]!["answers"]!.AsObject());
        Assert.Null(g["state"]!["submitted"]);
    }

    [Fact]
    public void Missing_gate_is_404()
    {
        using var f = new Fixture();
        Assert.Equal(404, Fails(() => f.Store.Read(GateRef.Create("k", "t", "gate-9"))).Code);
    }

    [Fact]
    public void Answers_are_saved_and_cleared_when_emptied()
    {
        using var f = new Fixture();
        var s = f.Patch("""{"answer":{"id":"q1","choice":"Full only","text":"because"}}""");
        Assert.Equal("Full only", (string)s["answers"]!["q1"]!["choice"]!);
        s = f.Patch("""{"answer":{"id":"q1","choice":"","text":"  "}}""");
        Assert.False(s["answers"]!.AsObject().ContainsKey("q1"));
    }

    [Fact]
    public void A_patch_has_exactly_one_key_and_a_known_kind()
    {
        using var f = new Fixture();
        Assert.Equal(400, Fails(() => f.Patch("""{"concerns":"a","ask":{"text":"b"}}""")).Code);
        Assert.Equal(400, Fails(() => f.Patch("""{"nope":1}""")).Code);
    }

    [Fact]
    public void Text_longer_than_the_limit_is_refused()
    {
        using var f = new Fixture();
        var big = new string('x', GateStore.MaxText + 1);
        Assert.Equal(400, Fails(() => f.Patch($$"""{"concerns":"{{big}}"}""")).Code);
    }

    [Fact]
    public void Approve_is_blocked_until_every_question_is_answered_and_the_dirty_repo_decided()
    {
        using var f = new Fixture();
        var e = Fails(() => f.Patch("""{"submit":{"action":"approve","round":1}}"""));
        Assert.Equal(400, e.Code);
        Assert.Contains("unanswered", e.Message);
        Assert.Contains("uncommitted", e.Message);

        f.AnswerEverything();
        var s = f.Patch("""{"submit":{"action":"approve","round":1}}""");
        Assert.Equal("approve", (string)s["submitted"]!["action"]!);
    }

    [Fact]
    public void A_free_text_answer_counts_for_a_question_with_options()
    {
        using var f = new Fixture();
        f.Patch("""{"answer":{"id":"q1","text":"only for EU"}}""");
        f.Patch("""{"answer":{"id":"q2","text":"EUR"}}""");
        f.Patch("""{"repo":{"id":"api","dirtyChoice":"stash"}}""");
        f.Patch("""{"submit":{"action":"approve","round":1}}""");
    }

    [Fact]
    public void Excluding_a_dirty_repo_removes_its_decision_requirement()
    {
        using var f = new Fixture();
        f.Patch("""{"answer":{"id":"q1","choice":"Full only"}}""");
        f.Patch("""{"answer":{"id":"q2","text":"EUR"}}""");
        f.Patch("""{"repo":{"id":"api","include":false}}""");
        f.Patch("""{"submit":{"action":"approve","round":1}}""");
    }

    [Fact]
    public void Excluding_every_repo_blocks_approval()
    {
        using var f = new Fixture();
        f.AnswerEverything();
        f.Patch("""{"repo":{"id":"api","include":false}}""");
        f.Patch("""{"repo":{"id":"web","include":false}}""");
        Assert.Contains("at least one repo", Fails(() => f.Patch("""{"submit":{"action":"approve","round":1}}""")).Message);
    }

    [Fact]
    public void Choosing_a_worktree_requires_a_folder_when_the_gate_asks_for_one()
    {
        using var f = new Fixture();
        f.AnswerEverything();
        f.Patch("""{"repo":{"id":"api","dirtyChoice":"worktree"}}""");
        Assert.Contains("worktree folder", Fails(() => f.Patch("""{"submit":{"action":"approve","round":1}}""")).Message);
        var s = f.Patch("""{"worktreeRoot":"C:\\wt"}""");
        Assert.Equal("C:/wt", (string)s["worktreeRoot"]!);
        f.Patch("""{"submit":{"action":"approve","round":1}}""");
    }

    [Fact]
    public void A_bad_dirty_choice_is_refused()
    {
        using var f = new Fixture();
        Assert.Equal(400, Fails(() => f.Patch("""{"repo":{"id":"api","dirtyChoice":"discard"}}""")).Code);
    }

    [Fact]
    public void A_submit_for_another_round_is_a_409()
    {
        using var f = new Fixture();
        Assert.Equal(409, Fails(() => f.Patch("""{"submit":{"action":"revise","round":3}}""")).Code);
    }

    [Fact]
    public void After_a_submit_the_round_is_locked_until_claude_starts_the_next_one()
    {
        using var f = new Fixture();
        f.Patch("""{"submit":{"action":"revise","round":1}}""");
        Assert.Equal(409, Fails(() => f.Patch("""{"concerns":"too late"}""")).Code);

        var c = Fixture.Default();
        c["round"] = 2;
        f.WriteContent(c);
        var s = f.Patch("""{"concerns":"for round two"}""");
        Assert.Equal("for round two", (string)s["concerns"]!);
        Assert.Equal(1, (int)s["submitted"]!["round"]!); // old submit stays as history
    }

    [Fact]
    public void Ask_needs_a_question_in_this_round()
    {
        using var f = new Fixture();
        Assert.Contains("question", Fails(() => f.Patch("""{"submit":{"action":"ask","round":1}}""")).Message);

        var s = f.Patch("""{"ask":{"text":"  Does this touch the ledger?  "}}""");
        var asked = s["asked"]!.AsArray();
        Assert.Single(asked);
        Assert.Equal("u1", (string)asked[0]!["id"]!);
        Assert.Equal("Does this touch the ledger?", (string)asked[0]!["text"]!);
        f.Patch("""{"submit":{"action":"ask","round":1}}""");
    }

    [Fact]
    public void Console_and_revise_do_not_need_answers()
    {
        using var f = new Fixture();
        f.Patch("""{"submit":{"action":"console","round":1}}""");
    }

    [Fact]
    public void Only_known_reports_can_be_read()
    {
        using var f = new Fixture();
        File.WriteAllText(Path.Combine(f.Ref.TaskDir(f.Home), "plan.md"), "# plan");
        Assert.Equal("# plan", f.Store.ReadReport(f.Ref, "plan.md"));
        Assert.Equal(404, Fails(() => f.Store.ReadReport(f.Ref, "../../config.json")).Code);
        Assert.Equal(404, Fails(() => f.Store.ReadReport(f.Ref, "analysis-report.md")).Code); // known name, not written yet
    }

    [Fact]
    public void The_state_file_is_separate_from_the_content_file()
    {
        using var f = new Fixture();
        var before = File.ReadAllText(f.Ref.ContentPath(f.Home));
        f.AnswerEverything();
        Assert.Equal(before, File.ReadAllText(f.Ref.ContentPath(f.Home)));
        Assert.True(File.Exists(f.Ref.StatePath(f.Home)));
    }
}
