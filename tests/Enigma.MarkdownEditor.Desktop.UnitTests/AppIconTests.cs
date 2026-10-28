using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using Avalonia.Headless.XUnit;
using Avalonia.Platform;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests;

/// <summary>
/// The application icon's two embedded assets, read as bytes.
/// </summary>
/// <remarks>
/// <para>
/// <c>Assets/app.ico</c> is what the window, the <c>.exe</c> and the MSI shortcuts show, and
/// <c>Assets/app-256.png</c> is what the splash and the About dialog draw. Both are binary files
/// replaced wholesale when the artwork changes, and nothing else in the suite reads the icon at all —
/// the Splash and About tests only prove the PNG resolves. A truncated copy or a wrong-sized export
/// would otherwise ship without a single failure.
/// </para>
/// <para>
/// The headless platform draws without Skia, so nothing here decodes an image. What is read is the
/// container — the icon's directory and the PNG's <c>IHDR</c> chunk — which is where a bad export
/// shows.
/// </para>
/// </remarks>
public sealed class AppIconTests
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [AvaloniaFact]
    public void TheIcon_CarriesTheFramesTheShellAsksFor()
    {
        byte[] icon = Read("app.ico");

        // ICONDIR: reserved (0), type (1 = icon), frame count; then one 16-byte ICONDIRENTRY per frame.
        Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(icon.AsSpan(0)));
        Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(icon.AsSpan(2)));

        int count = BinaryPrimitives.ReadUInt16LittleEndian(icon.AsSpan(4));
        HashSet<int> sizes = [];

        for (int i = 0; i < count; i++)
        {
            ReadOnlySpan<byte> entry = icon.AsSpan(6 + (16 * i), 16);

            // The width and height fields are one byte wide, so 0 stands for 256.
            int width = entry[0] == 0 ? 256 : entry[0];
            int height = entry[1] == 0 ? 256 : entry[1];
            uint length = BinaryPrimitives.ReadUInt32LittleEndian(entry[8..]);
            uint offset = BinaryPrimitives.ReadUInt32LittleEndian(entry[12..]);

            Assert.Equal(width, height);
            Assert.True(length > 0 && (long)offset + length <= icon.Length, $"The {width} px frame lies outside the file.");

            sizes.Add(width);
        }

        // Windows' small and large icons at 100 % (16, 32), the small one at 150 % (24), Explorer's
        // medium view (48) and its jumbo view (256). The window and the taskbar pick from the same set.
        Assert.Superset(new HashSet<int> { 16, 24, 32, 48, 256 }, sizes);
    }

    [AvaloniaFact]
    public void TheSplashAndAboutImage_Is256PixelsSquare()
    {
        byte[] png = Read("app-256.png");

        // The signature, then IHDR — always the first chunk: length (4), type (4), then the width and
        // the height, four bytes each, big-endian.
        Assert.Equal(PngSignature, png[..8]);
        Assert.Equal("IHDR"u8.ToArray(), png[12..16]);
        Assert.Equal(256, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16)));
        Assert.Equal(256, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20)));
    }

    private static byte[] Read(string asset)
    {
        using Stream stream = AssetLoader.Open(new Uri($"avares://Enigma.MarkdownEditor.Desktop/Assets/{asset}"));
        using MemoryStream buffer = new();

        stream.CopyTo(buffer);

        return buffer.ToArray();
    }
}
