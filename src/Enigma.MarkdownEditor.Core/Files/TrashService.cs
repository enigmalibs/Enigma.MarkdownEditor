using System;

namespace Enigma.MarkdownEditor.Core.Files;

/// <summary>
/// Picks the <see cref="ITrashService"/> that matches the machine the application is running on.
/// </summary>
/// <remarks>
/// The choice is made once, here, so that nothing else in the application has to know there is more
/// than one implementation. Composition roots call this and register the answer; the rest of the code
/// only ever sees <see cref="ITrashService"/> and the two things its return value can mean.
/// </remarks>
public static class TrashService
{
    /// <summary>
    /// Builds the trash implementation for the running operating system.
    /// </summary>
    /// <returns>
    /// A Windows or Linux implementation where there is one, and <see cref="UnsupportedTrashService"/>
    /// everywhere else — including macOS, whose trash is reachable but not yet implemented here.
    /// </returns>
    public static ITrashService ForCurrentPlatform()
    {
        // Asked as a question rather than decided by a compile-time symbol: this assembly targets a
        // platform-neutral framework and runs unchanged on all three. The guard also happens to be
        // what tells the platform analyzer that the Windows-only type below is safe to construct.
        if (OperatingSystem.IsWindows())
        {
            return new WindowsTrashService();
        }

        if (OperatingSystem.IsLinux())
        {
            return new LinuxTrashService();
        }

        return new UnsupportedTrashService();
    }
}
