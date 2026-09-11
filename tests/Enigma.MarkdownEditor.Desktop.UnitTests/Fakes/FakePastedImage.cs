using System.IO;
using System.Text;
using Enigma.MarkdownEditor.Desktop.Services;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;

/// <summary>
/// An image on the clipboard, without the graphics stack it would take to have a real one.
/// </summary>
/// <remarks>
/// It writes bytes to the path it is given, which is all the paste rule can observe about it: where
/// the file goes, what it is called, and that something is in it. Set <see cref="Failure"/> to make
/// the save throw the way a full disk or a read-only folder would.
/// </remarks>
internal sealed class FakePastedImage : IPastedImage
{
    public string? SavedTo { get; private set; }

    public IOException? Failure { get; set; }

    public void Save(string filePath)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        File.WriteAllBytes(filePath, Encoding.ASCII.GetBytes("PNG"));
        SavedTo = filePath;
    }
}
