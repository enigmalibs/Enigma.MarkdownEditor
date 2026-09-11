using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Enigma.MarkdownEditor.Desktop.Views;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// <see cref="IClipboardWriter"/> over the main window's platform clipboard.
/// </summary>
public sealed class ClipboardWriter : IClipboardWriter
{
    // The HTML flavour travels as bytes rather than as a string on purpose: Avalonia writes a string
    // format to the Windows clipboard as UTF-16, and CF_HTML has to be UTF-8 for its own byte offsets
    // to mean anything. See ClipboardHtmlFormat.
    private static readonly DataFormat<byte[]> HtmlFormat =
        DataFormat.CreateBytesPlatformFormat(ClipboardHtmlFormat.Identifier);

    private readonly Func<MainWindow> _mainWindow;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClipboardWriter"/> class.
    /// </summary>
    /// <param name="mainWindow">
    /// Resolves the window whose clipboard is used. A factory rather than the window itself, exactly
    /// as <see cref="FileLauncher"/> takes one: the window depends on the ViewModel that depends on
    /// this service, so taking the instance here would close the cycle.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="mainWindow"/> is <see langword="null"/>.</exception>
    public ClipboardWriter(Func<MainWindow> mainWindow)
        => _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public async Task<bool> WriteTextAsync(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        // Answered rather than thrown: a platform with no clipboard is a state the caller can report
        // and carry on from, and it is not a bug in the copy the reader asked for.
        if (TopLevel.GetTopLevel(_mainWindow())?.Clipboard is not { } clipboard)
        {
            return false;
        }

        // Avalonia 12 replaced the old SetTextAsync with the typed data-transfer API: one value in
        // one named format, and the clipboard cleared of whatever it held first. The two-flavour
        // write below is the same call with a transfer carrying two formats instead of this shortcut.
        await clipboard.SetValueAsync(DataFormat.Text, text);

        return true;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">
    /// <paramref name="text"/> or <paramref name="html"/> is <see langword="null"/>.
    /// </exception>
    public async Task<bool> WriteTextAndHtmlAsync(string text, string html)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(html);

        if (TopLevel.GetTopLevel(_mainWindow())?.Clipboard is not { } clipboard)
        {
            return false;
        }

        // One item carrying both formats, not one item each: Avalonia documents that Windows and X11
        // support a single item for every format except File, so a second item would be dropped
        // rather than offered as an alternative.
        DataTransferItem item = new();

        item.SetText(text);
        item.Set(HtmlFormat, ClipboardHtmlFormat.Payload(html));

        DataTransfer transfer = new();

        transfer.Add(item);

        // Not disposed here, and not wrapped in a using: IClipboard.SetDataAsync documents that the
        // clipboard takes ownership and disposes the transfer once it stops being the clipboard's
        // contents. Disposing it would pull the data out from under the next paste.
        await clipboard.SetDataAsync(transfer);

        return true;
    }
}
