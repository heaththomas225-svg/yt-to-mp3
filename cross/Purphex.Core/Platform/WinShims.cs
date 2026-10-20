// Stand-ins for the few Windows types that engine code names in signatures only (opening a plugin's window, an owner window).
// The cross-platform host gives these its own meaning; nothing here draws or touches Windows.
using System;
namespace System.Windows.Forms
{
    public interface IWin32Window { IntPtr Handle { get; } }
}
