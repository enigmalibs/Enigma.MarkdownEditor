using Enigma.MarkdownEditor.Core.Vim;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Vim;

/// <summary>
/// <c>u</c> and <c>Ctrl+R</c>, and what one step costs.
/// </summary>
/// <remarks>
/// <para>
/// The engine keeps no history of its own — it asks the buffer, which is the editor's own undo stack,
/// so that <c>u</c> and <c>Ctrl+Z</c> take back the same step. What it does decide is how much goes
/// into a step, and it decides it in one place: every command that changes the buffer does it in
/// exactly one write, and the only thing that opens a group is an insert session — including the one
/// a change opens on its own behalf.
/// </para>
/// <para>
/// That last part is the acceptance criterion. <c>cc</c>, the sentence typed after it and the
/// <c>Esc</c> that ends it are one <c>u</c>, not three.
/// </para>
/// </remarks>
public sealed class VimUndoTests
{
    [Theory]
    [InlineData("a|bc", "xu", "a|bc")]
    [InlineData("a|bc\ndef", "ddu", "a|bc\ndef")]
    [InlineData("|foo bar", "dwu", "|foo bar")]
    [InlineData("a|bc", "rXu", "a|bc")]
    [InlineData("|abc", "3~u", "|abc")]
    [InlineData("a|bc", "iXY<Esc>u", "a|bc")]
    [InlineData("a|bc", "oXY<Esc>u", "a|bc")]
    public void U_TakesBackTheLastCommandWhole(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("abc\nd|ef\nghi", "ccXYZ<Esc>u", "abc\nd|ef\nghi")]
    [InlineData("|foo bar", "cwqux<Esc>u", "|foo bar")]
    [InlineData("ab|cdef", "CXY<Esc>u", "ab|cdef")]
    public void AChangeAndWhatWasTypedInItsPlace_ComeBackInExactlyOneStep(
        string before,
        string keys,
        string after)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(after, run.Text);

        // Nothing left on the stack: the one step that was there has just been taken.
        Assert.Equal(0, run.Buffer.UndoSteps);
    }

    [Fact]
    public void ACommandThatChangesNothing_CostsNoStep()
        => Assert.Equal(0, VimScript.Run("a|bc\ndef", "yyGgg$0w").Buffer.UndoSteps);

    [Theory]
    [InlineData("|abcde", "xx", 2)]
    [InlineData("|abcde", "3x", 1)]
    [InlineData("|abcde", "xiQ<Esc>x", 3)]
    [InlineData("a|bc\ndef", "yy5p", 1)]
    public void EachCommandThatChangesSomething_IsOneStep(string before, string keys, int steps)
        => Assert.Equal(steps, VimScript.Run(before, keys).Buffer.UndoSteps);

    [Theory]
    [InlineData("|abcde", "xxu", "|bcde")]
    [InlineData("|abcde", "xx2u", "|abcde")]
    [InlineData("|abcde", "xx9u", "|abcde")]
    public void ACountOnU_TakesBackThatManySteps(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("a|bc", "xu<C-r>", "a|c")]
    [InlineData("|abcde", "xxuu<C-r><C-r>", "|cde")]
    [InlineData("abc\nd|ef\nghi", "ccXYZ<Esc>u<C-r>", "abc\nXY|Z\nghi")]
    public void CtrlR_PutsBackWhatUTook(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Fact]
    public void UWithNothingToTakeBack_DoesNothing()
    {
        VimRun run = VimScript.Run("a|bc", "u");

        Assert.Equal("a|bc", run.Text);
        Assert.Equal(0, run.Buffer.UndoSteps);
    }

    [Fact]
    public void CtrlRWithNothingToPutBack_DoesNothing()
        => Assert.Equal("a|bc", VimScript.Run("a|bc", "<C-r>").Text);

    [Fact]
    public void CtrlR_AndTheSixScrollKeys_AreTheOnlyHeldKeysTheEngineClaims()
    {
        // Every other Control combination belongs to the window: the editor has to be free to see
        // Ctrl+S and Ctrl+W, or vim would have taken the application's own shortcuts away.
        VimRun run = VimScript.Run("a|bc", "x");

        Assert.True(run.Engine.HandleKey(VimKeyControl('r')));
        Assert.True(run.Engine.HandleKey(VimKeyControl('d')));
        Assert.True(run.Engine.HandleKey(VimKeyControl('u')));
        Assert.False(run.Engine.HandleKey(VimKeyControl('s')));
        Assert.False(run.Engine.HandleKey(VimKeyControl('w')));
        Assert.False(run.Engine.HandleKey(VimKeyControl('k')));
    }

    [Fact]
    public void CtrlR_IsNotClaimedInInsertMode()
    {
        VimRun run = VimScript.Run("a|bc", "i");

        Assert.False(run.Engine.HandleKey(VimKeyControl('r')));
    }

    private static VimKey VimKeyControl(char character)
        => VimKey.Typed(character, VimModifiers.Control);
}
