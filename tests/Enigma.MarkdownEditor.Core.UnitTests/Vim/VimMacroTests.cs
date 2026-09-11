using Enigma.MarkdownEditor.Core.Vim;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Vim;

/// <summary>
/// Macros: <c>q</c> to record a run of keys into a register, <c>@</c> to run it back.
/// </summary>
/// <remarks>
/// Recording runs the keys as it collects them — that is what vim does, and what makes a macro
/// something you watch work before you replay it — so a test both drives the recording and reads what
/// it left in the register. Replay is the harness's own loop moved into the engine: a key the engine
/// turns down is typed into the buffer, so a macro is exactly what typing the keys would have been,
/// down to where it stops when a find or a search inside it fails.
/// </remarks>
public sealed class VimMacroTests
{
    [Fact]
    public void ARecordedMacro_HoldsTheKeysAndReplays()
    {
        // Recording 'dw' runs it once (foo goes); '@a' runs it again (bar goes).
        VimRun run = VimScript.Run("|foo bar baz", "qadwq@a");

        Assert.Equal("dw", run.RegisterNamed('a')?.Text);
        Assert.Equal("|baz", run.Text);
    }

    [Fact]
    public void ACountOnAtSign_ReplaysThatManyTimes()
        => Assert.Equal("|e f", VimScript.Run("|a b c d e f", "qadwq3@a").Text);

    [Fact]
    public void AtAt_ReplaysTheRegisterLastReplayed()
        => Assert.Equal("|d", VimScript.Run("|a b c d", "qadwq@a@@").Text);

    [Fact]
    public void WhileRecording_TheStatusReadsRecordingAndTheRegisterIsKnown()
    {
        VimRun run = VimScript.Run("a|bc", "qax");

        Assert.Equal("recording @a", run.Status);
        Assert.Equal('a', run.Engine.RecordingRegister);
    }

    [Fact]
    public void WhenRecordingStops_TheStatusClears()
    {
        VimRun run = VimScript.Run("a|bc", "qaxq");

        Assert.Null(run.Status);
        Assert.Null(run.Engine.RecordingRegister);
    }

    [Fact]
    public void TheRecordingDoesNotIncludeTheQsThatBoundIt()
    {
        VimRun run = VimScript.Run("|hello world", "qaciwfoo<Esc>q");

        // The insert session's text is in the register as vim writes it, so "ap would paste it and it
        // can be edited — the q's that opened and closed the recording are not part of it.
        Assert.Equal("ciwfoo<Esc>", run.RegisterNamed('a')?.Text);
    }

    [Fact]
    public void AMacroThatEntersInsertMode_ReplaysTheTypedText()
    {
        VimRun run = VimScript.Run("|hello world", "qaciwfoo<Esc>qw@a");

        Assert.Equal("foo fo|o", run.Text);
    }

    [Fact]
    public void AYankRegister_ReplaysAsKeys()
    {
        // Vim's rule: '@a' runs whatever is in a as keys, so a yanked line runs too — here the line
        // 'x', which deletes a character when it is run over the next line.
        VimRun run = VimScript.Run("|x\nabc", "\"ayyj@a");

        Assert.Equal("x\n|bc", run.Text);
    }

    [Fact]
    public void AMacroStopsAtAFindThatFindsNothing()
    {
        // Recorded over a line with a 'z', replayed over one without: the find fails and the 'x' after
        // it never runs, so the second line keeps its first character.
        VimRun run = VimScript.Run("|az\nab", "qafzxqj0@a");

        Assert.Equal("a\n|ab", run.Text);
    }

    [Fact]
    public void ARecursiveMacro_AbortsWithTheNestingMessage()
    {
        // 'a' calls itself: the live '@a' while recording finds nothing to run, but the keys are
        // recorded, so the replay recurses until it is stopped.
        VimRun run = VimScript.Run("a|bc", "qa@aq@a");

        Assert.Equal("Macro nesting too deep", run.Status);
    }

    [Fact]
    public void AtWithNoRegisterEverReplayed_SaysSo()
        => Assert.Equal("No previously used register", VimScript.Run("a|bc", "@@").Status);

    [Fact]
    public void TheLastMacro_SurvivesAReset()
    {
        FakeVimBuffer buffer = VimScript.NewBuffer("|a b c d");
        VimEngine engine = new(buffer);

        foreach (VimKey key in VimKeyNotation.Parse("qadwq@a"))
        {
            engine.HandleKey(key);
        }

        engine.Reset();

        // '@@' still knows which register — the last macro outlives the buffer, as the registers do.
        engine.HandleKey(VimKey.Typed('@'));
        engine.HandleKey(VimKey.Typed('@'));

        Assert.Equal("d", buffer.Text.TrimEnd());
    }
}
