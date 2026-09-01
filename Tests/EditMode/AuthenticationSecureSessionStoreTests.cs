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

        [Test]
        public void FingerprintBoundIdentityRejectsAMissingFingerprint()
        {
            Assert.Throws<ArgumentException>(() =>
                new AuthenticationPersistenceIdentity(
                    "simultria.api-v2",
                    "simultria.development",
                    "https://auth.example.invalid",
                    "unity-editor",
                    null,
                    string.Empty));
        }

        [Test]
        public void IdentityComponentsRejectStableKeyDelimiterCollisions()
        {
            Assert.Throws<ArgumentException>(() =>
                new AuthenticationPersistenceIdentity(
                    "simultria.api-v2",
                    "simultria.development",
                    "https://auth.example.invalid",
                    "unity-editor",
                    "account-a\nfingerprint-b"));

            var fingerprintBound = new AuthenticationPersistenceIdentity(
                "simultria.api-v2",
                "simultria.development",
                "https://auth.example.invalid",
                "unity-editor",
                "account-a",
                "fingerprint-b");
            Assert.That(
                fingerprintBound.StableKey,
                Does.EndWith("account-a\nfingerprint-b"));
        }

        [Test]
        public void EveryIdentityComponentRejectsCarriageReturnsAndLineFeeds()
        {
            string[] components =
            {
                "simultria.api-v2",
                "simultria.development",
                "https://auth.example.invalid",
                "unity-editor",
                "account-a",
                "fingerprint-a"
            };

            foreach (string delimiter in new[] { "\r", "\n" })
            {
                for (int index = 0; index < components.Length; index++)
                {
                    string[] mutated = (string[])components.Clone();
                    mutated[index] = delimiter + mutated[index];
                    Assert.Throws<ArgumentException>(() =>
                        new AuthenticationPersistenceIdentity(
                            mutated[0],
                            mutated[1],
                            mutated[2],
                            mutated[3],
                            mutated[4],
                            mutated[5]),
                        "Component {0} accepted a line delimiter.",
                        index);
                }
            }
        }

        [Test]
        public void LegacyIdentityStableKeyRemainsSourceCompatible()
        {
            var identity = new AuthenticationPersistenceIdentity(
                " Simultria.Api-V2 ",
                " Simultria.Development ",
                " HTTPS://AUTH.EXAMPLE.INVALID ",
                " Unity-Editor ");

            Assert.That(
                identity.StableKey,
                Is.EqualTo(
                    "simultria.api-v2\nsimultria.development\n" +
                    "https://auth.example.invalid\nunity-editor\n"));
            Assert.That(identity.ConfigurationFingerprint, Is.Empty);
        }

        [Test]
        public async Task ChangedCompositionFingerprintCannotLoadOldSession()
        {
            string directory = CreateTestDirectory();
            var protector = new ReversibleTestProtector();
            var first = new AuthenticationSecureSessionStore(
                CreateIdentity("aaaaaaaaaaaaaaaa"),
                directory,
                protector);
            var changed = new AuthenticationSecureSessionStore(
                CreateIdentity("baaaaaaaaaaaaaaa"),
                directory,
                protector);
            try
            {
                await first.SaveAsync(new SessionData("access-secret"));

                Assert.That(first.Exists, Is.True);
                Assert.That(changed.Exists, Is.False);
                Assert.That(await changed.LoadAsync(), Is.Null);
                Assert.That(changed.StoragePath, Is.Not.EqualTo(first.StoragePath));
            }
            finally
            {
                DeleteTestDirectory(directory);
            }
        }

        [Test]
        public async Task ExactFingerprintReopensAfterDomainReload()
        {
            const string fingerprint = "aaaaaaaaaaaaaaaa";
            string directory = CreateTestDirectory();
            var protector = new ReversibleTestProtector();
            var beforeReload = new AuthenticationSecureSessionStore(
                CreateIdentity(fingerprint),
                directory,
                protector);
            try
            {
                await beforeReload.SaveAsync(
                    new SessionData("access-secret"));

                var afterReload = new AuthenticationSecureSessionStore(
                    CreateIdentity(fingerprint),
                    directory,
                    protector);
                SessionData restored = await afterReload.LoadAsync();

                Assert.That(restored, Is.Not.Null);
                Assert.That(restored.AccessToken, Is.EqualTo("access-secret"));
                Assert.That(
                    afterReload.StoragePath,
                    Is.EqualTo(beforeReload.StoragePath));
            }
            finally
            {
                DeleteTestDirectory(directory);
            }
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

        private static AuthenticationPersistenceIdentity CreateIdentity(
            string configurationFingerprint)
        {
            return new AuthenticationPersistenceIdentity(
                "simultria.api-v2",
                "simultria.development",
                "https://auth.example.invalid",
                "unity-editor",
                "developer@example.invalid",
                configurationFingerprint);
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
