// The converter's helper programs on macOS and Linux: yt-dlp, ffmpeg / ffprobe and Deno.
// A copy already on the computer (Homebrew, apt, ...) is used first; otherwise the right build for this system is downloaded once.
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;

static class CrossTools
{
    public static bool Unix { get { return !OperatingSystem.IsWindows(); } }
    static bool Arm { get { return RuntimeInformation.OSArchitecture == Architecture.Arm64; } }
    public static string Exe(string name) { return Unix ? name : name + ".exe"; }
    public static string YtDlpAsset { get { return OperatingSystem.IsMacOS() ? "yt-dlp_macos" : OperatingSystem.IsLinux() ? (Arm ? "yt-dlp_linux_aarch64" : "yt-dlp_linux") : "yt-dlp.exe"; } }
    public static string DenoAsset { get { return "deno-" + (Arm ? "aarch64" : "x86_64") + (OperatingSystem.IsMacOS() ? "-apple-darwin" : "-unknown-linux-gnu") + ".zip"; } }

    // a program on the PATH or in the usual install folders (apps started from the Dock or a menu do not get the PATH of a terminal)
    public static string Which(string name)
    {
        var dirs = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator);
        foreach (var d in dirs) { try { var p = Path.Combine(d, name); if (d.Length > 0 && File.Exists(p)) return p; } catch { } }
        foreach (var d in new[] { "/opt/homebrew/bin", "/usr/local/bin", "/usr/bin", "/snap/bin", "/home/linuxbrew/.linuxbrew/bin" }) { var p = Path.Combine(d, name); if (File.Exists(p)) return p; }
        return null;
    }
    public static void MakeRunnable(string path)
    {
        if (!Unix || path == null || !File.Exists(path)) return;
        try { File.SetUnixFileMode(path, File.GetUnixFileMode(path) | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute); } catch { }
        if (OperatingSystem.IsMacOS()) Run("/usr/bin/xattr", "-d com.apple.quarantine \"" + path + "\"");   // a downloaded program is otherwise blocked by Gatekeeper
    }
    public static void MakeAllRunnable(string dir) { if (!Unix || !Directory.Exists(dir)) return; foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)) if (Path.GetExtension(f).Length == 0) MakeRunnable(f); }
    static void Run(string exe, string args) { try { using (var p = Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true })) p.WaitForExit(60000); } catch { } }

    // ffmpeg + ffprobe into dir. download(url, file, status) and verify(file, sumsUrl, name) are the converter's own (with progress and checksums).
    public static void InstallFfmpeg(string tools, string dir, Action<string, string, string> download, Action<string, string, string> verify)
    {
        if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
        if (OperatingSystem.IsLinux())
        {
            string asset = Arm ? "ffmpeg-master-latest-linuxarm64-gpl.tar.xz" : "ffmpeg-master-latest-linux64-gpl.tar.xz", tar = Path.Combine(tools, "ffmpeg.tar.xz");
            download("https://github.com/yt-dlp/FFmpeg-Builds/releases/download/latest/" + asset, tar, "Downloading ffmpeg...");
            verify(tar, "https://github.com/yt-dlp/FFmpeg-Builds/releases/download/latest/checksums.sha256", asset);
            Run("/usr/bin/tar", "-xJf \"" + tar + "\" -C \"" + dir + "\""); File.Delete(tar);
        }
        else
        {
            // macOS: the static builds from evermeet.cx (Intel; Apple Silicon runs them through Rosetta)
            foreach (var n in new[] { "ffmpeg", "ffprobe" })
            {
                string zip = Path.Combine(tools, n + ".zip");
                download("https://evermeet.cx/ffmpeg/getrelease/" + n + "/zip", zip, "Downloading " + n + "...");
                ZipFile.ExtractToDirectory(zip, dir, true); File.Delete(zip);
            }
        }
        MakeAllRunnable(dir);
    }
}
