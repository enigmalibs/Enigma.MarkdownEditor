using System;
using System.IO;
using Enigma.MarkdownEditor.Core.Markdown;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Markdown;

/// <summary>
/// What a pasted image is called and how the note points at it.
/// </summary>
public sealed class ImageLinkTests
{
    private static readonly DateTimeOffset Pasted = new(2026, 8, 28, 11, 45, 0, TimeSpan.Zero);

    [Fact]
    public void ThePastedFileIsNamedAfterTheNoteAndTheMoment()
        => Assert.Equal("meeting-20260828114500.png", ImageLink.FileName(Path.Combine("notes", "meeting.md"), Pasted));

    [Fact]
    public void ANoteWithNoNameOfItsOwn_FallsBackToAFixedStem()
        => Assert.Equal("image-20260828114500.png", ImageLink.FileName(".md", Pasted));

    [Fact]
    public void TheLinkIsRelativeToTheNotesOwnFolder()
    {
        string note = Path.Combine(Root, "notes", "meeting.md");
        string image = Path.Combine(Root, "notes", "assets", "meeting-20260828114500.png");

        Assert.Equal("assets/meeting-20260828114500.png", ImageLink.RelativePath(note, image));
    }

    [Fact]
    public void TheLinkUsesForwardSlashesOnEveryPlatform()
    {
        string note = Path.Combine(Root, "notes", "meeting.md");
        string image = Path.Combine(Root, "notes", "assets", "deeper", "shot.png");

        Assert.Equal("assets/deeper/shot.png", ImageLink.RelativePath(note, image));
        Assert.DoesNotContain('\\', ImageLink.RelativePath(note, image));
    }

    [Fact]
    public void AnImageBesideTheNote_IsJustItsName()
    {
        string note = Path.Combine(Root, "notes", "meeting.md");

        Assert.Equal("shot.png", ImageLink.RelativePath(note, Path.Combine(Root, "notes", "shot.png")));
    }

    [Fact]
    public void TheAltTextIsLeftEmpty()
        => Assert.Equal("![](assets/shot.png)", ImageLink.Markdown("assets/shot.png"));

    [Theory]
    [InlineData("assets/my shot.png")]
    [InlineData("assets/shot (2).png")]
    public void APathAMarkdownParserWouldMisread_IsWrappedInAngleBrackets(string relativePath)
        => Assert.Equal($"![](<{relativePath}>)", ImageLink.Markdown(relativePath));

    [Fact]
    public void TheArgumentsAreGuarded()
    {
        Assert.Throws<ArgumentNullException>(() => ImageLink.FileName(null!, Pasted));
        Assert.Throws<ArgumentException>(() => ImageLink.FileName("  ", Pasted));
        Assert.Throws<ArgumentNullException>(() => ImageLink.RelativePath(null!, "a.png"));
        Assert.Throws<ArgumentNullException>(() => ImageLink.Markdown(null!));
        Assert.Throws<ArgumentException>(() => ImageLink.Markdown(string.Empty));
    }

    // Rooted, because the relative path is worked out from full paths and a bare "notes/meeting.md"
    // would be resolved against whatever folder the test run happens to be in.
    private static string Root => OperatingSystem.IsWindows() ? @"C:\" : "/";
}
