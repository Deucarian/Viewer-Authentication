using System;
using System.IO;
using System.Threading.Tasks;
using Deucarian.Authentication.Editor;
using Deucarian.Session;
using NUnit.Framework;

namespace Deucarian.Authentication.Tests
{
    public sealed class AuthenticationRememberedSessionIdentityTests
    {
        private const string CompositionFingerprintA =
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string CompositionFingerprintB =
            "baaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        [Test]
        public async Task ExactRecreatedIdentityRestoresThroughEveryApplyPath()
        {
            var harness = new RememberedSessionHarness();
            try
            {
                await harness.RememberAsync(
                    CreateIdentity(CompositionFingerprintA));
                harness.RecreateTarget(
                    CreateIdentity(CompositionFingerprintA));

                Assert.That(
                    harness.Settings.HasRememberedAccessTokenFor(
                        harness.Target),
                    Is.True);
                Assert.That(
                    harness.Settings.TryGetRememberedSessionFor(
                        harness.Target,
                        out SessionData manualSession),
                    Is.True);
                manualSession = null;

                Assert.That(
                    AuthenticationRememberedTokenFacade.TryGet(
                        harness.Settings,
                        harness.TargetId,
                        out string facadeToken),
                    Is.True);
                facadeToken = null;

                Assert.That(
                    await AuthenticationAutoApply
                        .TryApplyRememberedTokenAsync(harness.Settings),
                    Is.True);
                Assert.That(
                    harness.Target.Session.Status.HasAccessToken,
                    Is.True);
            }
            finally
            {
                harness.Dispose();
            }
        }

        [Test]
        public async Task ChangedCompositionRejectsEveryRestorationPath()
        {
            var harness = new RememberedSessionHarness();
            try
            {
                await harness.RememberAsync(
                    CreateIdentity(CompositionFingerprintA));
                harness.RecreateTarget(
                    CreateIdentity(CompositionFingerprintB));

                Assert.That(
                    harness.Settings.HasRememberedAccessTokenFor(
                        harness.Target),
                    Is.False);
                Assert.That(
                    harness.Settings.TryGetRememberedSessionFor(
                        harness.Target,
                        out SessionData rejectedSession),
                    Is.False);
                rejectedSession = null;

                Assert.That(
                    AuthenticationRememberedTokenFacade.TryGet(
                        harness.Settings,
                        harness.TargetId,
                        out string rejectedToken),
                    Is.False);
                rejectedToken = null;

                Assert.That(
                    harness.Settings.TryRebindRememberedTokenOwner(
                        harness.TargetId,
                        harness.TargetId),
                    Is.False);
                Assert.That(
                    await AuthenticationAutoApply
                        .TryApplyRememberedTokenAsync(harness.Settings),
                    Is.False);
                Assert.That(
                    harness.Target.Session.Status.HasAccessToken,
                    Is.False);
            }
            finally
            {
                harness.Dispose();
            }
        }

        private static AuthenticationPersistenceIdentity CreateIdentity(
            string configurationFingerprint)
        {
            return new AuthenticationPersistenceIdentity(
                "simultria.api-v2",
                "simultria.development",
                "https://api.example.invalid",
                "primary",
                null,
                configurationFingerprint);
        }

        private sealed class RememberedSessionHarness : IDisposable
        {
            private readonly string directory;
            private readonly ReversibleProtector protector =
                new ReversibleProtector();
            private readonly IDisposable settingsScope;
            private IDisposable registration;

            internal RememberedSessionHarness()
            {
                directory = Path.GetFullPath(Path.Combine(
                    "Temp",
                    "AuthenticationRememberedIdentityTests",
                    Guid.NewGuid().ToString("N")));
                TargetId = "remembered-session-" +
                    Guid.NewGuid().ToString("N");
                Settings = AuthenticationLocalSettings.instance;
                settingsScope = Settings.BeginIsolatedTestScope(identity =>
                    new AuthenticationSecureSessionStore(
                        identity,
                        directory,
                        protector));
                Settings.SetRememberAccessToken(true);
                Settings.SetAutoApply(true);
            }

            internal string TargetId { get; }

            internal AuthenticationLocalSettings Settings { get; }

            internal AuthenticationTarget Target { get; private set; }

            internal async Task RememberAsync(
                AuthenticationPersistenceIdentity identity)
            {
                RecreateTarget(identity);
                string accessMaterial = Guid.NewGuid().ToString("N");
                try
                {
                    SessionResult result = await Target.Session
                        .ReplaceAccessTokenAsync(accessMaterial);
                    Assert.That(result.Succeeded, Is.True);
                    Assert.That(Settings.RememberSession(Target), Is.True);
                    await Target.Session.ClearAsync();
                }
                finally
                {
                    accessMaterial = null;
                }
            }

            internal void RecreateTarget(
                AuthenticationPersistenceIdentity identity)
            {
                registration?.Dispose();
                AuthenticationSession session =
                    AuthenticationSession.CreateTransient();
                registration = AuthenticationTargetRegistry.Register(
                    TargetId,
                    "Remembered Session Test",
                    session,
                    null,
                    null,
                    identity);
                Assert.That(
                    AuthenticationTargetRegistry.TryGet(
                        TargetId,
                        out AuthenticationTarget target),
                    Is.True);
                Target = target;
            }

            public void Dispose()
            {
                try
                {
                    try
                    {
                        Target?.Session?.ClearAsync()
                            .GetAwaiter()
                            .GetResult();
                    }
                    catch
                    {
                        // Test cleanup must not disclose session material.
                    }

                    registration?.Dispose();
                }
                finally
                {
                    registration = null;
                    Target = null;
                    try
                    {
                        settingsScope?.Dispose();
                    }
                    finally
                    {
                        if (Directory.Exists(directory))
                        {
                            Directory.Delete(directory, true);
                        }
                    }
                }
            }
        }

        private sealed class ReversibleProtector :
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
                    output[i] = (byte)(
                        input[i] ^ entropy[i % entropy.Length] ^ 0xA5);
                }

                return output;
            }
        }
    }
}
