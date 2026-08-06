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
                if (!Clipboard.ContainsData(DataFormats.UnicodeText) &&
                    !Clipboard.ContainsData(DataFormats.Text) &&
                    !Clipboard.ContainsImage() &&
                    !Clipboard.ContainsFileDropList())
                {
                    return ClipboardSnapshot.Empty();
                }

                var data = Clipboard.GetDataObject();
                return data is null ? ClipboardSnapshot.Empty() : new ClipboardSnapshot(data);
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
                // Never call Clipboard.Clear() — it wipes Windows clipboard history
                // and can break copy/paste system-wide.
                if (snapshot.IsEmpty)
                    return;

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
