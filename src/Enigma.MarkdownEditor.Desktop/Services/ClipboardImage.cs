using System;
using Avalonia.Media.Imaging;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// An Avalonia bitmap taken off the clipboard, as something the paste service can file.
/// </summary>
/// <remarks>
/// The whole of the adapter is one call to <see cref="Bitmap.Save(string, BitmapEncoderOptions)"/>,
/// with PNG's own options rather than the encoder Avalonia would infer from the extension — the
/// overload that infers it is obsolete in Avalonia 12, and naming the format here is what the paste
/// rule assumes anyway. The bitmap stays owned by whoever read it from the clipboard.
/// </remarks>
internal sealed class ClipboardImage : IPastedImage
{
    private readonly Bitmap _bitmap;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClipboardImage"/> class.
    /// </summary>
    /// <param name="bitmap">What the clipboard gave back.</param>
    /// <exception cref="ArgumentNullException"><paramref name="bitmap"/> is <see langword="null"/>.</exception>
    public ClipboardImage(Bitmap bitmap) => _bitmap = bitmap ?? throw new ArgumentNullException(nameof(bitmap));

    /// <inheritdoc />
    public void Save(string filePath) => _bitmap.Save(filePath, PngBitmapEncoderOptions.Default);
}
