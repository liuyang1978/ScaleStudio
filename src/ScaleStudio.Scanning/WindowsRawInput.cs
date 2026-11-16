using System.ComponentModel;
using System.Runtime.InteropServices;
namespace ScaleStudio.Scanning;

/// <summary>Host forwards WM_INPUT lParam. Does not suppress normal keyboard messages.</summary>
public sealed class WindowsRawInput : IDisposable
{
    private readonly Scanner scanner;
    public WindowsRawInput(nint window, Scanner scanner)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        this.scanner = scanner;
        var devices = new[] { new Device { Page = 1, Usage = 6, Target = window } };
        if (!RegisterRawInputDevices(devices, 1, (uint)Marshal.SizeOf<Device>())) throw new Win32Exception();
    }
    // Translation belongs to the host's keyboard-layout policy; device identity comes from RAWINPUTHEADER.
    public void Process(nint lParam, Func<ushort, ushort, string?> translate)
    {
        uint size = 0;
        var headerSize = (uint)Marshal.SizeOf<Header>();
        if (GetRawInputData(lParam, 0x10000003, 0, ref size, headerSize) == uint.MaxValue) throw new Win32Exception();
        var memory = Marshal.AllocHGlobal(checked((int)size));
        try
        {
            if (GetRawInputData(lParam, 0x10000003, memory, ref size, headerSize) == uint.MaxValue) throw new Win32Exception();
            var header = Marshal.PtrToStructure<Header>(memory);
            if (header.Type != 1 || size < headerSize + Marshal.SizeOf<Keyboard>()) return;
            var key = Marshal.PtrToStructure<Keyboard>(memory + (int)headerSize);
            if ((key.Flags & 1) != 0) return;
            var text = translate(key.VirtualKey, key.MakeCode);
            if (text != null) scanner.Feed(header.Handle.ToString(), text);
        }
        finally { Marshal.FreeHGlobal(memory); }
    }
    public void Dispose()
    {
        var devices = new[] { new Device { Page = 1, Usage = 6, Flags = 1 } };
        if (!RegisterRawInputDevices(devices, 1, (uint)Marshal.SizeOf<Device>())) throw new Win32Exception();
    }
    [StructLayout(LayoutKind.Sequential)] private struct Device { public ushort Page, Usage; public uint Flags; public nint Target; }
    [StructLayout(LayoutKind.Sequential)] private struct Header { public uint Type, Size; public nint Handle, WParam; }
    [StructLayout(LayoutKind.Sequential)] private struct Keyboard { public ushort MakeCode, Flags, Reserved, VirtualKey; public uint Message, Extra; }
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterRawInputDevices(Device[] devices, uint count, uint size);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint GetRawInputData(nint input, uint command, nint data, ref uint size, uint headerSize);
}
