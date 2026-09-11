using Enigma.MarkdownEditor.Core.Vim;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Vim;

/// <summary>
/// The grammar on its own: which keys spell which command, which one is only half of one, and which
/// can never become one.
/// </summary>
/// <remarks>
/// Kept apart from the engine's tests on purpose. What a sequence means is a question with no
/// document in it, and being able to put the parser into its interesting states — a <c>g</c> or a
/// <c>Z</c> waiting for its partner, an operator waiting for its motion — without a buffer is what
/// keeps that answer cheap to check.
/// </remarks>
public sealed class KeySequenceParserTests
{
    [Theory]
    [InlineData('h', VimMotion.Left)]
    [InlineData('l', VimMotion.Right)]
    [InlineData('k', VimMotion.Up)]
    [InlineData('j', VimMotion.Down)]
    [InlineData('0', VimMotion.LineStart)]
    [InlineData('$', VimMotion.LineEnd)]
    [InlineData('w', VimMotion.WordForward)]
    [InlineData('b', VimMotion.WordBackward)]
    [InlineData('e', VimMotion.WordEnd)]
    [InlineData('W', VimMotion.WordForwardBig)]
    [InlineData('B', VimMotion.WordBackwardBig)]
    [InlineData('E', VimMotion.WordEndBig)]
    [InlineData('^', VimMotion.FirstNonBlank)]
    [InlineData('%', VimMotion.MatchingBracket)]
    [InlineData('}', VimMotion.ParagraphForward)]
    [InlineData('{', VimMotion.ParagraphBackward)]
    [InlineData(')', VimMotion.SentenceForward)]
    [InlineData('(', VimMotion.SentenceBackward)]
    [InlineData('+', VimMotion.LineDownFirstNonBlank)]
    [InlineData('-', VimMotion.LineUpFirstNonBlank)]
    [InlineData('_', VimMotion.CurrentLineFirstNonBlank)]
    [InlineData('|', VimMotion.Column)]
    [InlineData(' ', VimMotion.RightWrapping)]
    [InlineData('G', VimMotion.LastLine)]
    // The four searches that are complete as typed: what they look for is the engine's memory or the
    // word under the caret, so the grammar has nothing to wait for.
    [InlineData('n', VimMotion.SearchNext)]
    [InlineData('N', VimMotion.SearchPrevious)]
    [InlineData('*', VimMotion.SearchWordForward)]
    [InlineData('#', VimMotion.SearchWordBackward)]
    // And the three that name a line by where the screen is.
    [InlineData('H', VimMotion.ScreenTop)]
    [InlineData('M', VimMotion.ScreenMiddle)]
    [InlineData('L', VimMotion.ScreenBottom)]
    public void AMotionKey_IsAMoveCommand(char character, VimMotion motion)
    {
        VimParseResult result = new KeySequenceParser().Feed(VimKey.Typed(character));

        Assert.Equal(VimParseStatus.Complete, result.Status);
        Assert.Equal(VimCommand.Move(motion), result.Command);
    }

    [Theory]
    [InlineData(VimSpecialKey.Left, VimMotion.Left)]
    [InlineData(VimSpecialKey.Right, VimMotion.Right)]
    [InlineData(VimSpecialKey.Up, VimMotion.Up)]
    [InlineData(VimSpecialKey.Down, VimMotion.Down)]
    [InlineData(VimSpecialKey.Home, VimMotion.LineStart)]
    [InlineData(VimSpecialKey.End, VimMotion.LineEnd)]
    // Outside the command line, which never lets these two reach the parser: Enter is '+', and
    // Backspace is the 'h' that crosses a line break.
    [InlineData(VimSpecialKey.Enter, VimMotion.LineDownFirstNonBlank)]
    [InlineData(VimSpecialKey.Backspace, VimMotion.LeftWrapping)]
    public void AnArrowKey_IsTheSameMoveAsItsLetter(VimSpecialKey key, VimMotion motion)
    {
        VimParseResult result = new KeySequenceParser().Feed(VimKey.Named(key));

        Assert.Equal(VimParseStatus.Complete, result.Status);
        Assert.Equal(VimCommand.Move(motion), result.Command);
    }

    [Theory]
    [InlineData('i', VimAction.InsertBeforeCaret)]
    [InlineData('I', VimAction.InsertAtLineStart)]
    [InlineData('a', VimAction.InsertAfterCaret)]
    [InlineData('A', VimAction.InsertAtLineEnd)]
    [InlineData('o', VimAction.OpenLineBelow)]
    [InlineData('O', VimAction.OpenLineAbove)]
    [InlineData('x', VimAction.DeleteCharacter)]
    [InlineData('J', VimAction.Join)]
    public void AnActionKey_IsThatAction(char character, VimAction action)
    {
        VimParseResult result = new KeySequenceParser().Feed(VimKey.Typed(character));

        Assert.Equal(VimParseStatus.Complete, result.Status);
        Assert.Equal(new VimCommand(action), result.Command);
    }

    [Fact]
    public void ALoneG_IsPendingAndSaysWhatHasBeenTyped()
    {
        KeySequenceParser parser = new();

        VimParseResult result = parser.Feed(VimKey.Typed('g'));

        Assert.Equal(VimParseStatus.Pending, result.Status);
        Assert.True(parser.HasPendingSequence);
        Assert.Equal("g", parser.PendingSequence);
    }

    [Fact]
    public void ASecondG_CompletesTheJumpToTheTop()
    {
        KeySequenceParser parser = new();
        parser.Feed(VimKey.Typed('g'));

        VimParseResult result = parser.Feed(VimKey.Typed('g'));

        Assert.Equal(VimParseStatus.Complete, result.Status);
        Assert.Equal(VimCommand.Move(VimMotion.FirstLine), result.Command);
        Assert.False(parser.HasPendingSequence);
    }

    [Theory]
    // 'Q' is vim's Ex mode and 'K' its keyword lookup, neither of which is here; '&' repeats a
    // substitute, which is out of scope. ('z' used to be on this list, and is a prefix now.)
    [InlineData('Q', "Q")]
    [InlineData('K', "K")]
    [InlineData('&', "&")]
    public void AKeyThatSpellsNothing_IsInvalidAndNamed(char character, string sequence)
    {
        VimParseResult result = new KeySequenceParser().Feed(VimKey.Typed(character));

        Assert.Equal(VimParseStatus.Invalid, result.Status);
        Assert.Equal(sequence, result.Sequence);
    }

    [Fact]
    public void GFollowedByAnythingElse_IsInvalidAndNamedInFull()
    {
        KeySequenceParser parser = new();
        parser.Feed(VimKey.Typed('g'));

        VimParseResult result = parser.Feed(VimKey.Typed('x'));

        Assert.Equal(VimParseStatus.Invalid, result.Status);
        Assert.Equal("gx", result.Sequence);
        Assert.False(parser.HasPendingSequence);
    }

    [Fact]
    public void AKeyAfterADeadSequence_StartsAgainFromNothing()
    {
        KeySequenceParser parser = new();
        parser.Feed(VimKey.Typed('g'));
        parser.Feed(VimKey.Typed('x'));

        VimParseResult result = parser.Feed(VimKey.Typed('h'));

        Assert.Equal(VimParseStatus.Complete, result.Status);
        Assert.Equal("h", result.Sequence);
    }

    [Fact]
    public void Reset_ThrowsAwayWhatWasHalfTyped()
    {
        KeySequenceParser parser = new();
        parser.Feed(VimKey.Typed('g'));

        parser.Reset();

        Assert.False(parser.HasPendingSequence);
        Assert.Equal(string.Empty, parser.PendingSequence);

        // And the g is gone rather than waiting: this one is a jump to the top only if it was kept.
        Assert.Equal(VimParseStatus.Pending, parser.Feed(VimKey.Typed('g')).Status);
    }

    [Fact]
    public void ACompletedCommand_LeavesNothingBehindForTheNextOne()
    {
        KeySequenceParser parser = new();
        parser.Feed(VimKey.Typed('h'));

        Assert.Equal("l", parser.Feed(VimKey.Typed('l')).Sequence);
    }

    [Fact]
    public void ANamedKeyWithNoMeaning_IsInvalid()
        => Assert.Equal(
            VimParseStatus.Invalid,
            new KeySequenceParser().Feed(VimKey.Named(VimSpecialKey.Other)).Status);

    [Theory]
    [InlineData("dw", VimOperator.Delete, VimMotion.WordForward)]
    [InlineData("yj", VimOperator.Yank, VimMotion.Down)]
    [InlineData("ce", VimOperator.Change, VimMotion.WordEnd)]
    [InlineData("d$", VimOperator.Delete, VimMotion.LineEnd)]
    [InlineData("y0", VimOperator.Yank, VimMotion.LineStart)]
    [InlineData("dG", VimOperator.Delete, VimMotion.LastLine)]
    [InlineData("dgg", VimOperator.Delete, VimMotion.FirstLine)]
    // An operator typed twice is that operator over its own line.
    [InlineData("dd", VimOperator.Delete, VimMotion.WholeLine)]
    [InlineData("yy", VimOperator.Yank, VimMotion.WholeLine)]
    [InlineData("cc", VimOperator.Change, VimMotion.WholeLine)]
    // The three capitals are spelled as what they are, and so are the two substitutes and 'X'.
    [InlineData("D", VimOperator.Delete, VimMotion.LineEnd)]
    [InlineData("C", VimOperator.Change, VimMotion.LineEnd)]
    [InlineData("Y", VimOperator.Yank, VimMotion.WholeLine)]
    [InlineData("s", VimOperator.Change, VimMotion.Right)]
    [InlineData("S", VimOperator.Change, VimMotion.WholeLine)]
    [InlineData("X", VimOperator.Delete, VimMotion.Left)]
    // The shift operators, over a motion and doubled.
    [InlineData(">w", VimOperator.ShiftRight, VimMotion.WordForward)]
    [InlineData("<j", VimOperator.ShiftLeft, VimMotion.Down)]
    [InlineData(">>", VimOperator.ShiftRight, VimMotion.WholeLine)]
    [InlineData("<<", VimOperator.ShiftLeft, VimMotion.WholeLine)]
    [InlineData(">}", VimOperator.ShiftRight, VimMotion.ParagraphForward)]
    // The case operators come out of the 'g' prefix, and double with either spelling.
    [InlineData("guw", VimOperator.Lowercase, VimMotion.WordForward)]
    [InlineData("gU$", VimOperator.Uppercase, VimMotion.LineEnd)]
    [InlineData("g~e", VimOperator.ToggleCase, VimMotion.WordEnd)]
    [InlineData("guu", VimOperator.Lowercase, VimMotion.WholeLine)]
    [InlineData("gugu", VimOperator.Lowercase, VimMotion.WholeLine)]
    [InlineData("gUU", VimOperator.Uppercase, VimMotion.WholeLine)]
    [InlineData("gUgU", VimOperator.Uppercase, VimMotion.WholeLine)]
    [InlineData("g~~", VimOperator.ToggleCase, VimMotion.WholeLine)]
    [InlineData("g~g~", VimOperator.ToggleCase, VimMotion.WholeLine)]
    // And the new motions behind the old operators.
    [InlineData("dW", VimOperator.Delete, VimMotion.WordForwardBig)]
    [InlineData("d}", VimOperator.Delete, VimMotion.ParagraphForward)]
    [InlineData("y%", VimOperator.Yank, VimMotion.MatchingBracket)]
    [InlineData("c)", VimOperator.Change, VimMotion.SentenceForward)]
    [InlineData("d_", VimOperator.Delete, VimMotion.CurrentLineFirstNonBlank)]
    // And the searches: 'dn' deletes to the next match, 'd*' to the next occurrence of the word.
    [InlineData("dn", VimOperator.Delete, VimMotion.SearchNext)]
    [InlineData("yN", VimOperator.Yank, VimMotion.SearchPrevious)]
    [InlineData("d*", VimOperator.Delete, VimMotion.SearchWordForward)]
    [InlineData("c#", VimOperator.Change, VimMotion.SearchWordBackward)]
    // And the screen motions: 'dL' deletes to the bottom of the screen.
    [InlineData("dL", VimOperator.Delete, VimMotion.ScreenBottom)]
    [InlineData("yH", VimOperator.Yank, VimMotion.ScreenTop)]
    [InlineData("cM", VimOperator.Change, VimMotion.ScreenMiddle)]
    public void AnOperatorAndAMotion_IsOneCommand(string keys, VimOperator @operator, VimMotion motion)
    {
        VimParseResult result = Feed(keys, out KeySequenceParser parser);

        Assert.Equal(VimParseStatus.Complete, result.Status);
        Assert.Equal(VimCommand.Operate(@operator, motion), result.Command);
        Assert.False(parser.HasPendingSequence);
    }

    [Theory]
    [InlineData("3j", 3)]
    [InlineData("12l", 12)]
    // The two counts of the grammar multiply, which is vim's own rule: '2d3w' is 'd6w'.
    [InlineData("2d3w", 6)]
    [InlineData("3dd", 3)]
    [InlineData("2d", 0)]
    [InlineData("3>>", 3)]
    [InlineData("2gU3U", 6)]
    [InlineData("4J", 4)]
    [InlineData("3s", 3)]
    [InlineData("3|", 3)]
    public void TheCountsAroundAnOperator_MultiplyIntoOne(string keys, int count)
    {
        VimParseResult result = Feed(keys, out _);

        Assert.Equal(count, result.Command.Count);
    }

    [Theory]
    [InlineData("x", VimAction.DeleteCharacter)]
    [InlineData("p", VimAction.PutAfter)]
    [InlineData("P", VimAction.PutBefore)]
    [InlineData("~", VimAction.ToggleCase)]
    [InlineData("u", VimAction.Undo)]
    public void TheCommandsWithNoMotion_AreThatActionAlone(string keys, VimAction action)
        => Assert.Equal(new VimCommand(action), Feed(keys, out _).Command);

    [Theory]
    [InlineData(':')]
    [InlineData('/')]
    [InlineData('?')]
    public void TheCommandLineKeys_CarryThemselvesAsTheArgument(char character)
    {
        VimParseResult result = new KeySequenceParser().Feed(VimKey.Typed(character));

        Assert.Equal(VimParseStatus.Complete, result.Status);
        Assert.Equal(
            new VimCommand(VimAction.BeginCommandLine, VimMotion.None, 0, VimOperator.None, character),
            result.Command);
    }

    [Theory]
    [InlineData("3n", VimMotion.SearchNext, 3)]
    [InlineData("12N", VimMotion.SearchPrevious, 12)]
    [InlineData("2*", VimMotion.SearchWordForward, 2)]
    public void ACountOnTheSearchRepeats_ReachesTheMotion(string keys, VimMotion motion, int count)
        => Assert.Equal(VimCommand.Move(motion, count), Feed(keys, out _).Command);

    [Theory]
    // The two search openers behind an operator: the operator and the count ride on the command that
    // opens the line, because the motion cannot complete until Enter, and the engine finishes it then.
    [InlineData("d/", VimOperator.Delete, '/', 0, '\0')]
    [InlineData("c?", VimOperator.Change, '?', 0, '\0')]
    [InlineData("2d3/", VimOperator.Delete, '/', 6, '\0')]
    [InlineData("\"ay/", VimOperator.Yank, '/', 0, 'a')]
    [InlineData("\"a2d/", VimOperator.Delete, '/', 2, 'a')]
    public void ASearchOpenerBehindAnOperator_CarriesTheOperatorOntoTheCommandLine(
        string keys,
        VimOperator @operator,
        char opener,
        int count,
        char register)
    {
        VimParseResult result = Feed(keys, out KeySequenceParser parser);

        Assert.Equal(VimParseStatus.Complete, result.Status);
        Assert.Equal(
            new VimCommand(VimAction.BeginCommandLine, VimMotion.None, count, @operator, opener, VimTextObject.None, register),
            result.Command);
        Assert.False(parser.HasPendingSequence);
    }

    [Fact]
    public void ARegisterPrefixOnASearchOpenerWithNoOperator_IsInvalid()
    {
        // '"a/foo' would name a register nothing reads: a search on its own takes none, exactly as a
        // bare motion takes none.
        VimParseResult result = Feed("\"a/", out _);

        Assert.Equal(VimParseStatus.Invalid, result.Status);
        Assert.Equal("\"a/", result.Sequence);
    }

    [Fact]
    public void ALoneOperator_IsPendingAndSaysWhatHasBeenTyped()
    {
        VimParseResult result = Feed("3d", out KeySequenceParser parser);

        Assert.Equal(VimParseStatus.Pending, result.Status);
        Assert.True(parser.HasPendingSequence);
        Assert.Equal("3d", parser.PendingSequence);
    }

    [Fact]
    public void ALoneCount_IsPending()
    {
        VimParseResult result = Feed("12", out KeySequenceParser parser);

        Assert.Equal(VimParseStatus.Pending, result.Status);
        Assert.True(parser.HasPendingSequence);
    }

    [Theory]
    // An operator over a different one, or over anything that is not a motion at all.
    [InlineData("dy", "dy")]
    [InlineData("dc", "dc")]
    [InlineData("d>", "d>")]
    // The literal '<' is named the way vim writes it, <lt> — the same notation a macro register and
    // the harness use — because '<' is where a key name would otherwise begin.
    [InlineData("><", "><lt>")]
    [InlineData("gUu", "gUu")]
    [InlineData("dgu", "dgu")]
    [InlineData("dgJ", "dgJ")]
    [InlineData("dJ", "dJ")]
    [InlineData("dZ", "dZ")]
    // 'di' opens a text object rather than dying, so what dies is the key that names none.
    [InlineData("diz", "diz")]
    [InlineData("dx", "dx")]
    [InlineData("2d3z", "2d3z")]
    [InlineData("dgx", "dgx")]
    // A scroll is not a motion: neither 'z' nor a held key nor a page key means anything behind an
    // operator, and each dies on the spot rather than waiting for more.
    [InlineData("dz", "dz")]
    [InlineData("d<C-d>", "d<C-d>")]
    [InlineData("d<PageDown>", "d<PageDown>")]
    public void AnOperatorOverSomethingThatIsNotAMotion_IsInvalidAndNamedInFull(string keys, string sequence)
    {
        VimParseResult result = Feed(keys, out KeySequenceParser parser);

        Assert.Equal(VimParseStatus.Invalid, result.Status);
        Assert.Equal(sequence, result.Sequence);
        Assert.False(parser.HasPendingSequence);
    }

    [Theory]
    [InlineData('X')]
    [InlineData('d')]
    [InlineData('3')]
    [InlineData('~')]
    public void RTakesTheNextKeyLiterally_WhateverElseItWouldHaveSpelled(char character)
    {
        KeySequenceParser parser = new();

        Assert.Equal(VimParseStatus.Pending, parser.Feed(VimKey.Typed('r')).Status);
        Assert.True(parser.HasPendingSequence);

        VimParseResult result = parser.Feed(VimKey.Typed(character));

        Assert.Equal(VimParseStatus.Complete, result.Status);
        Assert.Equal(new VimCommand(VimAction.ReplaceCharacter, VimMotion.None, 0, VimOperator.None, character), result.Command);
    }

    [Fact]
    public void RWithACount_KeepsIt()
        => Assert.Equal(
            new VimCommand(VimAction.ReplaceCharacter, VimMotion.None, 3, VimOperator.None, 'X'),
            Feed("3rX", out _).Command);

    [Fact]
    public void RFollowedByAKeyThatTypesNothing_IsInvalid()
    {
        KeySequenceParser parser = new();
        parser.Feed(VimKey.Typed('r'));

        Assert.Equal(VimParseStatus.Invalid, parser.Feed(VimKey.Named(VimSpecialKey.Other)).Status);
        Assert.False(parser.HasPendingSequence);
    }

    [Theory]
    [InlineData("gu")]
    [InlineData("gU")]
    [InlineData("g~")]
    [InlineData(">")]
    [InlineData("Z")]
    [InlineData("z")]
    [InlineData("3z")]
    public void ACaseOperatorAShiftOrAZ_IsPendingAndSaysWhatHasBeenTyped(string keys)
    {
        VimParseResult result = Feed(keys, out KeySequenceParser parser);

        Assert.Equal(VimParseStatus.Pending, result.Status);
        Assert.True(parser.HasPendingSequence);
        Assert.Equal(keys, parser.PendingSequence);
    }

    [Fact]
    public void AShiftLeft_IsPendingAndNamesItselfInVimsNotation()
    {
        VimParseResult result = Feed("<", out KeySequenceParser parser);

        Assert.Equal(VimParseStatus.Pending, result.Status);
        Assert.True(parser.HasPendingSequence);

        // The one key whose display differs from what was typed: '<' is where vim's notation would
        // otherwise start a key name, so a half-typed shift-left reads '<lt>'.
        Assert.Equal("<lt>", parser.PendingSequence);
    }

    [Theory]
    [InlineData("gJ", VimAction.JoinRaw)]
    [InlineData("ZZ", VimAction.WriteQuit)]
    [InlineData("ZQ", VimAction.QuitDiscard)]
    // The six 'z' commands: three that leave the caret, three that take it to the first non-blank.
    [InlineData("zt", VimAction.ScrollCaretTop)]
    [InlineData("zz", VimAction.ScrollCaretCenter)]
    [InlineData("zb", VimAction.ScrollCaretBottom)]
    [InlineData("z<CR>", VimAction.ScrollCaretTopToFirstNonBlank)]
    [InlineData("z.", VimAction.ScrollCaretCenterToFirstNonBlank)]
    [InlineData("z-", VimAction.ScrollCaretBottomToFirstNonBlank)]
    public void TheTwoLetterCommands_AreThatAction(string keys, VimAction action)
    {
        VimParseResult result = Feed(keys, out KeySequenceParser parser);

        Assert.Equal(VimParseStatus.Complete, result.Status);
        Assert.Equal(new VimCommand(action), result.Command);
        Assert.False(parser.HasPendingSequence);
    }

    [Theory]
    [InlineData("Zx", "Zx")]
    [InlineData("ZG", "ZG")]
    [InlineData("Zz", "Zz")]
    [InlineData("zx", "zx")]
    [InlineData("zZ", "zZ")]
    [InlineData("zq", "zq")]
    [InlineData("z<C-d>", "z<C-d>")]
    public void ZFollowedByAnythingButItsPartners_IsInvalidAndNamedInFull(string keys, string sequence)
    {
        VimParseResult result = Feed(keys, out KeySequenceParser parser);

        Assert.Equal(VimParseStatus.Invalid, result.Status);
        Assert.Equal(sequence, result.Sequence);
        Assert.False(parser.HasPendingSequence);
    }

    [Theory]
    // The six scroll commands vim spells with Control, and the two page keys that spell two of them
    // again. A count in front of one reaches it, and the letter's case does not matter.
    [InlineData("<C-d>", VimAction.ScrollHalfPageDown, 0)]
    [InlineData("5<C-d>", VimAction.ScrollHalfPageDown, 5)]
    [InlineData("<C-D>", VimAction.ScrollHalfPageDown, 0)]
    [InlineData("<C-u>", VimAction.ScrollHalfPageUp, 0)]
    [InlineData("<C-f>", VimAction.ScrollPageDown, 0)]
    [InlineData("2<C-f>", VimAction.ScrollPageDown, 2)]
    [InlineData("<C-b>", VimAction.ScrollPageUp, 0)]
    [InlineData("<C-e>", VimAction.ScrollLineDown, 0)]
    [InlineData("3<C-e>", VimAction.ScrollLineDown, 3)]
    [InlineData("<C-y>", VimAction.ScrollLineUp, 0)]
    [InlineData("<PageDown>", VimAction.ScrollPageDown, 0)]
    [InlineData("3<PageDown>", VimAction.ScrollPageDown, 3)]
    [InlineData("<PageUp>", VimAction.ScrollPageUp, 0)]
    public void TheScrollKeys_AreWholeCommandsThatTakeACount(string keys, VimAction action, int count)
    {
        VimParseResult result = Feed(keys, out KeySequenceParser parser);

        Assert.Equal(VimParseStatus.Complete, result.Status);
        Assert.Equal(new VimCommand(action, VimMotion.None, count), result.Command);
        Assert.False(parser.HasPendingSequence);
    }

    [Theory]
    [InlineData("<C-d>", VimAction.ScrollHalfPageDown)]
    [InlineData("<PageUp>", VimAction.ScrollPageUp)]
    [InlineData("zz", VimAction.ScrollCaretCenter)]
    [InlineData("z<CR>", VimAction.ScrollCaretTopToFirstNonBlank)]
    public void InAVisualMode_TheScrollKeysAreStillCommands(string keys, VimAction action)
        => Assert.Equal(new VimCommand(action), FeedIn(keys, VimMode.Visual, out _).Command);

    [Theory]
    // A held key that is not one of the six spells nothing; and a held key ends whatever was half
    // typed rather than being taken literally by it — 'r' and Ctrl+D is not "write a d".
    [InlineData("<C-x>", "<C-x>")]
    [InlineData("<A-d>", "<A-d>")]
    [InlineData("r<C-d>", "r<C-d>")]
    [InlineData("f<C-d>", "f<C-d>")]
    [InlineData("\"<C-d>", "\"<C-d>")]
    [InlineData("q<C-d>", "q<C-d>")]
    [InlineData("g<C-d>", "g<C-d>")]
    [InlineData("Z<C-d>", "Z<C-d>")]
    public void AHeldKey_ThatIsNotAScroll_OrThatFollowsAPrefix_IsInvalidAndNamedInFull(string keys, string sequence)
    {
        VimParseResult result = Feed(keys, out KeySequenceParser parser);

        Assert.Equal(VimParseStatus.Invalid, result.Status);
        Assert.Equal(sequence, result.Sequence);
        Assert.False(parser.HasPendingSequence);
    }

    [Theory]
    [InlineData("<C-d>", true)]
    [InlineData("<C-U>", true)]
    [InlineData("<C-f>", true)]
    [InlineData("<C-b>", true)]
    [InlineData("<C-e>", true)]
    [InlineData("<C-y>", true)]
    [InlineData("<C-r>", false)]
    [InlineData("<C-x>", false)]
    [InlineData("<A-d>", false)]
    [InlineData("d", false)]
    public void IsControlCommand_NamesTheSixScrollKeysAndNothingElse(string key, bool expected)
        => Assert.Equal(expected, KeySequenceParser.IsControlCommand(VimKeyNotation.Parse(key)[0]));

    [Theory]
    [InlineData("gu")]
    [InlineData("Z")]
    [InlineData("z")]
    public void Reset_ThrowsAwayTheNewPrefixesToo(string keys)
    {
        Feed(keys, out KeySequenceParser parser);

        parser.Reset();

        Assert.False(parser.HasPendingSequence);
        Assert.Equal(VimCommand.Move(VimMotion.WordForward), parser.Feed(VimKey.Typed('w')).Command);
    }

    [Fact]
    public void Reset_ThrowsAwayACountAndAnOperatorToo()
    {
        KeySequenceParser parser = new();
        parser.Feed(VimKey.Typed('3'));
        parser.Feed(VimKey.Typed('d'));

        parser.Reset();

        Assert.False(parser.HasPendingSequence);
        Assert.Equal(string.Empty, parser.PendingSequence);

        // And they are gone rather than waiting: this one is a plain 'w', not '3dw'.
        Assert.Equal(VimCommand.Move(VimMotion.WordForward), parser.Feed(VimKey.Typed('w')).Command);
    }

    [Fact]
    public void ACompletedCommandWithACount_LeavesTheCountBehindToo()
    {
        KeySequenceParser parser = new();
        parser.Feed(VimKey.Typed('3'));
        parser.Feed(VimKey.Typed('w'));

        Assert.Equal(VimCommand.Move(VimMotion.WordForward), parser.Feed(VimKey.Typed('w')).Command);
    }

    [Theory]
    [InlineData('v', VimAction.VisualCharacterwise)]
    [InlineData('V', VimAction.VisualLinewise)]
    public void TheVisualKeys_AreTheSameCommandInEveryModeTheyAreTypedIn(char character, VimAction action)
    {
        VimCommand expected = new(action);

        Assert.Equal(expected, new KeySequenceParser().Feed(VimKey.Typed(character)).Command);
        Assert.Equal(expected, new KeySequenceParser().Feed(VimKey.Typed(character), VimMode.Visual).Command);
        Assert.Equal(expected, new KeySequenceParser().Feed(VimKey.Typed(character), VimMode.VisualLine).Command);
    }

    [Theory]
    [InlineData("d", VimOperator.Delete)]
    [InlineData("y", VimOperator.Yank)]
    [InlineData("c", VimOperator.Change)]
    [InlineData(">", VimOperator.ShiftRight)]
    [InlineData("<", VimOperator.ShiftLeft)]
    [InlineData("gu", VimOperator.Lowercase)]
    [InlineData("gU", VimOperator.Uppercase)]
    [InlineData("g~", VimOperator.ToggleCase)]
    public void InAVisualMode_AnOperatorIsAWholeCommandOverTheSelection(string keys, VimOperator @operator)
    {
        VimParseResult result = FeedIn(keys, VimMode.Visual, out _);

        Assert.Equal(VimParseStatus.Complete, result.Status);

        // No motion: what it acts on is the selection, which the parser cannot see and does not need
        // to. And nothing is left pending, so the second 'd' of a 'dd' nobody can type is a command of
        // its own rather than half of one.
        Assert.Equal(VimCommand.Operate(@operator, VimMotion.None), result.Command);
    }

    [Fact]
    public void InAVisualMode_ACountStillReachesTheOperatorEvenThoughNothingUsesIt()
        => Assert.Equal(
            VimCommand.Operate(VimOperator.Delete, VimMotion.None, 2),
            FeedIn("2d", VimMode.Visual, out _).Command);

    [Theory]
    [InlineData("j", VimMotion.Down)]
    [InlineData("$", VimMotion.LineEnd)]
    [InlineData("w", VimMotion.WordForward)]
    [InlineData("W", VimMotion.WordForwardBig)]
    [InlineData("}", VimMotion.ParagraphForward)]
    [InlineData("%", VimMotion.MatchingBracket)]
    [InlineData(" ", VimMotion.RightWrapping)]
    [InlineData("gg", VimMotion.FirstLine)]
    [InlineData("G", VimMotion.LastLine)]
    // The searches too: in a visual mode a search extends the selection to what it finds.
    [InlineData("n", VimMotion.SearchNext)]
    [InlineData("N", VimMotion.SearchPrevious)]
    [InlineData("*", VimMotion.SearchWordForward)]
    [InlineData("#", VimMotion.SearchWordBackward)]
    // And the screen motions, which move the caret end of the selection like any other.
    [InlineData("H", VimMotion.ScreenTop)]
    [InlineData("M", VimMotion.ScreenMiddle)]
    [InlineData("L", VimMotion.ScreenBottom)]
    public void InAVisualMode_AMotionIsStillAMove(string keys, VimMotion motion)
        => Assert.Equal(VimCommand.Move(motion), FeedIn(keys, VimMode.Visual, out _).Command);

    [Theory]
    [InlineData('/')]
    [InlineData('?')]
    public void InAVisualMode_ASearchOpenerOpensTheCommandLine(char opener)
    {
        VimParseResult result = FeedIn(opener.ToString(), VimMode.Visual, out KeySequenceParser parser);

        Assert.Equal(VimParseStatus.Complete, result.Status);
        Assert.Equal(
            new VimCommand(VimAction.BeginCommandLine, VimMotion.None, 0, VimOperator.None, opener),
            result.Command);
        Assert.False(parser.HasPendingSequence);
    }

    [Theory]
    // Each of these is a command in normal mode and none of them is one while there is a selection:
    // the insert keys that are not text-object openers, the 'Z' pair, and ':'.
    [InlineData('I')]
    [InlineData('A')]
    [InlineData('O')]
    [InlineData('Z')]
    [InlineData(':')]
    public void InAVisualMode_AKeyWithNoVisualMeaning_IsInvalidRatherThanItsNormalModeSelf(char character)
    {
        KeySequenceParser parser = new();

        VimParseResult result = parser.Feed(VimKey.Typed(character), VimMode.Visual);

        Assert.Equal(VimParseStatus.Invalid, result.Status);
        Assert.False(parser.HasPendingSequence);
    }

    [Theory]
    // The spellings of the three operators: 'x' is 'd' over the selection, 's' is 'c', and the
    // capitals are the same operators made linewise — which is what WholeLine in place of None says.
    [InlineData('x', VimAction.Operate, VimOperator.Delete, VimMotion.None)]
    [InlineData('X', VimAction.Operate, VimOperator.Delete, VimMotion.WholeLine)]
    [InlineData('D', VimAction.Operate, VimOperator.Delete, VimMotion.WholeLine)]
    [InlineData('Y', VimAction.Operate, VimOperator.Yank, VimMotion.WholeLine)]
    [InlineData('s', VimAction.Operate, VimOperator.Change, VimMotion.None)]
    [InlineData('C', VimAction.Operate, VimOperator.Change, VimMotion.WholeLine)]
    [InlineData('S', VimAction.Operate, VimOperator.Change, VimMotion.WholeLine)]
    // The case trio, so 'u' is not undo while there is a selection.
    [InlineData('~', VimAction.Operate, VimOperator.ToggleCase, VimMotion.None)]
    [InlineData('u', VimAction.Operate, VimOperator.Lowercase, VimMotion.None)]
    [InlineData('U', VimAction.Operate, VimOperator.Uppercase, VimMotion.None)]
    // And the commands of their own.
    [InlineData('J', VimAction.Join, VimOperator.None, VimMotion.None)]
    [InlineData('o', VimAction.SwapVisualEnds, VimOperator.None, VimMotion.None)]
    public void InAVisualMode_TheVisualCommands_AreATableOfTheirOwn(
        char character,
        VimAction action,
        VimOperator @operator,
        VimMotion motion)
    {
        VimCommand expected = new(action, motion, 0, @operator);

        Assert.Equal(expected, new KeySequenceParser().Feed(VimKey.Typed(character), VimMode.Visual).Command);
        Assert.Equal(expected, new KeySequenceParser().Feed(VimKey.Typed(character), VimMode.VisualLine).Command);
    }

    [Theory]
    // 'p' and 'P' carry their key as the argument, the way ':' and '/' do: the engine has to know
    // which of the two it was, because only 'p' leaves what it replaced in the register.
    [InlineData("p", 'p', 0)]
    [InlineData("P", 'P', 0)]
    [InlineData("2p", 'p', 2)]
    public void InAVisualMode_PAndCapitalP_ReplaceTheSelectionAndSayWhichTheyWere(string keys, char argument, int count)
        => Assert.Equal(
            new VimCommand(VimAction.ReplaceSelection, VimMotion.None, count, VimOperator.None, argument),
            FeedIn(keys, VimMode.Visual, out _).Command);

    [Theory]
    [InlineData("gJ", VimAction.JoinRaw)]
    [InlineData("gv", VimAction.ReselectVisual)]
    public void InAVisualMode_GJAndGVAreCommands(string keys, VimAction action)
    {
        VimParseResult result = FeedIn(keys, VimMode.Visual, out KeySequenceParser parser);

        Assert.Equal(new VimCommand(action), result.Command);
        Assert.False(parser.HasPendingSequence);
    }

    [Fact]
    public void GV_IsACommandInNormalModeAndNothingBehindAnOperator()
    {
        Assert.Equal(new VimCommand(VimAction.ReselectVisual), Feed("gv", out _).Command);

        VimParseResult dead = Feed("dgv", out KeySequenceParser parser);

        Assert.Equal(VimParseStatus.Invalid, dead.Status);
        Assert.Equal("dgv", dead.Sequence);
        Assert.False(parser.HasPendingSequence);
    }

    [Fact]
    public void InAVisualMode_RArmsItselfAndTakesTheNextKeyLiterally()
    {
        KeySequenceParser parser = new();

        Assert.Equal(VimParseStatus.Pending, parser.Feed(VimKey.Typed('r'), VimMode.Visual).Status);
        Assert.True(parser.HasPendingSequence);

        // 'd' is the character to write, not a delete — exactly as 'rd' reads in normal mode.
        VimParseResult result = parser.Feed(VimKey.Typed('d'), VimMode.Visual);

        Assert.Equal(new VimCommand(VimAction.ReplaceCharacter, VimMotion.None, 0, VimOperator.None, 'd'), result.Command);
        Assert.False(parser.HasPendingSequence);
    }

    [Theory]
    // A register prefix rides on the command it precedes, on either side of the count, and reads the
    // same in normal mode and behind a doubled operator.
    [InlineData("\"ayy", VimAction.Operate, 'a')]
    [InlineData("\"a3yy", VimAction.Operate, 'a')]
    [InlineData("3\"ayy", VimAction.Operate, 'a')]
    [InlineData("\"Ayy", VimAction.Operate, 'A')]
    [InlineData("\"0p", VimAction.PutAfter, '0')]
    [InlineData("\"_dd", VimAction.Operate, '_')]
    [InlineData("\"ax", VimAction.DeleteCharacter, 'a')]
    public void ARegisterPrefix_RidesOnTheCommand(string keys, VimAction action, char register)
    {
        VimParseResult result = Feed(keys, out KeySequenceParser parser);

        Assert.Equal(VimParseStatus.Complete, result.Status);
        Assert.Equal(action, result.Command.Action);
        Assert.Equal(register, result.Command.Register);
        Assert.False(parser.HasPendingSequence);
    }

    [Theory]
    [InlineData("\"a")]
    [InlineData("2\"b")]
    public void ARegisterPrefixOnItsOwn_IsPendingAndCancellable(string keys)
    {
        VimParseResult result = Feed(keys, out KeySequenceParser parser);

        Assert.Equal(VimParseStatus.Pending, result.Status);
        Assert.True(parser.HasPendingSequence);
    }

    [Theory]
    // A register only makes sense on a command that reads or fills one, so a prefix on anything else
    // is a dead sequence rather than a command with a register nobody will use.
    [InlineData("\"aj", "\"aj")]
    [InlineData("\"ai", "\"ai")]
    // A prefix is pending until its second key says what it was, so '"az' dies as '"azz' — exactly as
    // '"ag' dies as '"agg'.
    [InlineData("\"azz", "\"azz")]
    [InlineData("\"a<C-d>", "\"a<C-d>")]
    public void ARegisterPrefixOnAKeyThatTakesNone_IsInvalidAndNamed(string keys, string sequence)
    {
        VimParseResult result = Feed(keys, out KeySequenceParser parser);

        Assert.Equal(VimParseStatus.Invalid, result.Status);
        Assert.Equal(sequence, result.Sequence);
        Assert.False(parser.HasPendingSequence);
    }

    [Theory]
    // A '"' is not a name a key can be looked for: only a-zA-Z, 0, _ and '"' itself. Fed just the
    // prefix, because a dead prefix resets the parser and a valid command typed after it would be the
    // result instead.
    [InlineData("\"1")]
    [InlineData("\" ")]
    public void ARegisterNameThatIsNotOne_IsInvalid(string keys)
        => Assert.Equal(VimParseStatus.Invalid, Feed(keys, out _).Status);

    [Fact]
    public void Dot_IsARepeatCommand()
    {
        VimParseResult result = new KeySequenceParser().Feed(VimKey.Typed('.'));

        Assert.Equal(VimParseStatus.Complete, result.Status);
        Assert.Equal(VimAction.Repeat, result.Command.Action);
    }

    [Fact]
    public void ACountOnDot_IsKept()
        => Assert.Equal(3, Feed("3.", out _).Command.Count);

    [Theory]
    // 'q' and '@' take the next key literally as the register, like 'r' and the finds.
    [InlineData('q')]
    [InlineData('@')]
    public void QAndAt_ArePendingAfterThemselves(char opener)
    {
        KeySequenceParser parser = new();

        Assert.Equal(VimParseStatus.Pending, parser.Feed(VimKey.Typed(opener)).Status);
        Assert.True(parser.HasPendingSequence);
    }

    [Theory]
    [InlineData("qa", VimAction.RecordMacro, 'a')]
    [InlineData("qA", VimAction.RecordMacro, 'A')]
    [InlineData("@a", VimAction.PlayMacro, 'a')]
    [InlineData("@0", VimAction.PlayMacro, '0')]
    [InlineData("3@a", VimAction.PlayMacro, 'a')]
    public void QAndAt_CompleteWithTheirRegister(string keys, VimAction action, char register)
    {
        VimParseResult result = Feed(keys, out _);

        Assert.Equal(VimParseStatus.Complete, result.Status);
        Assert.Equal(action, result.Command.Action);
        Assert.Equal(register, result.Command.Argument);
    }

    [Fact]
    public void AtAt_IsPlayLastMacro()
        => Assert.Equal(VimAction.PlayLastMacro, Feed("@@", out _).Command.Action);

    [Theory]
    // A recording goes into a letter; a digit or punctuation names no macro to record into.
    [InlineData("q1")]
    [InlineData("q\"")]
    public void QOnSomethingThatIsNotALetter_IsInvalid(string keys)
        => Assert.Equal(VimParseStatus.Invalid, Feed(keys, out _).Status);

    private static VimParseResult Feed(string keys, out KeySequenceParser parser)
        => FeedIn(keys, VimMode.Normal, out parser);

    // The keys are written in the engine's own notation, so a case can spell a held key or a named
    // one — <C-d>, <PageDown>, <CR> — beside the plain characters.
    private static VimParseResult FeedIn(string keys, VimMode mode, out KeySequenceParser parser)
    {
        parser = new KeySequenceParser();

        VimParseResult result = default;

        foreach (VimKey key in VimKeyNotation.Parse(keys))
        {
            result = parser.Feed(key, mode);
        }

        return result;
    }
}
