// Who this computer is on macOS and Linux (Windows uses its machine id from the registry).
using System;
using System.Diagnostics;
using System.IO;

static class CrossPc
{
    public static string MachineId()
    {
        try
        {
            if (OperatingSystem.IsLinux()) { foreach (var p in new[] { "/etc/machine-id", "/var/lib/dbus/machine-id" }) if (File.Exists(p)) { var t = File.ReadAllText(p).Trim(); if (t.Length > 0) return t; } }
            if (OperatingSystem.IsMacOS())
            {
                var psi = new ProcessStartInfo("/usr/sbin/ioreg", "-rd1 -c IOPlatformExpertDevice") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
                using (var pr = Process.Start(psi)) { var o = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(3000); int i = o.IndexOf("IOPlatformUUID"); if (i >= 0) { int q1 = o.IndexOf('"', o.IndexOf('=', i)); int q2 = o.IndexOf('"', q1 + 1); if (q1 > 0 && q2 > q1) return o.Substring(q1 + 1, q2 - q1 - 1); } }
            }
        }
        catch { }
        return "";
    }
}
