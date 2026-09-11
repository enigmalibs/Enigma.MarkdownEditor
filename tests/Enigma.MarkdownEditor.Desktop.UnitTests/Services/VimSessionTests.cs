using System;
using Enigma.MarkdownEditor.Core.Vim;
using Enigma.MarkdownEditor.Desktop.Services;
using Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Services;

/// <summary>
/// The one piece of vim state the whole window shares: the setting, the mode, and the message.
/// </summary>
/// <remarks>
/// Its only real job is to say when something changed and not when nothing did — the status bar
/// rebuilds a row on every notification, and the mode changes several times a sentence.
/// </remarks>
public sealed class VimSessionTests
{
    [Fact]
    public void ASession_StartsFromTheSetting()
    {
        using TempSettings settings = new();
        settings.Value.VimEnabled = false;

        VimSession session = new(settings.Config);

        Assert.False(session.IsEnabled);
        Assert.Equal(VimMode.Normal, session.Mode);
        Assert.Null(session.StatusMessage);
    }

    [Fact]
    public void VimIsOnByDefault_WhichIsWhatTheDefaultSettingsSay()
        => Assert.True(Create().IsEnabled);

    [Fact]
    public void AModeChange_IsAnnouncedOnce()
    {
        VimSession session = Create();
        int announced = 0;
        session.Changed += (_, _) => announced++;

        session.ReportMode(VimMode.Insert);
        session.ReportMode(VimMode.Insert);

        Assert.Equal(VimMode.Insert, session.Mode);
        Assert.Equal(1, announced);
    }

    [Fact]
    public void AMessage_IsAnnouncedAndCleared()
    {
        VimSession session = Create();
        int announced = 0;
        session.Changed += (_, _) => announced++;

        session.ReportStatus("Not a command: z");
        session.ReportStatus("Not a command: z");
        session.ReportStatus(null);

        Assert.Null(session.StatusMessage);
        Assert.Equal(2, announced);
    }

    [Fact]
    public void ACommandLine_IsAnnouncedAndCleared()
    {
        VimSession session = Create();
        int announced = 0;
        session.Changed += (_, _) => announced++;

        session.ReportCommandLine(":w");
        session.ReportCommandLine(":w");
        session.ReportCommandLine(null);

        Assert.Null(session.CommandLine);
        Assert.Equal(2, announced);
    }

    [Fact]
    public void SwitchingVimOff_TakesAHalfTypedCommandLineDownWithTheMode()
    {
        using TempSettings settings = new();
        VimSession session = new(settings.Config);
        session.ReportCommandLine(":w");

        settings.Value.VimEnabled = false;
        session.OnSettingsChanged();

        Assert.Null(session.CommandLine);
    }

    [Fact]
    public void SwitchingVimOff_IsAnnouncedAndPutsTheModeBack()
    {
        using TempSettings settings = new();
        VimSession session = new(settings.Config);
        session.ReportMode(VimMode.Insert);

        int announced = 0;
        session.Changed += (_, _) => announced++;

        settings.Value.VimEnabled = false;
        session.OnSettingsChanged();

        Assert.False(session.IsEnabled);
        Assert.Equal(VimMode.Normal, session.Mode);
        Assert.Equal(1, announced);
    }

    [Fact]
    public void ASettingsVisitThatChangedNothing_IsNotAnnounced()
    {
        VimSession session = Create();
        int announced = 0;
        session.Changed += (_, _) => announced++;

        session.OnSettingsChanged();

        Assert.Equal(0, announced);
    }

    [Fact]
    public void ItsSettings_AreRequired()
        => Assert.Throws<ArgumentNullException>(() => new VimSession(null!));

    private static VimSession Create()
    {
        using TempSettings settings = new();

        return new VimSession(settings.Config);
    }
}
