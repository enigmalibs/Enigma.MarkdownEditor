using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Enigma.MarkdownEditor.Core.Files;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Files;

/// <summary>
/// Tests for the trash services: that the right one is chosen, that every one of them keeps the
/// promise the interface makes about <see langword="false"/>, and that the one belonging to the
/// running system really does empty a path off the disk.
/// </summary>
/// <remarks>
/// <para>
/// The platform-specific tests return early instead of asserting, the same way
/// <c>DirectoryListerTests</c> branches on the operating system rather than skipping: the suite has
/// to stay green on all three, and a test that quietly has nothing to say on macOS is preferable to
/// one that fails there for being written elsewhere.
/// </para>
/// <para>
/// <b>Side effect, deliberate:</b> the round trip below really trashes a file, so a run on Windows
/// leaves one entry in the developer's recycle bin — hence the <c>enigma-markdowneditor-trash-test-</c>
/// name, which makes it obvious where it came from. Nothing short of a real call proves the native
/// marshalling is right, and a wrong one is silent: it deletes nothing, or something else.
/// </para>
/// </remarks>
public sealed class TrashServiceTests
{
    [Fact]
    public void ForCurrentPlatform_ReturnsTheImplementationTheRunningSystemHas()
    {
        ITrashService service = TrashService.ForCurrentPlatform();

        if (OperatingSystem.IsWindows())
        {
            Assert.IsType<WindowsTrashService>(service);
            Assert.True(service.IsAvailable);
        }
        else if (OperatingSystem.IsLinux())
        {
            Assert.IsType<LinuxTrashService>(service);
            Assert.True(service.IsAvailable);
        }
        else
        {
            Assert.IsType<UnsupportedTrashService>(service);
            Assert.False(service.IsAvailable);
        }
    }

    [Fact]
    public async Task AnUnsupportedPlatform_DeclinesAndLeavesTheEntryWhereItWas()
    {
        using TempTreeFolder folder = new();
        string file = folder.AddFile("notes.md", "still here");

        UnsupportedTrashService service = new();

        bool trashed = await service.MoveToTrashAsync(file, TestContext.Current.CancellationToken);

        // The whole contract in three lines: declined, and therefore untouched.
        Assert.False(service.IsAvailable);
        Assert.False(trashed);
        Assert.True(File.Exists(file));
    }

    [Fact]
    public async Task EveryImplementation_ChecksItsPath()
    {
        List<ITrashService> services = [new UnsupportedTrashService(), new LinuxTrashService()];

        if (OperatingSystem.IsWindows())
        {
            services.Add(new WindowsTrashService());
        }

        foreach (ITrashService service in services)
        {
            await Assert.ThrowsAnyAsync<ArgumentException>(
                () => service.MoveToTrashAsync(null!, TestContext.Current.CancellationToken));
            await Assert.ThrowsAnyAsync<ArgumentException>(
                () => service.MoveToTrashAsync("   ", TestContext.Current.CancellationToken));
        }
    }

    /// <summary>
    /// The one test that actually costs something: it leaves a file in the running user's trash.
    /// </summary>
    /// <remarks>
    /// A file rather than a directory, and only one of them. Each round trip leaves its own entry in
    /// the recycle bin, and a second one would double that cost to re-test marshalling that is
    /// identical for both kinds of entry.
    /// </remarks>
    [Fact]
    public async Task OnAPlatformWithATrash_AFileReallyGoesIntoIt()
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
        {
            return;
        }

        using TempTreeFolder folder = new();
        string file = folder.AddFile(
            $"enigma-markdowneditor-trash-test-{Guid.NewGuid():N}.md",
            "This file was created by the Enigma.MarkdownEditor test suite and can be deleted.");

        ITrashService service = TrashService.ForCurrentPlatform();

        bool trashed = await service.MoveToTrashAsync(file, TestContext.Current.CancellationToken);

        Assert.True(trashed);
        Assert.False(File.Exists(file));
    }

    [Fact]
    public async Task AnEntryThatIsAlreadyGone_CountsAsTrashed()
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
        {
            return;
        }

        using TempTreeFolder folder = new();

        ITrashService service = TrashService.ForCurrentPlatform();

        // The caller's goal is that the entry is not there. It is not there.
        Assert.True(await service.MoveToTrashAsync(folder.Combine("gone.md"), TestContext.Current.CancellationToken));
    }
}
