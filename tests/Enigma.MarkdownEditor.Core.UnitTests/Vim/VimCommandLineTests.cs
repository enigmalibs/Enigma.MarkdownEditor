using Enigma.MarkdownEditor.Core.Vim;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Vim;

/// <summary>
/// The command line: what opens it, what goes into it, what closes it, and what nothing typed into it
/// is allowed to do to the document.
/// </summary>
/// <remarks>
/// <para>
/// It is not a mode, which is the point most of these cases are making: the editor is still in normal
/// mode and still says <c>NORMAL</c>, and yet <c>dd</c> typed after a <c>:</c> is three characters of
/// a line rather than a deleted one. Every case that types a command into the strip and asserts the
/// text is unchanged is holding that promise.
/// </para>
/// <para>
/// The other half is the keys the command line needed named — Enter and Backspace — which are motions
/// in normal mode once the line is closed, and the editor's own in insert mode.
/// </para>
/// </remarks>
public sealed class VimCommandLineTests
{
    [Theory]
    [InlineData(":", ":")]
    [InlineData("/", "/")]
    [InlineData(":wq", ":wq")]
    [InlineData("/foo bar", "/foo bar")]
    // The strip shows what was typed, whatever it spells: ':' has already taken the keys away from
    // the grammar, so 'dd' is two characters rather than a command.
    [InlineData(":dd", ":dd")]
    [InlineData("/3x$", "/3x$")]
    [InlineData("?foo", "?foo")]
    public void TypingAfterTheOpeningKey_BuildsTheLineAndLeavesTheDocumentAlone(string keys, string line)
    {
        VimRun run = VimScript.Run("|abc\ndef", keys);

        Assert.Equal(line, run.CommandLine);
        Assert.Equal("abc\ndef", run.Buffer.Text);
        Assert.Equal(VimMode.Normal, run.Mode);
    }

    [Fact]
    public void AnOpenCommandLine_CountsAsSomethingHalfTyped()
        => Assert.True(VimScript.Run("|abc", ":w").Engine.HasPendingSequence);

    [Fact]
    public void TheEngineTellsWhatIsOnTheLine_AsWellAsShowingIt()
        => Assert.Equal(":wq", VimScript.Run("|abc", ":wq").Engine.CommandLine);

    [Fact]
    public void OpeningTheLine_ClearsWhateverTheEngineWasSaying()
    {
        // 'Q' is not a command, so the engine complains; ':' has to take the complaint down with it
        // rather than leave it beside a line that is being typed.
        VimRun run = VimScript.Run("|abc", "Q:");

        Assert.Null(run.Status);
        Assert.Equal(":", run.CommandLine);
    }

    [Theory]
    [InlineData(":wq<Esc>")]
    [InlineData("/foo<Esc>")]
    [InlineData("?foo<Esc>")]
    [InlineData(":<Esc>")]
    public void Escape_TakesTheLineAwayAndRunsNothing(string keys)
    {
        VimRun run = VimScript.Run("|abc", keys);

        Assert.Null(run.CommandLine);
        Assert.Empty(run.ExCommands);
        Assert.Equal("abc", run.Buffer.Text);
        Assert.False(run.Engine.HasPendingSequence);
    }

    [Fact]
    public void AfterEscape_TheKeysAreCommandsAgain()
        => Assert.Equal("|bc", VimScript.Run("|abc", ":q<Esc>x").Text);

    [Theory]
    [InlineData(":wqx<BS>", ":wq")]
    [InlineData("/foo<BS><BS>", "/f")]
    public void Backspace_RubsOutTheLastCharacter(string keys, string line)
        => Assert.Equal(line, VimScript.Run("|abc", keys).CommandLine);

    [Theory]
    // Rubbing out the ':' itself is the way out of a line with nothing left in it.
    [InlineData(":w<BS><BS>")]
    [InlineData("/<BS>")]
    public void BackspacingOverTheOpeningKey_ClosesTheLine(string keys)
    {
        VimRun run = VimScript.Run("|abc", keys);

        Assert.Null(run.CommandLine);
        Assert.Equal("abc", run.Buffer.Text);
    }

    [Theory]
    [InlineData(":w<CR>", VimExCommand.Write)]
    [InlineData(":q<CR>", VimExCommand.Quit)]
    [InlineData(":wq<CR>", VimExCommand.WriteQuit)]
    [InlineData(":x<CR>", VimExCommand.WriteQuit)]
    [InlineData(":wq!<CR>", VimExCommand.WriteQuit)]
    [InlineData(":q!<CR>", VimExCommand.QuitDiscard)]
    [InlineData(": w <CR>", VimExCommand.Write)]
    public void EnterRunsTheLine_AndAsksForTheCommandOnce(string keys, VimExCommand expected)
    {
        VimRun run = VimScript.Run("|abc", keys);

        Assert.Equal(expected, Assert.Single(run.ExCommands));
        Assert.Null(run.CommandLine);
        Assert.Null(run.Status);
    }

    [Theory]
    [InlineData(":wa<CR>", "Not an editor command: wa")]
    [InlineData(":w!<CR>", "Not an editor command: w!")]
    [InlineData(":wqa<CR>", "Not an editor command: wqa")]
    [InlineData(":foo<CR>", "Not an editor command: foo")]
    // A range on the four file requests: vim writes part of a file with ':1,3w', and there is no
    // such thing here, so the line is refused whole rather than read as ':w'.
    [InlineData(":1,3w<CR>", "Not an editor command: 1,3w")]
    [InlineData(":%q<CR>", "Not an editor command: %q")]
    // A range followed by something that is not a name, and a line that starts with one.
    [InlineData(":3 foo<CR>", "Not an editor command: 3 foo")]
    [InlineData(":!ls<CR>", "Not an editor command: !ls")]
    [InlineData(":'a<CR>", "Not an editor command: 'a")]
    public void AnUnknownExCommand_IsNamedAndNothingIsAskedFor(string keys, string message)
    {
        VimRun run = VimScript.Run("|abc", keys);

        Assert.Equal(message, run.Status);
        Assert.Empty(run.ExCommands);
        Assert.Null(run.CommandLine);
    }

    [Theory]
    [InlineData(":<CR>")]
    [InlineData(":   <CR>")]
    public void AnEmptyExLine_DoesNothingAndSaysNothing(string keys)
    {
        VimRun run = VimScript.Run("|abc", keys);

        Assert.Empty(run.ExCommands);
        Assert.Null(run.Status);
        Assert.Null(run.CommandLine);
    }

    [Fact]
    public void AfterTheLineHasRun_TheKeysAreCommandsAgain()
    {
        VimRun run = VimScript.Run("|abc", ":w<CR>x");

        Assert.Equal("|bc", run.Text);
        Assert.Single(run.ExCommands);
    }

    [Theory]
    // Every one of these is a command in normal mode, and none of them is one while a line is open.
    [InlineData(":dd<Esc>")]
    [InlineData(":3x<Esc>")]
    [InlineData("/ip<Esc>")]
    [InlineData(":rZ<Esc>")]
    [InlineData(":o<Esc>")]
    public void NothingTypedIntoTheLine_ReachesTheDocumentOrTheGrammar(string keys)
    {
        VimRun run = VimScript.Run("|abc\ndef", keys);

        Assert.Equal("abc\ndef", run.Buffer.Text);
        Assert.Equal(VimMode.Normal, run.Mode);
    }

    [Fact]
    public void InAVisualMode_TheColonIsRefusedAndTheSelectionStays()
    {
        VimRun run = VimScript.Run("a|bc", "v:");

        Assert.Null(run.CommandLine);
        Assert.Equal("Not a command: :", run.Status);
        Assert.Equal(VimMode.Visual, run.Mode);
        Assert.Equal("b", run.SelectedText);
    }

    [Theory]
    // A range on its own is a jump to its last line, at the first non-blank — every address form,
    // clamped at both ends of the buffer.
    [InlineData("|a\n  b\nc\nd", ":2<CR>", "a\n  |b\nc\nd")]
    [InlineData("|a\n  b\nc\nd", ":$<CR>", "a\n  b\nc\n|d")]
    [InlineData("|a\n  b\nc\nd", ":99<CR>", "a\n  b\nc\n|d")]
    [InlineData("a\n  b\n|c\nd", ":0<CR>", "|a\n  b\nc\nd")]
    [InlineData("a\n  b\n|c\nd", ":1<CR>", "|a\n  b\nc\nd")]
    [InlineData("|a\n  b\nc\nd", ":.+2<CR>", "a\n  b\n|c\nd")]
    [InlineData("|a\n  b\nc\nd", ":+<CR>", "a\n  |b\nc\nd")]
    [InlineData("a\n  b\nc\n|d", ":-2<CR>", "a\n  |b\nc\nd")]
    [InlineData("|a\n  b\nc\nd", ":$-1<CR>", "a\n  b\n|c\nd")]
    [InlineData("|a\n  b\nc\nd", ":%<CR>", "a\n  b\nc\n|d")]
    [InlineData("|a\n  b\nc\nd", ":2,3<CR>", "a\n  b\n|c\nd")]
    [InlineData("|a\n  b\nc\nd", ":3,2<CR>", "a\n  b\n|c\nd")]
    [InlineData("|a\n  b\nc\nd", ": 2 , 3 <CR>", "a\n  b\n|c\nd")]
    // The marks a selection leaves: the last selection ran from line 1 to line 3.
    [InlineData("|a\n  b\nc\nd", "Vjj<Esc>gg:'<<CR>", "|a\n  b\nc\nd")]
    [InlineData("|a\n  b\nc\nd", "Vjj<Esc>gg:'><CR>", "a\n  b\n|c\nd")]
    [InlineData("|a\n  b\nc\nd", "Vjj<Esc>gg:'>-1<CR>", "a\n  |b\nc\nd")]
    public void ARangeOnItsOwn_GoesToItsLastLine(string before, string keys, string after)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(after, run.Text);
        Assert.Null(run.Status);
        Assert.Equal(VimMode.Normal, run.Mode);
        Assert.Empty(run.ExCommands);
    }

    [Fact]
    public void AJumpToALine_ChangesNothingAndBringsTheCaretIntoView()
    {
        VimRun run = VimScript.Run("|a\nb\nc", ":3<CR>");

        Assert.Equal("a\nb\nc", run.Buffer.Text);
        Assert.Equal(0, run.Buffer.UndoSteps);
        Assert.True(run.Buffer.ScrollRequests > 0);
    }

    [Fact]
    public void AJumpToALine_ForgetsTheColumnDollarLeftBehind()
    {
        // '$' leaves "the end of the line" behind for j and k to follow. A jump is a place, and j
        // after it goes straight down from there.
        VimRun run = VimScript.Run("|abcdef\nabcdef\nabcdef", "$:2<CR>j");

        Assert.Equal("abcdef\nabcdef\n|abcdef", run.Text);
    }

    [Theory]
    [InlineData(":'<<CR>")]
    [InlineData(":'<,'>s/a/b/<CR>")]
    public void AMarkBeforeAnySelection_IsNotSet(string keys)
    {
        VimRun run = VimScript.Run("|abc", keys);

        Assert.Equal("Mark not set", run.Status);
        Assert.Equal("|abc", run.Text);
    }

    [Fact]
    public void InInsertMode_AColonIsJustAColon()
        => Assert.Equal(":|abc", VimScript.Run("|abc", "i:").Text);

    [Theory]
    // Outside the line both keys are motions — Enter is '+', Backspace is 'h' across a line break —
    // which is vim's reading and the one the line-motion tests hold in full.
    [InlineData("|abc\ndef", "<CR>", "abc\n|def")]
    [InlineData("a|bc", "<BS>", "|abc")]
    public void OutsideACommandLine_EnterAndBackspaceAreMotions(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Fact]
    public void OutsideACommandLine_EnterBehindAnOperatorIsTheMotionToo()
    {
        // 'd' then Enter is 'd+': the caret's line and the one below it, as whole lines.
        VimRun run = VimScript.Run("|abc\ndef\nghi", "d<CR>");

        Assert.Equal("|ghi", run.Text);
        Assert.False(run.Engine.HasPendingSequence);
    }

    [Theory]
    // In insert mode both keys are the editor's, exactly as they were before either was named.
    [InlineData("a|bc", "i<CR>", "a\n|bc")]
    [InlineData("ab|c", "i<BS>", "a|c")]
    public void InInsertMode_EnterAndBackspaceAreStillTheEditors(string before, string keys, string after)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(after, run.Text);
        Assert.Equal(VimMode.Insert, run.Mode);
    }

    [Fact]
    public void TheLineSurvivesNothing_AResetTakesItAway()
    {
        FakeVimBuffer buffer = VimScript.NewBuffer("|abc");
        VimEngine engine = new(buffer);

        engine.HandleKey(VimKey.Typed(':'));
        engine.HandleKey(VimKey.Typed('w'));

        // What the editor calls when vim is switched off or another document is swapped in.
        engine.Reset();

        Assert.Null(engine.CommandLine);
        Assert.Null(buffer.CommandLine);
        Assert.False(engine.HasPendingSequence);
    }

    [Fact]
    public void WhileALineIsOpen_ControlKeysStillBelongToTheWindow()
    {
        FakeVimBuffer buffer = VimScript.NewBuffer("|abc");
        VimEngine engine = new(buffer);

        engine.HandleKey(VimKey.Typed(':'));

        // Ctrl+S is the window's Save, and it stays the window's: the engine turns the key down and
        // leaves the half-typed line where it was.
        Assert.False(engine.HandleKey(VimKey.Typed('s', VimModifiers.Control)));
        Assert.Equal(":", engine.CommandLine);
    }

    [Fact]
    public void WhileALineIsOpen_EveryOtherKeyIsTakenRatherThanLetThrough()
    {
        FakeVimBuffer buffer = VimScript.NewBuffer("|abc");
        VimEngine engine = new(buffer);

        engine.HandleKey(VimKey.Typed(':'));

        Assert.True(engine.HandleKey(VimKey.Typed('w')));
        Assert.True(engine.HandleKey(VimKey.Named(VimSpecialKey.Left)));
        Assert.True(engine.HandleKey(VimKey.Named(VimSpecialKey.Other)));

        // The arrow and the unknown key were swallowed without changing anything: this command line
        // is typed and rubbed out, and has nowhere else for a caret to be.
        Assert.Equal(":w", engine.CommandLine);
    }
}
