using Enigma.MarkdownEditor.Core.Vim;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Vim;

/// <summary>
/// <c>:set</c>: the five spellings over the five options, what each shows, and every refusal.
/// </summary>
/// <remarks>
/// The options are read back off <see cref="VimEngine.Options"/> rather than only through their
/// effect, so a case says "this spelling writes this value"; the effect is proved separately, once
/// per option, so that the two halves of <c>:set ic</c> — the parse and the search that reads it —
/// are each pinned on their own.
/// </remarks>
public sealed class VimSetTests
{
    [Theory]
    [InlineData(":set ic<CR>", true)]
    [InlineData(":set ignorecase<CR>", true)]
    [InlineData(":set noic<CR>", false)]
    [InlineData(":set noignorecase<CR>", false)]
    [InlineData(":set ic!<CR>", true)]
    [InlineData(":set ic<CR>:set ic!<CR>", false)]
    [InlineData(":se ic<CR>", true)]
    public void ASwitch_IsSetClearedAndFlipped(string keys, bool expected)
    {
        VimRun run = VimScript.Run("|a", keys);

        Assert.Equal(expected, run.Engine.Options.IgnoreCase);
        Assert.Null(run.Status);
        Assert.Null(run.CommandLine);
    }

    [Theory]
    [InlineData(":set scs<CR>")]
    [InlineData(":set smartcase<CR>")]
    public void SmartCase_IsASwitch(string keys)
        => Assert.True(VimScript.Run("|a", keys).Engine.Options.SmartCase);

    [Theory]
    [InlineData(":set nows<CR>")]
    [InlineData(":set nowrapscan<CR>")]
    public void WrapScan_IsASwitch(string keys)
        => Assert.False(VimScript.Run("|a", keys).Engine.Options.WrapScan);

    [Theory]
    [InlineData(":set noet<CR>")]
    [InlineData(":set noexpandtab<CR>")]
    public void ExpandTab_IsASwitch(string keys)
        => Assert.False(VimScript.Run("|a", keys).Engine.Options.ExpandTab);

    [Theory]
    [InlineData(":set sw=8<CR>", 8)]
    [InlineData(":set shiftwidth=2<CR>", 2)]
    public void ANumber_IsSetWithEquals(string keys, int expected)
    {
        VimRun run = VimScript.Run("|a", keys);

        Assert.Equal(expected, run.Engine.Options.ShiftWidth);
        Assert.Null(run.Status);
    }

    [Theory]
    [InlineData(":set ic?<CR>", "noignorecase")]
    [InlineData(":set ic<CR>:set ic?<CR>", "ignorecase")]
    [InlineData(":set ignorecase?<CR>", "noignorecase")]
    [InlineData(":set ws?<CR>", "wrapscan")]
    [InlineData(":set sw?<CR>", "shiftwidth=4")]
    // A bare number option shows its value rather than setting anything.
    [InlineData(":set sw<CR>", "shiftwidth=4")]
    [InlineData(":set sw=8<CR>:set sw<CR>", "shiftwidth=8")]
    // Several at once, and everything at once.
    [InlineData(":set ic? ws?<CR>", "noignorecase wrapscan")]
    [InlineData(":set<CR>", "noignorecase nosmartcase wrapscan expandtab shiftwidth=4")]
    public void AQuestionMark_ShowsTheValueUnderItsLongName(string keys, string shown)
        => Assert.Equal(shown, VimScript.Run("|a", keys).Status);

    [Fact]
    public void SeveralArguments_AreAllApplied()
    {
        VimRun run = VimScript.Run("|a", ":set ic nows sw=2<CR>");

        Assert.True(run.Engine.Options.IgnoreCase);
        Assert.False(run.Engine.Options.WrapScan);
        Assert.Equal(2, run.Engine.Options.ShiftWidth);
    }

    [Theory]
    [InlineData(":set sw=abc<CR>", "Invalid argument: sw=abc")]
    [InlineData(":set sw=<CR>", "Invalid argument: sw=")]
    [InlineData(":set sw=0<CR>", "Invalid argument: sw=0")]
    [InlineData(":set sw=-4<CR>", "Invalid argument: sw=-4")]
    [InlineData(":set sw!<CR>", "Invalid argument: sw!")]
    [InlineData(":set nosw<CR>", "Invalid argument: nosw")]
    [InlineData(":set ic=1<CR>", "Invalid argument: ic=1")]
    [InlineData(":set noic?<CR>", "Invalid argument: noic?")]
    [InlineData(":set noic!<CR>", "Invalid argument: noic!")]
    public void ABadValue_IsAnInvalidArgument(string keys, string message)
    {
        VimRun run = VimScript.Run("|a", keys);

        Assert.Equal(message, run.Status);
        Assert.Equal(4, run.Engine.Options.ShiftWidth);
        Assert.False(run.Engine.Options.IgnoreCase);
    }

    [Theory]
    [InlineData(":set foo<CR>", "Unknown option: foo")]
    [InlineData(":set nofoo<CR>", "Unknown option: nofoo")]
    [InlineData(":set hlsearch<CR>", "Unknown option: hlsearch")]
    [InlineData(":set foo=3<CR>", "Unknown option: foo=3")]
    public void AnUnknownName_IsSaidToBe(string keys, string message)
        => Assert.Equal(message, VimScript.Run("|a", keys).Status);

    [Fact]
    public void ARefusal_StopsAtTheFirstBadArgument()
    {
        // 'ic' before the unknown name was applied; 'nows' after it was not.
        VimRun run = VimScript.Run("|a", ":set ic foo nows<CR>");

        Assert.Equal("Unknown option: foo", run.Status);
        Assert.True(run.Engine.Options.IgnoreCase);
        Assert.True(run.Engine.Options.WrapScan);
    }

    [Fact]
    public void ARangeOnSet_IsRefused()
    {
        VimRun run = VimScript.Run("|a", ":3set ic<CR>");

        Assert.Equal("No range allowed", run.Status);
        Assert.False(run.Engine.Options.IgnoreCase);
    }

    [Fact]
    public void IgnoreCase_ChangesWhatTheNextSearchFinds()
        => Assert.Equal("x |foo", VimScript.Run("|x foo", ":set ic<CR>/FOO<CR>").Text);

    [Fact]
    public void WrapScan_ChangesWhereTheNextSearchStops()
    {
        VimRun run = VimScript.Run("|foo bar foo", ":set nows<CR>/foo<CR>n");

        Assert.Equal("foo bar |foo", run.Text);
        Assert.Equal("search hit BOTTOM without match for: foo", run.Status);
    }

    [Fact]
    public void ShiftWidthAndExpandTab_ChangeWhatTheNextShiftWrites()
    {
        Assert.Equal("  |a", VimScript.Run("|a", ":set sw=2<CR>>>").Text);
        Assert.Equal("\t|a", VimScript.Run("|a", ":set noet<CR>>>").Text);
    }

    [Fact]
    public void ASet_ChangesNothingInTheDocument()
    {
        VimRun run = VimScript.Run("|abc", ":set ic<CR>");

        Assert.Equal("abc", run.Buffer.Text);
        Assert.Equal(0, run.Buffer.UndoSteps);
        Assert.Empty(run.ExCommands);
    }

    [Fact]
    public void ASet_IsNotWhatDotRepeats()
        => Assert.Equal("|c", VimScript.Run("|abc", "x:set ic<CR>.").Text);
}
