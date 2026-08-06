using System.Windows;
using System.Windows.Interop;

namespace Oops;

public sealed class MessageWindow : Window
{
    public IntPtr Handle { get; private set; }

    public MessageWindow()
    {
        Width = 0;
        Height = 0;
        WindowStyle = WindowStyle.None;
        ShowInTaskbar = false;
        Visibility = Visibility.Hidden;
    }

    public HwndSource CreateSource()
    {
        var helper = new WindowInteropHelper(this);
        helper.EnsureHandle();
        Handle = helper.Handle;
        return HwndSource.FromHwnd(Handle)!;
    }
}
