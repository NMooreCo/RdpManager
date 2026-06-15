namespace RdpManager.CredentialManagement
{
    using System;
    using System.Runtime.InteropServices;

    public static class CredentialManager
    {
        // Constants for the Credential Type and Persistence
        private const int CRED_TYPE_GENERIC = 1;
        private const int CRED_TYPE_DOMAIN_PASSWORD = 2; // For Windows Credentials
        private const int CRED_PERSIST_LOCAL_MACHINE = 2; // Persist locally

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CREDENTIAL
        {
            public int Flags;
            public int Type;
            public string TargetName;
            public string Comment;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
            public int CredentialBlobSize;
            public IntPtr CredentialBlob;
            public int Persist;
            public int AttributeCount;
            public IntPtr Attributes;
            public string TargetAlias;
            public string UserName;
        }

        [DllImport("Advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CredWrite([In] ref CREDENTIAL Credential, [In] uint Flags);

        [DllImport("advapi32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredDelete(string TargetName, uint Type, uint Flags);

        public static bool SaveWindowsCredentials(string target, string username, string password)
        {
            byte[] byteArray = System.Text.Encoding.Unicode.GetBytes(password);
            IntPtr passwordPtr = Marshal.AllocCoTaskMem(byteArray.Length);
            Marshal.Copy(byteArray, 0, passwordPtr, byteArray.Length);

            try
            {
                var cred = new CREDENTIAL
                {
                    Type = CRED_TYPE_DOMAIN_PASSWORD,
                    TargetName = target,
                    CredentialBlobSize = byteArray.Length,
                    CredentialBlob = passwordPtr,
                    Persist = CRED_PERSIST_LOCAL_MACHINE,
                    AttributeCount = 0,
                    Attributes = IntPtr.Zero,
                    TargetAlias = null,
                    Comment = null,
                    UserName = username,
                };

                bool result = CredWrite(ref cred, 0);
                if (!result)
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new Exception($"CredWrite failed with error code {error}");
                }
                return result;
            }
            finally
            {
                // Free the allocated memory
                Marshal.FreeCoTaskMem(passwordPtr);
            }
        }
    }

}
