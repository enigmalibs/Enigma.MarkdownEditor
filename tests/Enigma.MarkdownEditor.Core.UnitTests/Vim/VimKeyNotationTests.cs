using Enigma.MarkdownEditor.Core.Vim;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Vim;

/// <summary>
/// The one notation, written and read back: what a status message names a dead sequence with, what a
/// macro register holds, and what the test harness spells its keys in.
/// </summary>
/// <remarks>
/// The property that matters is the round trip — a run of keys written and read back is the same run
/// — because a macro is recorded by writing it and replayed by reading it, and any key those two
/// disagree on is a key a macro would run as something else than it recorded.
/// </remarks>
public sealed class VimKeyNotationTests
{
    [Theory]
    [InlineData('a', "a")]
    [InlineData('X', "X")]
    [InlineData('3', "3")]
    [InlineData(' ', " ")]
    [InlineData('~', "~")]
    [InlineData('>', ">")]
    // The one character that is not itself: '<' is where a key name begins, so it is written the way
    // vim writes a literal one.
    [InlineData('<', "<lt>")]
    public void ACharacter_IsWrittenAsItself_ExceptTheOneThatOpensAName(char character, string written)
        => Assert.Equal(written, VimKeyNotation.Format(VimKey.Typed(character)));

    [Theory]
    [InlineData(VimSpecialKey.Escape, "<Esc>")]
    [InlineData(VimSpecialKey.Enter, "<CR>")]
    [InlineData(VimSpecialKey.Backspace, "<BS>")]
    [InlineData(VimSpecialKey.Tab, "<Tab>")]
    [InlineData(VimSpecialKey.Home, "<Home>")]
    [InlineData(VimSpecialKey.End, "<End>")]
    [InlineData(VimSpecialKey.Left, "<Left>")]
    [InlineData(VimSpecialKey.Right, "<Right>")]
    [InlineData(VimSpecialKey.Up, "<Up>")]
    [InlineData(VimSpecialKey.Down, "<Down>")]
    [InlineData(VimSpecialKey.PageUp, "<PageUp>")]
    [InlineData(VimSpecialKey.PageDown, "<PageDown>")]
    [InlineData(VimSpecialKey.Other, "<Other>")]
    public void ANamedKey_IsWrittenInAngleBrackets(VimSpecialKey special, string written)
        => Assert.Equal(written, VimKeyNotation.Format(VimKey.Named(special)));

    [Fact]
    public void AControlKey_CarriesItsModifier()
        => Assert.Equal("<C-r>", VimKeyNotation.Format(VimKey.Typed('r', VimModifiers.Control)));

    [Fact]
    public void ShiftOnACharacter_IsNotWritten_BecauseTheCharacterAlreadyCarriesIt()
        => Assert.Equal("G", VimKeyNotation.Format(VimKey.Typed('G', VimModifiers.Shift)));

    [Theory]
    // Every key the harness and the engine deal in, written out and read back to the same key.
    [InlineData("x")]
    [InlineData("ciwfoo<Esc>")]
    [InlineData("3dd")]
    [InlineData("di<lt>")]
    [InlineData("f<lt>")]
    [InlineData("<C-r>")]
    [InlineData("2o- <BS><Esc>")]
    [InlineData("A!!!<Esc>j0")]
    [InlineData("/foo<CR>n")]
    [InlineData("<PageDown><PageUp><C-d>zz3zt")]
    public void WritingAndReadingBack_IsTheSameKeys(string notation)
        => Assert.Equal(notation, VimKeyNotation.Format(VimKeyNotation.Parse(notation)));

    [Fact]
    public void ReadingBack_GivesTheKeysThemselves()
    {
        var keys = VimKeyNotation.Parse("a<Esc>");

        Assert.Equal(2, keys.Count);
        Assert.Equal(VimKey.Typed('a'), keys[0]);
        Assert.Equal(VimKey.Named(VimSpecialKey.Escape), keys[1]);
    }

    [Theory]
    // A '<' that opens nothing the notation knows is a '<', exactly as it is in a vim mapping — which
    // is what lets a yanked line, never meant as keys, be run as keys without every '<' in it becoming
    // a mystery.
    [InlineData("a<b", 3)]
    [InlineData("<Foo>", 5)]
    [InlineData("i<", 2)]
    public void ALessThanThatOpensNoName_IsALessThan(string text, int count)
    {
        var keys = VimKeyNotation.Parse(text);

        Assert.Equal(count, keys.Count);
        Assert.Contains(keys, key => key == VimKey.Typed('<'));
    }

    [Fact]
    public void TheSpaceAndBarNames_ReadBackAsTheirCharacters()
    {
        var keys = VimKeyNotation.Parse("<Space><Bar>");

        Assert.Equal(VimKey.Typed(' '), keys[0]);
        Assert.Equal(VimKey.Typed('|'), keys[1]);
    }

    [Fact]
    public void VimKeyToString_IsTheNotation()
    {
        Assert.Equal("<Esc>", VimKey.Named(VimSpecialKey.Escape).ToString());
        Assert.Equal("<lt>", VimKey.Typed('<').ToString());
        Assert.Equal("k", VimKey.Typed('k').ToString());
    }
}
