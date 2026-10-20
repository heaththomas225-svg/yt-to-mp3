// Keeps saved keys and sign-ins encrypted. Windows: the same DPAPI as the Windows build (so files carry over).
// macOS / Linux: AES-256 with an HMAC, using a random key kept in your user folder that only you can read.
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace System.Security.Cryptography
{
    public enum DataProtectionScope { CurrentUser, LocalMachine }
    public static class ProtectedData
    {
        public static byte[] Protect(byte[] data, byte[] entropy, DataProtectionScope scope)
        {
            if (OperatingSystem.IsWindows()) return Dpapi(data, entropy, true);
            var k = Key(); using (var aes = Aes.Create()) { aes.Key = Slice(k, 0); aes.GenerateIV(); byte[] ct; using (var e = aes.CreateEncryptor()) ct = e.TransformFinalBlock(data, 0, data.Length);
                var body = new byte[1 + 16 + ct.Length]; body[0] = 1; Buffer.BlockCopy(aes.IV, 0, body, 1, 16); Buffer.BlockCopy(ct, 0, body, 17, ct.Length);
                byte[] mac; using (var h = new HMACSHA256(Slice(k, 32))) mac = h.ComputeHash(body); var o = new byte[body.Length + 32]; Buffer.BlockCopy(body, 0, o, 0, body.Length); Buffer.BlockCopy(mac, 0, o, body.Length, 32); return o; }
        }
        public static byte[] Unprotect(byte[] data, byte[] entropy, DataProtectionScope scope)
        {
            if (OperatingSystem.IsWindows()) return Dpapi(data, entropy, false);
            if (data == null || data.Length < 1 + 16 + 16 + 32 || data[0] != 1) throw new CryptographicException("not Purphex data");
            var k = Key(); int bl = data.Length - 32; byte[] mac; using (var h = new HMACSHA256(Slice(k, 32))) mac = h.ComputeHash(data, 0, bl);
            if (!CryptographicOperations.FixedTimeEquals(mac, new ReadOnlySpan<byte>(data, bl, 32))) throw new CryptographicException("changed or made on another computer");
            using (var aes = Aes.Create()) { aes.Key = Slice(k, 0); var iv = new byte[16]; Buffer.BlockCopy(data, 1, iv, 0, 16); aes.IV = iv; using (var d = aes.CreateDecryptor()) return d.TransformFinalBlock(data, 17, bl - 17); }
        }
        static byte[] Slice(byte[] k, int at) { var r = new byte[32]; Buffer.BlockCopy(k, at, r, 0, 32); return r; }
        static byte[] key;
        static byte[] Key()
        {
            if (key != null) return key;
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "purphex"); Directory.CreateDirectory(dir);
            var p = Path.Combine(dir, "secret.key");
            if (File.Exists(p)) { var b = File.ReadAllBytes(p); if (b.Length == 64) return key = b; }
            var n = RandomNumberGenerator.GetBytes(64); File.WriteAllBytes(p, n);
            try { File.SetUnixFileMode(p, UnixFileMode.UserRead | UnixFileMode.UserWrite); File.SetUnixFileMode(dir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); } catch { }
            return key = n;
        }

        [StructLayout(LayoutKind.Sequential)] struct Blob { public int Size; public IntPtr Data; }
        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern bool CryptProtectData(ref Blob input, string desc, ref Blob entropy, IntPtr reserved, IntPtr prompt, int flags, ref Blob output);
        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern bool CryptUnprotectData(ref Blob input, IntPtr desc, ref Blob entropy, IntPtr reserved, IntPtr prompt, int flags, ref Blob output);
        [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr p);
        static byte[] Dpapi(byte[] data, byte[] entropy, bool protect)
        {
            var hIn = GCHandle.Alloc(data, GCHandleType.Pinned); var ent = entropy ?? new byte[0]; var hEnt = GCHandle.Alloc(ent, GCHandleType.Pinned);
            try
            {
                var i = new Blob { Size = data.Length, Data = hIn.AddrOfPinnedObject() }; var e = new Blob { Size = ent.Length, Data = hEnt.AddrOfPinnedObject() }; var o = new Blob();
                bool ok = protect ? CryptProtectData(ref i, "", ref e, IntPtr.Zero, IntPtr.Zero, 1, ref o) : CryptUnprotectData(ref i, IntPtr.Zero, ref e, IntPtr.Zero, IntPtr.Zero, 1, ref o);
                if (!ok) throw new CryptographicException(Marshal.GetLastWin32Error());
                var r = new byte[o.Size]; Marshal.Copy(o.Data, r, 0, o.Size); LocalFree(o.Data); return r;
            }
            finally { hIn.Free(); hEnt.Free(); }
        }
    }
}
