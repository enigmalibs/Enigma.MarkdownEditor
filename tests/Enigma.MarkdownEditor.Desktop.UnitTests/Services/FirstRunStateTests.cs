using System;
using System.IO;
using System.Threading.Tasks;
using Enigma.MarkdownEditor.Desktop.Services;
using Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Services;

/// <summary>
/// Tests for the first-run gate. It is a predicate over one path, which is the whole point: the
/// question has to be answerable before the settings loader has run, and a predicate can be asked in
/// a test without a running application.
/// </summary>
public sealed class FirstRunStateTests
{
    [Fact]
    public void NoSettingsFile_IsAFirstRun()
    {
        // The loader does not write the file, so a temp settings folder is a machine the editor has
        // never been started on.
        using TempSettings settings = new();

        Assert.False(File.Exists(settings.Config.FilePath));
        Assert.True(FirstRunState.Detect(settings.Config.FilePath).IsFirstRun);
    }

    [Fact]
    public async Task ASettingsFileThatExists_IsNotAFirstRun()
    {
        using TempSettings settings = new();
        await settings.Config.WriteAsync(TestContext.Current.CancellationToken);

        Assert.False(FirstRunState.Detect(settings.Config.FilePath).IsFirstRun);
    }

    [Fact]
    public void AQuarantinedSettingsFile_IsStillNotAFirstRun()
    {
        using TempSettings settings = new();

        // What a corruption looks like at the moment the gate is asked: the unreadable file is still
        // where it is, because the loader has not run yet and so has not moved it aside. Asking after
        // the load would read this as a first run and re-onboard someone who has used the editor for
        // months — the loader leaves the path absent, as
        // SettingsLoaderTests.ABrokenFile_IsQuarantinedAndTheDefaultsAreUsed asserts. This is why App
        // asks first.
        File.WriteAllText(settings.Config.FilePath, "{ not json");

        Assert.False(FirstRunState.Detect(settings.Config.FilePath).IsFirstRun);
    }

    [Fact]
    public void TheTwoStates_SayWhatTheyAre()
    {
        Assert.True(FirstRunState.First.IsFirstRun);
        Assert.False(FirstRunState.Returning.IsFirstRun);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AnEmptyPath_IsRejected(string path)
        => Assert.Throws<ArgumentException>(() => FirstRunState.Detect(path));

    [Fact]
    public void ANullPath_IsRejected()
        => Assert.Throws<ArgumentNullException>(() => FirstRunState.Detect(null!));
}
