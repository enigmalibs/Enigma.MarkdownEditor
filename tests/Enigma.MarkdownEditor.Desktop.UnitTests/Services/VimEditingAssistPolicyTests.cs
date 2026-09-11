using System;
using Enigma.MarkdownEditor.Core.Vim;
using Enigma.MarkdownEditor.Desktop.Services;
using Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Services;

/// <summary>
/// The gate FEATURE-3C44 left for vim: when the markdown assists apply, and when the same keys are
/// commands instead.
/// </summary>
public sealed class VimEditingAssistPolicyTests
{
    [Fact]
    public void WithVimOff_TheAssistsAreAlwaysOnJustAsBefore()
    {
        VimSession session = Create(vimEnabled: false);

        Assert.True(new VimEditingAssistPolicy(session).IsActive);
    }

    [Fact]
    public void InNormalMode_TheAssistsAreOff()
    {
        VimSession session = Create(vimEnabled: true);
        VimEditingAssistPolicy policy = new(session);

        Assert.Equal(VimMode.Normal, session.Mode);

        // Enter in normal mode is a command. An editor that continued a bullet here would not be vim.
        Assert.False(policy.IsActive);
    }

    [Fact]
    public void InInsertMode_TheAssistsAreOnAgain()
    {
        VimSession session = Create(vimEnabled: true);
        VimEditingAssistPolicy policy = new(session);

        session.ReportMode(VimMode.Insert);

        Assert.True(policy.IsActive);
    }

    [Theory]
    [InlineData(VimMode.Visual)]
    [InlineData(VimMode.VisualLine)]
    public void InAVisualMode_TheAssistsAreOffAsTheyAreInNormalMode(VimMode mode)
    {
        VimSession session = Create(vimEnabled: true);
        VimEditingAssistPolicy policy = new(session);

        session.ReportMode(mode);

        // A selection is being commanded, not typed into. Insert mode is the only one that is typing.
        Assert.False(policy.IsActive);
    }

    [Fact]
    public void ItFollowsTheModeRatherThanReadingItOnce()
    {
        VimSession session = Create(vimEnabled: true);
        VimEditingAssistPolicy policy = new(session);

        session.ReportMode(VimMode.Insert);
        Assert.True(policy.IsActive);

        session.ReportMode(VimMode.Normal);
        Assert.False(policy.IsActive);
    }

    [Fact]
    public void ItsSession_IsRequired()
        => Assert.Throws<ArgumentNullException>(() => new VimEditingAssistPolicy(null!));

    private static VimSession Create(bool vimEnabled)
    {
        using TempSettings settings = new();
        settings.Value.VimEnabled = vimEnabled;

        return new VimSession(settings.Config);
    }
}
