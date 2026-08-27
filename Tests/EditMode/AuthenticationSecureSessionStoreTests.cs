using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.Authentication.Editor;
using Deucarian.Session;
using NUnit.Framework;

namespace Deucarian.Authentication.Tests
{
    public sealed class AuthenticationSecureSessionStoreTests
    {
        [Test]
        public async Task RoundTripPersistsAccessRefreshAndExpiryWithoutPlaintext()
        {
            string directory = CreateTestDirectory();
            var protector = new ReversibleTestProtector();
            var store = new AuthenticationSecureSessionStore(
                CreateIdentity(),
                directory,
                protector);
            try
            {
                DateTimeOffset expiry = DateTimeOffset.UtcNow.AddHours(1);
                await store.SaveAsync(new SessionData(
                    "access-secret",
                    "refresh-secret",
                    expiry));
                var reopened = new AuthenticationSecureSessionStore(
                    CreateIdentity(),
                    directory,
                    protector);
                SessionData restored = await reopened.LoadAsync();

                Assert.That(restored.AccessToken, Is.EqualTo("access-secret"));
                Assert.That(restored.RefreshToken, Is.EqualTo("refresh-secret"));
                Assert.That(restored.ExpiresAtUtc, Is.EqualTo(expiry));
                string diskText = Encoding.UTF8.GetString(
                    File.ReadAllBytes(store.StoragePath));
                Assert.That(diskText, Does.Not.Contain("access-secret"));
                Assert.That(diskText, Does.Not.Contain("refresh-secret"));
            }
            finally
            {
                DeleteTestDirectory(directory);
            }
        }

        [Test]
        public async Task ClearRemovesTheProtectedSession()
        {
            string directory = CreateTestDirectory();
            var store = new AuthenticationSecureSessionStore(
                CreateIdentity(),
                directory,
                new ReversibleTestProtector());
            try
            {
                await store.SaveAsync(new SessionData("access-secret"));
                await store.ClearAsync();

                Assert.That(store.Exists, Is.False);
                Assert.That(await store.LoadAsync(), Is.Null);
            }
            finally
            {
                DeleteTestDirectory(directory);
            }
        }

        [Test]
        public void StableIdentityRejectsTransientMissingComponents()
        {
            Assert.Throws<ArgumentException>(() =>
                new AuthenticationPersistenceIdentity(
                    "simultria.api-v2",
                    string.Empty,
                    "https://auth.simultria.invalid",
                    "unity-editor"));
        }

        private static string CreateTestDirectory()
        {
            return Path.GetFullPath(Path.Combine(
                "Temp",
                "AuthTests",
                Guid.NewGuid().ToString("N")));
        }

        private static AuthenticationPersistenceIdentity CreateIdentity()
        {
            return new AuthenticationPersistenceIdentity(
                "simultria.api-v2",
                "simultria.development",
                "https://auth.simultria.invalid",
                "unity-editor",
                "developer@example.invalid");
        }

        private static void DeleteTestDirectory(string directory)
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        private sealed class ReversibleTestProtector :
            IAuthenticationSecretProtector
        {
            public bool IsAvailable => true;

            public byte[] Protect(byte[] plaintext, byte[] entropy)
            {
                return Transform(plaintext, entropy);
            }

            public byte[] Unprotect(byte[] ciphertext, byte[] entropy)
            {
                return Transform(ciphertext, entropy);
            }

            private static byte[] Transform(byte[] input, byte[] entropy)
            {
                byte[] output = new byte[input.Length];
                for (int i = 0; i < input.Length; i++)
                {
                    output[i] = (byte)(input[i] ^ entropy[i % entropy.Length] ^
                        0xA5);
                }

                return output;
            }
        }
    }
}
