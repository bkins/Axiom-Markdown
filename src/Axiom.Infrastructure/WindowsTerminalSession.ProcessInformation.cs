using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Axiom.Infrastructure;





/// <summary>Owns a Windows pseudoconsole and its shell. Pipe reads and writes run independently.</summary>
public sealed partial class WindowsTerminalSession
{
    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public nint Process, Thread;
        public int ProcessId, ThreadId;
    }
}
