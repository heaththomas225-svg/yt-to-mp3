using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

// Runs a program (a test) on a hidden Windows desktop of its own: its windows never show on your screen and never take the focus
// from what you are doing (a game, a video). Its output comes through here as usual, and its exit code is passed on.
//   RunHidden.exe Test.exe [arguments...]
static class RunHidden
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct STARTUPINFO { public int cb; public string lpReserved, lpDesktop, lpTitle; public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags; public short wShowWindow, cbReserved2; public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError; }
    [StructLayout(LayoutKind.Sequential)] struct PROCESS_INFORMATION { public IntPtr hProcess, hThread; public int dwProcessId, dwThreadId; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern IntPtr CreateDesktop(string name, IntPtr dev, IntPtr mode, int flags, uint access, IntPtr sa);
    [DllImport("user32.dll", SetLastError = true)] static extern bool CloseDesktop(IntPtr h);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool CreateProcess(string app, StringBuilder cmd, IntPtr pa, IntPtr ta, bool inherit, int flags, IntPtr env, string dir, ref STARTUPINFO si, out PROCESS_INFORMATION pi);
    [DllImport("kernel32.dll")] static extern uint WaitForSingleObject(IntPtr h, uint ms);
    [DllImport("kernel32.dll")] static extern bool GetExitCodeProcess(IntPtr h, out uint code);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll")] static extern IntPtr GetStdHandle(int n);

    static int Main(string[] args)
    {
        if (args.Length == 0) { Console.Error.WriteLine("RunHidden.exe Program.exe [arguments]"); return 2; }
        const uint GENERIC_ALL = 0x10000000; IntPtr desk = CreateDesktop("PurphexTests", IntPtr.Zero, IntPtr.Zero, 0, GENERIC_ALL, IntPtr.Zero);
        if (desk == IntPtr.Zero) { Console.Error.WriteLine("Could not make the hidden desktop: " + new Win32Exception(Marshal.GetLastWin32Error()).Message); return 3; }
        var cmd = new StringBuilder(); foreach (var a in args) { if (cmd.Length > 0) cmd.Append(' '); cmd.Append(a.IndexOf(' ') >= 0 ? "\"" + a + "\"" : a); }
        var si = new STARTUPINFO { cb = Marshal.SizeOf(typeof(STARTUPINFO)), lpDesktop = "PurphexTests", dwFlags = 0x100 /* use the std handles */, hStdInput = GetStdHandle(-10), hStdOutput = GetStdHandle(-11), hStdError = GetStdHandle(-12) };
        PROCESS_INFORMATION pi;
        Environment.SetEnvironmentVariable("PURPHEX_MUTE", "1");   // everything started here is a test: it never makes a sound on the speakers
        if (!CreateProcess(null, cmd, IntPtr.Zero, IntPtr.Zero, true, 0, IntPtr.Zero, Environment.CurrentDirectory, ref si, out pi)) { Console.Error.WriteLine("Could not start " + args[0] + ": " + new Win32Exception(Marshal.GetLastWin32Error()).Message); CloseDesktop(desk); return 4; }
        WaitForSingleObject(pi.hProcess, 0xFFFFFFFF); uint code; GetExitCodeProcess(pi.hProcess, out code);
        CloseHandle(pi.hThread); CloseHandle(pi.hProcess); CloseDesktop(desk); return (int)code;
    }
}
