using System.Windows;
using Clipboard = System.Windows.Clipboard;
using DataFormats = System.Windows.DataFormats;
using IDataObject = System.Windows.IDataObject;

namespace Oops.Services;

public sealed class ClipboardService
{
    private readonly object _sync = new();

    public ClipboardSnapshot? Capture()
    {
        lock (_sync)
        {
            try
            {
                if (!Clipboard.ContainsData(DataFormats.Text) &&
                    !Clipboard.ContainsImage() &&
                    !Clipboard.ContainsFileDropList())
                {
                    return ClipboardSnapshot.Empty();
                }

                return new ClipboardSnapshot(Clipboard.GetDataObject());
            }
            catch
            {
                return null;
            }
        }
    }

    public bool TrySetText(string text)
    {
        lock (_sync)
        {
            for (var attempt = 0; attempt < 8; attempt++)
            {
                try
                {
                    Clipboard.SetText(text);
                    return true;
                }
                catch
                {
                    Thread.Sleep(30);
                }
            }

            return false;
        }
    }

    public string? TryGetText()
    {
        lock (_sync)
        {
            for (var attempt = 0; attempt < 8; attempt++)
            {
                try
                {
                    if (!Clipboard.ContainsText())
                        return null;

                    return Clipboard.GetText();
                }
                catch
                {
                    Thread.Sleep(30);
                }
            }

            return null;
        }
    }

    public void Restore(ClipboardSnapshot? snapshot)
    {
        if (snapshot is null)
            return;

        lock (_sync)
        {
            try
            {
                if (snapshot.IsEmpty)
                {
                    try { Clipboard.Clear(); } catch { }
                    return;
                }

                if (snapshot.DataObject is not null)
                    Clipboard.SetDataObject(snapshot.DataObject, copy: true);
            }
            catch
            {
                // Best-effort restore.
            }
        }
    }
}

public sealed class ClipboardSnapshot
{
    public IDataObject? DataObject { get; }
    public bool IsEmpty { get; }

    private ClipboardSnapshot(IDataObject? dataObject, bool isEmpty)
    {
        DataObject = dataObject;
        IsEmpty = isEmpty;
    }

    public static ClipboardSnapshot Empty() => new(null, isEmpty: true);

    public ClipboardSnapshot(IDataObject dataObject) : this(dataObject, isEmpty: false)
    {
    }
}
