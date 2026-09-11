using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace Enigma.MarkdownEditor.Core.Files;

/// <summary>
/// The Windows trash: the shell's own recycle bin, driven through <c>SHFileOperationW</c>.
/// </summary>
/// <remarks>
/// <para>
/// The recycle bin is not a folder anything can be moved into by hand — it is a shell facility, and
/// <c>SHFileOperationW</c> with <c>FOF_ALLOWUNDO</c> is the documented way to ask for it. Take that
/// one flag away and the very same call becomes a permanent delete, which is why it is spelled out
/// on its own below rather than folded in with the others.
/// </para>
/// <para>
/// <c>IFileOperation</c> is the modern replacement for this API, and would be the choice if this
/// class needed progress reporting or per-item results. It does not: one entry goes in, one answer
/// comes out, and <c>SHFileOperationW</c> says that in a single call with no COM apartment to
/// arrange first.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsTrashService : ITrashService
{
    /// <summary>
    /// <c>FO_DELETE</c>: the entries named by <c>pFrom</c> are to be deleted.
    /// </summary>
    private const uint FoDelete = 0x0003;

    /// <summary>
    /// <c>FOF_SILENT</c>: no progress dialog, however long the operation takes.
    /// </summary>
    private const ushort FofSilent = 0x0004;

    /// <summary>
    /// <c>FOF_NOCONFIRMATION</c>: answer "yes to all" to anything the shell would ask. The
    /// application has already put the question to the user; a second dialog would only confuse.
    /// </summary>
    private const ushort FofNoConfirmation = 0x0010;

    /// <summary>
    /// <c>FOF_ALLOWUNDO</c>: the flag that makes this the recycle bin rather than a delete.
    /// </summary>
    private const ushort FofAllowUndo = 0x0040;

    /// <summary>
    /// <c>FOF_NOCONFIRMMKDIR</c>: do not ask before creating a directory the operation needs.
    /// </summary>
    private const ushort FofNoConfirmMkDir = 0x0200;

    /// <summary>
    /// <c>FOF_NOERRORUI</c>: report failures through the return code instead of putting a message
    /// box on screen, which is the only form this class can turn into an exception the caller sees.
    /// </summary>
    private const ushort FofNoErrorUi = 0x0400;

    /// <summary>
    /// The flags every call from here uses.
    /// </summary>
    private const ushort DeleteFlags =
        FofAllowUndo | FofNoConfirmation | FofNoErrorUi | FofSilent | FofNoConfirmMkDir;

    /// <summary>
    /// <c>ERROR_FILE_NOT_FOUND</c>.
    /// </summary>
    private const int ErrorFileNotFound = 2;

    /// <summary>
    /// <c>ERROR_PATH_NOT_FOUND</c>.
    /// </summary>
    private const int ErrorPathNotFound = 3;

    /// <summary>
    /// <c>DE_INVALIDFILES</c>: the shell's own way of saying the source path led nowhere.
    /// </summary>
    private const int DeInvalidFiles = 0x7C;

    /// <inheritdoc />
    public bool IsAvailable => OperatingSystem.IsWindows();

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Offloaded with <see cref="Task.Run{TResult}(Func{TResult}, CancellationToken)"/> for the same
    /// reason <see cref="DirectoryLister"/> offloads its enumeration: the call underneath is blocking
    /// and there is no asynchronous form of it. Recycling a directory walks every entry inside it,
    /// and on a network share that is seconds of a frozen window if it runs where it was called.
    /// </para>
    /// <para>
    /// The token is honoured only up to the moment the shell takes over. Once the operation is under
    /// way there is no way to interrupt it, and abandoning the wait while it carried on would break
    /// this class's promise that <see langword="false"/> means the entry is untouched.
    /// </para>
    /// </remarks>
    public Task<bool> MoveToTrashAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        // Resolved here rather than inside the offload, so a malformed path is reported on the
        // calling thread as the argument mistake it is, before any work starts. SHFileOperationW
        // documents fully-qualified paths only; a relative one would be resolved against a working
        // directory this library does not control.
        string fullPath = Path.GetFullPath(path);

        return Task.Run(() => MoveToTrash(fullPath), cancellationToken);
    }

    // SYSLIB1054 asks for [LibraryImport] here, and that would be the better declaration: it is
    // exactly what this signature was shaped for, right down to the struct being blittable so the
    // generator has nothing to marshal. It is not used because its generator always emits unsafe
    // code and so demands <AllowUnsafeBlocks>true</AllowUnsafeBlocks> (SYSLIB1062), which no project
    // in this solution sets and which is not a build-wide switch to flip for one call. The
    // suppression is scoped to this one line, and swapping the two back over is a two-line change
    // the day that property is turned on.
#pragma warning disable SYSLIB1054
    [DllImport("shell32.dll", EntryPoint = "SHFileOperationW", ExactSpelling = true)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCTW lpFileOp);
#pragma warning restore SYSLIB1054

    private static bool MoveToTrash(string fullPath)
    {
        IntPtr from = IntPtr.Zero;

        try
        {
            // pFrom is not a string but a list of names, each closed by a NUL, with an empty name
            // closing the list itself — so a single entry has to end with two NULs. The marshaller
            // contributes the terminator any string gets, which is why the string handed to it
            // carries the other one already. A singly-terminated buffer is the classic way this call
            // goes wrong: the shell reads past the end and either does nothing at all or acts on
            // whatever happened to follow in memory.
            from = Marshal.StringToHGlobalUni(fullPath + '\0');

            SHFILEOPSTRUCTW operation = new()
            {
                // No owner window: the flags below suppress every dialog this call could raise, so
                // there is nothing left to parent one to.
                hwnd = IntPtr.Zero,
                wFunc = FoDelete,
                pFrom = from,

                // A delete has no destination.
                pTo = IntPtr.Zero,
                fFlags = DeleteFlags,

                // An out parameter in practice; zero is the "nothing aborted yet" it starts from.
                fAnyOperationsAborted = 0,
                hNameMappings = IntPtr.Zero,
                lpszProgressTitle = IntPtr.Zero,
            };

            int result = SHFileOperation(ref operation);

            if (result == 0)
            {
                // Success and abandonment are reported separately: zero comes back both when the
                // shell did what it was asked and when it was stopped part-way, and only this field
                // tells the two apart. Stopped means the entry may still be there, which is exactly
                // what the caller's permanent-delete offer exists for.
                return operation.fAnyOperationsAborted == 0;
            }

            if (result is ErrorFileNotFound or ErrorPathNotFound or DeInvalidFiles)
            {
                // The entry was not there to begin with — deleted from another window, or already
                // trashed by a second press on the menu item. The caller wanted it gone and it is,
                // so this is success rather than a failure the user has to read about.
                return true;
            }

            throw new IOException(
                $"The shell refused to recycle '{fullPath}'. SHFileOperationW returned {result} (0x{result:X}).");
        }
        finally
        {
            if (from != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(from);
            }
        }
    }

    /// <summary>
    /// The <c>SHFILEOPSTRUCTW</c> the shell reads its instructions from.
    /// </summary>
    /// <remarks>
    /// Deliberately blittable: every string is an <see cref="IntPtr"/> this class fills in by hand,
    /// and the flags and counters carry their exact Win32 widths. That costs one manual allocation
    /// for <c>pFrom</c> and buys a struct the runtime copies straight through with no marshaller in
    /// the way — and it is also the shape source-generated interop needs, for the day this project
    /// can use it.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    private struct SHFILEOPSTRUCTW
    {
        /// <summary>
        /// The window any dialog would belong to.
        /// </summary>
        public IntPtr hwnd;

        /// <summary>
        /// Which operation to perform; always <c>FO_DELETE</c> here.
        /// </summary>
        public uint wFunc;

        /// <summary>
        /// The double-NUL-terminated list of source paths.
        /// </summary>
        public IntPtr pFrom;

        /// <summary>
        /// The double-NUL-terminated list of destination paths; unused by a delete.
        /// </summary>
        public IntPtr pTo;

        /// <summary>
        /// The <c>FOF_*</c> flags.
        /// </summary>
        public ushort fFlags;

        /// <summary>
        /// Set by the shell to a non-zero value when the operation was stopped before it finished.
        /// </summary>
        public int fAnyOperationsAborted;

        /// <summary>
        /// Receives the shell's name-mapping table, and only ever when <c>FOF_WANTMAPPINGHANDLE</c>
        /// is set — which it is not.
        /// </summary>
        public IntPtr hNameMappings;

        /// <summary>
        /// The title of the progress dialog that <c>FOF_SILENT</c> keeps from ever appearing.
        /// </summary>
        public IntPtr lpszProgressTitle;
    }
}
