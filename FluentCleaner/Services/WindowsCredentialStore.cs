using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace FluentCleaner.Services;

/// <summary>Stores per-user API credentials in the Windows Credential Manager.</summary>
internal static class WindowsCredentialStore
{
    private const uint GenericCredential = 1;
    private const uint PersistLocalMachine = 2;
    private const int ErrorNotFound = 1168;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        public IntPtr TargetName;
        public IntPtr Comment;
        public long LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public IntPtr TargetAlias;
        public IntPtr UserName;
    }

    [DllImport("Advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref NativeCredential credential, uint flags);

    [DllImport("Advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);

    [DllImport("Advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target, uint type, uint flags);

    [DllImport("Advapi32.dll", EntryPoint = "CredFree")]
    private static extern void CredFree(IntPtr buffer);

    public static string? Read(string target)
    {
        if (!CredRead(target, GenericCredential, 0, out var pointer))
            return null;

        try
        {
            var credential = Marshal.PtrToStructure<NativeCredential>(pointer);
            if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0 || credential.CredentialBlobSize > 5120)
                return null;

            var bytes = new byte[credential.CredentialBlobSize];
            try
            {
                Marshal.Copy(credential.CredentialBlob, bytes, 0, bytes.Length);
                return Encoding.UTF8.GetString(bytes);
            }
            finally
            {
                Array.Clear(bytes);
            }
        }
        finally
        {
            CredFree(pointer);
        }
    }

    public static bool Write(string target, string secret)
    {
        var secretBytes = Encoding.UTF8.GetBytes(secret);
        if (secretBytes.Length is 0 or > 5120)
        {
            Array.Clear(secretBytes);
            throw new ArgumentOutOfRangeException(nameof(secret), "Credential must be between 1 and 5120 bytes.");
        }

        IntPtr targetPointer = IntPtr.Zero;
        IntPtr userPointer = IntPtr.Zero;
        IntPtr blobPointer = IntPtr.Zero;
        try
        {
            targetPointer = Marshal.StringToHGlobalUni(target);
            userPointer = Marshal.StringToHGlobalUni("Groq API key");
            blobPointer = Marshal.AllocHGlobal(secretBytes.Length);
            Marshal.Copy(secretBytes, 0, blobPointer, secretBytes.Length);

            var credential = new NativeCredential
            {
                Type = GenericCredential,
                TargetName = targetPointer,
                CredentialBlobSize = (uint)secretBytes.Length,
                CredentialBlob = blobPointer,
                Persist = PersistLocalMachine,
                UserName = userPointer
            };

            return CredWrite(ref credential, 0);
        }
        finally
        {
            Array.Clear(secretBytes);
            if (blobPointer != IntPtr.Zero)
            {
                for (var i = 0; i < secret.Length * sizeof(char); i++) Marshal.WriteByte(blobPointer, i, 0);
                Marshal.FreeHGlobal(blobPointer);
            }
            if (targetPointer != IntPtr.Zero) Marshal.FreeHGlobal(targetPointer);
            if (userPointer != IntPtr.Zero) Marshal.FreeHGlobal(userPointer);
        }
    }

    public static void Delete(string target)
    {
        if (CredDelete(target, GenericCredential, 0)) return;
        var error = Marshal.GetLastWin32Error();
        if (error != ErrorNotFound)
            throw new Win32Exception(error, "Could not remove the Groq API credential from Windows Credential Manager.");
    }
}
