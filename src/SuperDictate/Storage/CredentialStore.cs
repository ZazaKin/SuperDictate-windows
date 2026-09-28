using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using SuperDictate.Interop;

namespace SuperDictate.Storage;

/// <summary>
/// Generic secrets in Windows Credential Manager, scoped to the current user.
/// Secrets never go into settings.json.
/// </summary>
public static class CredentialStore
{
    /// <summary>Only the self-test redirects it, so it never touches the real key.</summary>
    internal static string AiCleanupTarget { get; set; } = "com.local.superdictate/ai-cleanup";

    /// <summary>Returns null when no credential exists for the target.</summary>
    public static string? Read(string target)
    {
        if (!NativeMethods.CredRead(target, NativeMethods.CRED_TYPE_GENERIC, 0, out var handle))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == NativeMethods.ERROR_NOT_FOUND) return null;
            throw new Win32Exception(error);
        }

        try
        {
            var credential = Marshal.PtrToStructure<NativeMethods.CREDENTIAL>(handle);
            return credential.CredentialBlobSize == 0
                ? string.Empty
                : Marshal.PtrToStringUni(credential.CredentialBlob, (int)credential.CredentialBlobSize / 2);
        }
        finally
        {
            NativeMethods.CredFree(handle);
        }
    }

    public static void Write(string target, string secret)
    {
        // UTF-16 matches what cmdkey and the Credential Manager UI store.
        var bytes = Encoding.Unicode.GetBytes(secret);
        var blob = Marshal.AllocHGlobal(Math.Max(bytes.Length, 1));
        try
        {
            Marshal.Copy(bytes, 0, blob, bytes.Length);
            var credential = new NativeMethods.CREDENTIAL
            {
                Type = NativeMethods.CRED_TYPE_GENERIC,
                TargetName = target,
                CredentialBlobSize = (uint)bytes.Length,
                CredentialBlob = blob,
                Persist = NativeMethods.CRED_PERSIST_LOCAL_MACHINE,
                UserName = Environment.UserName,
            };
            if (!NativeMethods.CredWrite(ref credential, 0))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
        finally
        {
            Marshal.Copy(new byte[bytes.Length], 0, blob, bytes.Length);
            Marshal.FreeHGlobal(blob);
        }
    }

    /// <summary>Deleting a missing credential is not an error.</summary>
    public static void Delete(string target)
    {
        if (!NativeMethods.CredDelete(target, NativeMethods.CRED_TYPE_GENERIC, 0))
        {
            var error = Marshal.GetLastWin32Error();
            if (error != NativeMethods.ERROR_NOT_FOUND) throw new Win32Exception(error);
        }
    }
}
