using Enigma.MarkdownEditor.Core.Vim;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Vim;

/// <summary>
/// The registers: the named ones a <c>"</c> in front of a command picks, the yank register <c>0</c>,
/// the black hole <c>_</c>, and the append a capital does.
/// </summary>
/// <remarks>
/// The prefix is the whole grammar addition, and it reads the same before or after the count and on
/// either grammar. What each register then does with what it is handed is vim's, tested one rule at
/// a time — the unnamed register still following every fill, <c>0</c> surviving a delete, <c>_</c>
/// keeping nothing, a capital adding rather than replacing.
/// </remarks>
public sealed class VimRegisterTests
{
    [Fact]
    public void ANamedYank_FillsThatRegister_AndPuttingItBackReadsIt()
    {
        VimRun run = VimScript.Run("|foo bar", "\"ayiwo<Esc>\"ap");

        // 'foo' yanked into a, a new line opened, and "ap put it there — the caret on the last
        // character a characterwise put lands on.
        Assert.Equal("foo", run.RegisterNamed('a')?.Text);
        Assert.Equal("foo bar\nfo|o", run.Text);
    }

    [Fact]
    public void ANamedRegister_MayBePrefixedBeforeOrAfterTheCount()
    {
        VimRun before = VimScript.Run("|a\nb\nc\nd", "\"a2yy");
        VimRun after = VimScript.Run("|a\nb\nc\nd", "2\"ayy");

        Assert.Equal("a\nb", before.RegisterNamed('a')?.Text);
        Assert.Equal("a\nb", after.RegisterNamed('a')?.Text);
    }

    [Fact]
    public void ANamedDelete_FillsBothTheNamedAndTheUnnamedRegister()
    {
        VimRun run = VimScript.Run("|foo bar", "\"adw");

        Assert.Equal("foo ", run.RegisterNamed('a')?.Text);
        Assert.Equal("foo ", run.Register?.Text);
    }

    [Fact]
    public void ACapital_AppendsToTheLowercaseRegister()
    {
        // 'a' takes the first word, 'A' adds the second onto it — characterwise onto characterwise
        // stays characterwise, so the two run together.
        VimRun run = VimScript.Run("|foo bar", "\"ayiww\"Ayiw");

        Assert.Equal("foobar", run.RegisterNamed('a')?.Text);
    }

    [Fact]
    public void ACapitalOntoLines_KeepsItLinewise_AndAddsALine()
    {
        VimRun run = VimScript.Run("|a\nb\nc", "\"ayyj\"Ayy");

        VimRegister? register = run.RegisterNamed('a');

        Assert.Equal("a\nb", register?.Text);
        Assert.Equal(VimRegisterKind.Linewise, register?.Kind);
    }

    [Fact]
    public void TheYankRegister_HoldsTheLastYank_WhateverDeletesHappenSince()
    {
        // yy fills 0 and the unnamed register; the delete that follows fills the unnamed one again but
        // leaves 0 with the yank, which is the whole reason 0 exists.
        VimRun run = VimScript.Run("|keep\ndrop", "yyjdd\"0p");

        Assert.Equal("keep", run.RegisterNamed('0')?.Text);
        Assert.Equal("keep\n|keep", run.Text);
    }

    [Fact]
    public void TheYankRegister_IsNotFilledByADelete()
    {
        VimRun run = VimScript.Run("|abc", "dd");

        Assert.Null(run.RegisterNamed('0'));
    }

    [Fact]
    public void TheBlackHole_TakesTheTextWithoutTouchingAnyRegister()
    {
        // 'keep' is yanked, then a black-hole delete removes a line without disturbing the unnamed
        // register, so p still puts 'keep'.
        VimRun run = VimScript.Run("|keep\ndrop", "yyj\"_ddP");

        // P still puts the yanked line, linewise, above — proof the black-hole dd left it there.
        Assert.Equal("keep", run.Register?.Text);
        Assert.Equal("|keep\nkeep", run.Text);
    }

    [Fact]
    public void APrefixWithNothingAfterIt_IsCancelledByEscape()
    {
        VimRun run = VimScript.Run("a|bc", "\"a<Esc>");

        Assert.False(run.Engine.HasPendingSequence);
        Assert.Equal("a|bc", run.Text);
    }

    [Fact]
    public void APrefixOnAKeyThatTakesNoRegister_IsNotACommand()
    {
        VimRun run = VimScript.Run("a|bc", "\"aj");

        Assert.Equal("Not a command: \"aj", run.Status);
    }

    [Fact]
    public void ANamedPut_ReadsTheNamedRegister()
    {
        VimRun run = VimScript.Run("|foo bar", "\"ayiwwviw\"ap");

        // 'foo' into a; then the second word selected and replaced with a's contents, the caret on
        // the last character the inline put lands on.
        Assert.Equal("foo fo|o", run.Text);
    }

    [Fact]
    public void AVisualNamedDelete_FillsTheNamedRegister()
    {
        // The prefix in a visual mode sits before the operator, as vim's does: viw selects, "ad
        // deletes into a.
        VimRun run = VimScript.Run("|foo bar", "viw\"ad");

        Assert.Equal("foo", run.RegisterNamed('a')?.Text);
    }

    [Fact]
    public void TheUnnamedRegister_IsWhatPPutsWithoutAName()
    {
        // A named yank fills the unnamed register too, so a plain p still works.
        VimRun run = VimScript.Run("|foo bar", "\"ayiwwP");

        Assert.Equal("foo fo|obar", run.Text);
    }

    [Fact]
    public void ANamedRegister_SurvivesAReset()
    {
        FakeVimBuffer buffer = VimScript.NewBuffer("|foo bar");
        VimEngine engine = new(buffer);

        foreach (VimKey key in VimKeyNotation.Parse("\"ayiw"))
        {
            engine.HandleKey(key);
        }

        engine.Reset();

        Assert.Equal("foo", engine.Registers.Get('a')?.Text);
    }
}
