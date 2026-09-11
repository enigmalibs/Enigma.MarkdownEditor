using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Enigma.MarkdownEditor.Core.Files;

/// <summary>
/// The Linux trash: <c>gio trash</c> where the desktop stack is installed, and a minimal
/// implementation of the freedesktop.org Trash specification where it is not.
/// </summary>
/// <remarks>
/// <para>
/// <c>gio trash</c> is tried first because it is the desktop's own answer: it knows about per-volume
/// trash directories, about the trash on a removable drive, and about whatever the running desktop
/// environment expects to see. Reimplementing that would be reimplementing GLib.
/// </para>
/// <para>
/// The fallback exists because <c>gio</c> is absent on plenty of perfectly ordinary machines — a
/// server, a container, a minimal window-manager install. It is deliberately <i>minimal</i>: it
/// writes into the home trash under <c>$XDG_DATA_HOME/Trash</c> and nowhere else. The specification's
/// per-volume <c>.Trash-$uid</c> directories are out of scope, so an entry on a different filesystem
/// from the home directory cannot be moved there and is reported as untrashable — which routes the
/// user to the caller's confirmed permanent delete rather than to a surprise.
/// </para>
/// </remarks>
public sealed class LinuxTrashService : ITrashService
{
    /// <summary>
    /// How many names are tried in the trash before giving up on finding a free one. Reaching this
    /// means a thousand entries already share the name; that is a trash worth emptying, not a case
    /// worth looping over.
    /// </summary>
    private const int MaximumNameAttempts = 100;

    /// <inheritdoc />
    public bool IsAvailable => OperatingSystem.IsLinux();

    /// <inheritdoc />
    /// <remarks>
    /// A non-zero exit from <c>gio</c>, or no <c>gio</c> at all, falls through to the fallback rather
    /// than throwing: the tool declining is not evidence that the entry cannot be trashed, only that
    /// this route to the trash is unavailable. Cancellation is the exception to that — it is raised,
    /// because a <c>gio</c> that was still running when the wait was abandoned leaves the outcome
    /// genuinely unknown, and this class must never answer <see langword="false"/> for an entry it
    /// cannot promise is untouched.
    /// </remarks>
    public async Task<bool> MoveToTrashAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        // The Trash specification stores the original location, and a relative path would record a
        // location that only means anything from this process's working directory.
        string fullPath = Path.GetFullPath(path);

        if (await TryGioTrashAsync(fullPath, cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        // Offloaded for the reason DirectoryLister offloads its enumeration: moving a directory into
        // the trash is a blocking file-system operation with no asynchronous form, and it must not
        // run on the thread that called it.
        return await Task.Run(() => MoveToHomeTrash(fullPath), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asks the desktop stack to do it.
    /// </summary>
    /// <param name="fullPath">The absolute path to trash.</param>
    /// <param name="cancellationToken">Abandons the wait for the tool.</param>
    /// <returns><see langword="true"/> when <c>gio</c> reported success.</returns>
    private static async Task<bool> TryGioTrashAsync(string fullPath, CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = "gio",

            // Required for the redirections below, and it also keeps the tool from being resolved
            // through a shell this class does not control.
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        startInfo.ArgumentList.Add("trash");

        // No "--" separator is needed: the path came out of Path.GetFullPath and therefore starts
        // with a slash, so it can never be mistaken for an option.
        startInfo.ArgumentList.Add(fullPath);

        try
        {
            using Process? process = Process.Start(startInfo);

            if (process is null)
            {
                return false;
            }

            // Both pipes are drained while the process runs. A redirected pipe nobody reads fills up
            // and blocks the writer, and a gio that is blocked writing an error message would hang
            // this wait instead of failing it.
            Task<string> output = process.StandardOutput.ReadToEndAsync(cancellationToken);
            Task<string> error = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            await Task.WhenAll(output, error).ConfigureAwait(false);

            return process.ExitCode == 0;
        }
        catch (Win32Exception)
        {
            // There is no gio on this machine. Not an error — the fallback below is exactly what
            // this case was written for.
            return false;
        }
    }

    /// <summary>
    /// Moves an entry into the home trash the way the freedesktop.org Trash specification describes.
    /// </summary>
    /// <param name="fullPath">The absolute path to trash.</param>
    /// <returns>
    /// <see langword="true"/> when the entry is in the trash, <see langword="false"/> when it is
    /// still exactly where it was.
    /// </returns>
    /// <remarks>
    /// Every failure before the entry itself is touched answers <see langword="false"/> rather than
    /// throwing: at that point nothing has moved, which is precisely the promise
    /// <see langword="false"/> makes, and the caller's permanent-delete offer is a better outcome for
    /// the user than an error about a trash directory they never asked about.
    /// </remarks>
    private static bool MoveToHomeTrash(string fullPath)
    {
        bool isDirectory = Directory.Exists(fullPath);

        if (!isDirectory && !File.Exists(fullPath))
        {
            // Already gone. The caller wanted it not to be there, and it is not.
            return true;
        }

        string? trashRoot = FindTrashRoot();

        if (trashRoot is null)
        {
            return false;
        }

        string filesDirectory = Path.Combine(trashRoot, "files");
        string infoDirectory = Path.Combine(trashRoot, "info");

        try
        {
            Directory.CreateDirectory(filesDirectory);
            Directory.CreateDirectory(infoDirectory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // No writable trash on this machine, and nothing has been touched.
            return false;
        }

        string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(fullPath));

        // The .trashinfo file goes down first, on purpose: a trash entry whose original location is
        // unknown cannot be restored, so the record has to exist before the entry it describes does.
        // The reverse order leaves an unrestorable file behind if the process dies in between.
        (string TargetPath, string InfoPath)? claim = ClaimName(filesDirectory, infoDirectory, name, fullPath);

        if (claim is null)
        {
            return false;
        }

        try
        {
            if (isDirectory)
            {
                Directory.Move(fullPath, claim.Value.TargetPath);
            }
            else
            {
                File.Move(fullPath, claim.Value.TargetPath);
            }
        }
        catch (IOException)
        {
            // Almost always a cross-device move: the entry lives on a different filesystem from the
            // home directory, and the specification answers that with a per-volume trash directory
            // this minimal implementation does not provide. The .trashinfo written a moment ago has
            // to go with it, or the trash grows an entry pointing at a file that is not in it.
            TryDelete(claim.Value.InfoPath);

            return false;
        }

        return true;
    }

    /// <summary>
    /// Takes a name in the trash that nothing else is using, by writing its <c>.trashinfo</c> record.
    /// </summary>
    /// <param name="filesDirectory">The trash's <c>files</c> directory.</param>
    /// <param name="infoDirectory">The trash's <c>info</c> directory.</param>
    /// <param name="name">The entry's own name, which is the first choice.</param>
    /// <param name="originalPath">The absolute path the entry is being taken from.</param>
    /// <returns>The two paths that were claimed, or <see langword="null"/> when none could be.</returns>
    private static (string TargetPath, string InfoPath)? ClaimName(
        string filesDirectory,
        string infoDirectory,
        string name,
        string originalPath)
    {
        string record = BuildTrashInfo(originalPath);

        for (int attempt = 1; attempt <= MaximumNameAttempts; attempt++)
        {
            // Suffixed at the very end rather than before the extension, which is what the desktop
            // implementations do and what a user scanning the trash expects to read.
            string candidate = attempt == 1 ? name : $"{name}.{attempt}";
            string targetPath = Path.Combine(filesDirectory, candidate);
            string infoPath = Path.Combine(infoDirectory, candidate + ".trashinfo");

            if (Path.Exists(targetPath) || Path.Exists(infoPath))
            {
                continue;
            }

            try
            {
                // CreateNew is the claim itself: it fails rather than overwrites if another process
                // took the same name between the check above and this line, which is the only race
                // this loop actually has.
                using FileStream stream = new(infoPath, FileMode.CreateNew, FileAccess.Write);
                stream.Write(Encoding.UTF8.GetBytes(record));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            return (targetPath, infoPath);
        }

        return null;
    }

    /// <summary>
    /// Builds the contents of a <c>.trashinfo</c> record.
    /// </summary>
    /// <param name="originalPath">The absolute path the entry is being taken from.</param>
    /// <returns>The record, exactly as the specification asks for it.</returns>
    /// <remarks>
    /// <c>DeletionDate</c> is local time with no offset, which is what the specification asks for —
    /// the file managers that read this show the user when <i>they</i> deleted something, so
    /// <see cref="DateTime.Now"/> is right here and <see cref="DateTime.UtcNow"/> would be wrong. The
    /// format is pinned to the invariant culture all the same, since a non-Gregorian calendar would
    /// otherwise write a year no reader could parse.
    /// </remarks>
    private static string BuildTrashInfo(string originalPath)
    {
        // Percent-encoded per the specification, which cites RFC 2396 — except for the separators,
        // which stay readable so the record is still a path a human can recognise.
        string encodedPath = Uri.EscapeDataString(originalPath).Replace("%2F", "/", StringComparison.Ordinal);
        string deletionDate = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

        return $"[Trash Info]\nPath={encodedPath}\nDeletionDate={deletionDate}\n";
    }

    /// <summary>
    /// Finds the home trash directory.
    /// </summary>
    /// <returns>
    /// The trash root, or <see langword="null"/> when this process has no home directory to put one
    /// in.
    /// </returns>
    private static string? FindTrashRoot()
    {
        string? dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");

        // The specification requires XDG_DATA_HOME to be absolute and says a relative value is to be
        // ignored, which is what keeps a stray export from putting a trash directory in whatever
        // folder the application happened to be started from.
        if (string.IsNullOrWhiteSpace(dataHome) || !Path.IsPathRooted(dataHome))
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            if (string.IsNullOrWhiteSpace(home))
            {
                return null;
            }

            dataHome = Path.Combine(home, ".local", "share");
        }

        return Path.Combine(dataHome, "Trash");
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Cleaning up after a move that already failed. A stale record is untidy; failing the
            // whole delete over it would be worse.
        }
    }
}
