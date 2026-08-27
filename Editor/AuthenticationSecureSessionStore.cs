using System;
using System.IO;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.Session;
using UnityEngine;

namespace Deucarian.Authentication.Editor
{
    /// <summary>Protects local authentication payloads using platform security.</summary>
    public interface IAuthenticationSecretProtector
    {
        bool IsAvailable { get; }
        byte[] Protect(byte[] plaintext, byte[] entropy);
        byte[] Unprotect(byte[] ciphertext, byte[] entropy);
    }

    /// <summary>
    /// Encrypted, atomic Editor session storage keyed by stable service identity.
    /// No token material is written to Unity settings or preferences.
    /// </summary>
    public sealed class AuthenticationSecureSessionStore : ISessionStore
    {
        private readonly string path;
        private readonly byte[] entropy;
        private readonly IAuthenticationSecretProtector protector;

        public static bool IsPlatformProtectionAvailable =>
            AuthenticationPlatformSecretProtector.Instance.IsAvailable;

        public AuthenticationSecureSessionStore(
            AuthenticationPersistenceIdentity identity)
            : this(
                identity,
                ResolveDefaultDirectory(),
                AuthenticationPlatformSecretProtector.Instance)
        {
        }

        internal AuthenticationSecureSessionStore(
            AuthenticationPersistenceIdentity identity,
            string directory,
            IAuthenticationSecretProtector secretProtector)
        {
            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }

            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException(
                    "A secure session directory is required.",
                    nameof(directory));
            }

            protector = secretProtector ??
                throw new ArgumentNullException(nameof(secretProtector));
            entropy = Hash(identity.StableKey);
            path = Path.Combine(directory, ToHex(entropy) + ".session");
        }

        public Task<SessionData> LoadAsync(
            CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(path))
            {
                return Task.FromResult<SessionData>(null);
            }

            EnsureAvailable();
            byte[] ciphertext = File.ReadAllBytes(path);
            byte[] plaintext = null;
            try
            {
                plaintext = protector.Unprotect(ciphertext, entropy);
                string json = Encoding.UTF8.GetString(plaintext);
                SecureSessionPayload payload =
                    JsonUtility.FromJson<SecureSessionPayload>(json);
                if (payload == null ||
                    !SessionData.IsValidAccessToken(payload.accessToken))
                {
                    throw new InvalidDataException(
                        "The protected authentication session is invalid.");
                }

                DateTimeOffset? expiry = null;
                if (!string.IsNullOrWhiteSpace(payload.expiresAtUtc) &&
                    DateTimeOffset.TryParse(
                        payload.expiresAtUtc,
                        null,
                        System.Globalization.DateTimeStyles.RoundtripKind,
                        out DateTimeOffset parsed))
                {
                    expiry = parsed.ToUniversalTime();
                }

                return Task.FromResult(new SessionData(
                    payload.accessToken,
                    payload.refreshToken,
                    expiry));
            }
            finally
            {
                Clear(ciphertext);
                Clear(plaintext);
            }
        }

        public Task SaveAsync(
            SessionData session,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            EnsureAvailable();
            string directory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);
            var payload = new SecureSessionPayload
            {
                accessToken = session.AccessToken,
                refreshToken = session.RefreshToken,
                expiresAtUtc = session.ExpiresAtUtc?.ToUniversalTime()
                    .ToString("O")
            };
            byte[] plaintext = Encoding.UTF8.GetBytes(
                JsonUtility.ToJson(payload));
            byte[] ciphertext = null;
            string temporaryPath = path + ".tmp-" +
                Guid.NewGuid().ToString("N");
            try
            {
                ciphertext = protector.Protect(plaintext, entropy);
                // The store owns this directory. Recreate it immediately
                // before the atomic write so external Library cleanup cannot
                // create a create/protect/write race.
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(temporaryPath, ciphertext);
                if (File.Exists(path))
                {
                    File.Replace(temporaryPath, path, null);
                }
                else
                {
                    File.Move(temporaryPath, path);
                }
            }
            finally
            {
                Clear(plaintext);
                Clear(ciphertext);
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }

            return Task.CompletedTask;
        }

        public Task ClearAsync(
            CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return Task.CompletedTask;
        }

        internal bool Exists => File.Exists(path);
        internal string StoragePath => path;

        private void EnsureAvailable()
        {
            if (!protector.IsAvailable)
            {
                throw new PlatformNotSupportedException(
                    "Secure authentication persistence is unavailable on " +
                    "this Editor platform.");
            }
        }

        private static string ResolveDefaultDirectory()
        {
            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "Library",
                "Deucarian",
                "Authentication",
                "Sessions"));
        }

        private static byte[] Hash(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return sha.ComputeHash(Encoding.UTF8.GetBytes(value));
            }
        }

        private static string ToHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
            {
                builder.Append(bytes[i].ToString("x2"));
            }

            return builder.ToString();
        }

        private static void Clear(byte[] bytes)
        {
            if (bytes != null)
            {
                Array.Clear(bytes, 0, bytes.Length);
            }
        }

        [Serializable]
        private sealed class SecureSessionPayload
        {
            public string accessToken;
            public string refreshToken;
            public string expiresAtUtc;
        }
    }

    internal sealed class AuthenticationPlatformSecretProtector :
        IAuthenticationSecretProtector
    {
        internal static readonly AuthenticationPlatformSecretProtector Instance =
            new AuthenticationPlatformSecretProtector();

        public bool IsAvailable
        {
            get
            {
#if UNITY_EDITOR_WIN
                return true;
#else
                return false;
#endif
            }
        }

        public byte[] Protect(byte[] plaintext, byte[] entropy)
        {
#if UNITY_EDITOR_WIN
            return WindowsDataProtection.Protect(plaintext, entropy);
#else
            throw new PlatformNotSupportedException();
#endif
        }

        public byte[] Unprotect(byte[] ciphertext, byte[] entropy)
        {
#if UNITY_EDITOR_WIN
            return WindowsDataProtection.Unprotect(ciphertext, entropy);
#else
            throw new PlatformNotSupportedException();
#endif
        }

#if UNITY_EDITOR_WIN
        private static class WindowsDataProtection
        {
            private const int UiForbidden = 0x1;

            internal static byte[] Protect(byte[] input, byte[] entropy)
            {
                return Transform(input, entropy, protect: true);
            }

            internal static byte[] Unprotect(byte[] input, byte[] entropy)
            {
                return Transform(input, entropy, protect: false);
            }

            private static byte[] Transform(
                byte[] input,
                byte[] entropy,
                bool protect)
            {
                DataBlob inputBlob = CreateBlob(input);
                DataBlob entropyBlob = CreateBlob(entropy);
                DataBlob outputBlob = default(DataBlob);
                try
                {
                    bool succeeded = protect
                        ? CryptProtectData(
                            ref inputBlob,
                            null,
                            ref entropyBlob,
                            IntPtr.Zero,
                            IntPtr.Zero,
                            UiForbidden,
                            out outputBlob)
                        : CryptUnprotectData(
                            ref inputBlob,
                            IntPtr.Zero,
                            ref entropyBlob,
                            IntPtr.Zero,
                            IntPtr.Zero,
                            UiForbidden,
                            out outputBlob);
                    if (!succeeded)
                    {
                        throw new CryptographicException(
                            Marshal.GetLastWin32Error());
                    }

                    byte[] output = new byte[outputBlob.Length];
                    Marshal.Copy(outputBlob.Data, output, 0, output.Length);
                    return output;
                }
                finally
                {
                    FreeInput(ref inputBlob);
                    FreeInput(ref entropyBlob);
                    if (outputBlob.Data != IntPtr.Zero)
                    {
                        LocalFree(outputBlob.Data);
                    }
                }
            }

            private static DataBlob CreateBlob(byte[] value)
            {
                if (value == null || value.Length == 0)
                {
                    return default(DataBlob);
                }

                var blob = new DataBlob
                {
                    Length = value.Length,
                    Data = Marshal.AllocHGlobal(value.Length)
                };
                Marshal.Copy(value, 0, blob.Data, value.Length);
                return blob;
            }

            private static void FreeInput(ref DataBlob blob)
            {
                if (blob.Data == IntPtr.Zero)
                {
                    return;
                }

                for (int i = 0; i < blob.Length; i++)
                {
                    Marshal.WriteByte(blob.Data, i, 0);
                }

                Marshal.FreeHGlobal(blob.Data);
                blob.Data = IntPtr.Zero;
                blob.Length = 0;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct DataBlob
            {
                internal int Length;
                internal IntPtr Data;
            }

            [DllImport("crypt32.dll", SetLastError = true,
                CharSet = CharSet.Unicode)]
            private static extern bool CryptProtectData(
                ref DataBlob input,
                string description,
                ref DataBlob optionalEntropy,
                IntPtr reserved,
                IntPtr prompt,
                int flags,
                out DataBlob output);

            [DllImport("crypt32.dll", SetLastError = true,
                CharSet = CharSet.Unicode)]
            private static extern bool CryptUnprotectData(
                ref DataBlob input,
                IntPtr description,
                ref DataBlob optionalEntropy,
                IntPtr reserved,
                IntPtr prompt,
                int flags,
                out DataBlob output);

            [DllImport("kernel32.dll")]
            private static extern IntPtr LocalFree(IntPtr memory);
        }
#endif
    }
}
