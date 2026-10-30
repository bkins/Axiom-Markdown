using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Axiom.Infrastructure;





/// <summary>Owns a Windows pseudoconsole and its shell. Pipe reads and writes run independently.</summary>
public sealed partial class WindowsTerminalSession
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int Size;
        public nint Reserved;
        public nint Desktop;
        public nint Title;
        public int X, Y, XSize, YSize, XCountChars, YCountChars, FillAttribute, Flags;
        public short ShowWindow, ReservedSize;
        public nint ReservedBytes, StdInput, StdOutput, StdError;
    }
}
