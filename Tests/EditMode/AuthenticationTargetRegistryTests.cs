using System;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.Session;
using NUnit.Framework;

namespace Deucarian.Authentication.Tests
{
    public sealed class AuthenticationTargetRegistryTests
    {
        [Test]
        public void RegistrationIsExplicitDiscoverableAndIdempotentlyDisposable()
        {
            string id = "target-" + Guid.NewGuid().ToString("N");
            AuthenticationSession session =
                AuthenticationSession.CreateTransient();
            var acquisition = new StubAcquisitionProvider();
            var validation = new StubValidationProvider();
            IDisposable registration =
                AuthenticationTargetRegistry.Register(
                    id,
                    "Test Viewer",
                    session,
                    acquisition,
                    validation);

            Assert.That(
                AuthenticationTargetRegistry.TryGet(
                    id,
                    out AuthenticationTarget target),
                Is.True);
            Assert.That(target.DisplayName, Is.EqualTo("Test Viewer"));
            Assert.That(target.Session, Is.SameAs(session));
            Assert.That(target.AcquisitionProvider, Is.SameAs(acquisition));
            Assert.That(target.ValidationProvider, Is.SameAs(validation));

            registration.Dispose();
            registration.Dispose();
            Assert.That(
                AuthenticationTargetRegistry.TryGet(id, out target),
                Is.False);
        }

        [Test]
        public void DuplicateLiveIdIsRejectedWithoutReplacingTarget()
        {
            string id = "target-" + Guid.NewGuid().ToString("N");
            IDisposable registration =
                AuthenticationTargetRegistry.Register(
                    id,
                    "First",
                    AuthenticationSession.CreateTransient());
            try
            {
                Assert.Throws<InvalidOperationException>(() =>
                    AuthenticationTargetRegistry.Register(
                        id,
                        "Second",
                        AuthenticationSession.CreateTransient()));
                Assert.That(
                    AuthenticationTargetRegistry.TryGet(
                        id,
                        out AuthenticationTarget target),
                    Is.True);
                Assert.That(target.DisplayName, Is.EqualTo("First"));
            }
            finally
            {
                registration.Dispose();
            }
        }

        [Test]
        public async Task RegisteredSessionChangesNotifyUntilRegistrationIsDisposed()
        {
            string id = "target-" + Guid.NewGuid().ToString("N");
            AuthenticationSession session =
                AuthenticationSession.CreateTransient();
            int changeCount = 0;
            int registrationChangeCount = 0;
            void OnTargetsChanged()
            {
                changeCount++;
            }

            void OnRegistrationsChanged()
            {
                registrationChangeCount++;
            }

            AuthenticationTargetRegistry.TargetsChanged +=
                OnTargetsChanged;
            AuthenticationTargetRegistry.RegistrationsChanged +=
                OnRegistrationsChanged;
            IDisposable registration = null;
            try
            {
                registration = AuthenticationTargetRegistry.Register(
                    id,
                    "Observable",
                    session);
                Assert.That(changeCount, Is.EqualTo(1));
                Assert.That(registrationChangeCount, Is.EqualTo(1));

                await session.ReplaceAccessTokenAsync("first-token");
                Assert.That(changeCount, Is.EqualTo(2));
                Assert.That(registrationChangeCount, Is.EqualTo(1));

                registration.Dispose();
                Assert.That(changeCount, Is.EqualTo(3));
                Assert.That(registrationChangeCount, Is.EqualTo(2));
                await session.ReplaceAccessTokenAsync("second-token");
                Assert.That(changeCount, Is.EqualTo(3));
                Assert.That(registrationChangeCount, Is.EqualTo(2));
            }
            finally
            {
                if (registration != null)
                {
                    registration.Dispose();
                }

                AuthenticationTargetRegistry.TargetsChanged -=
                    OnTargetsChanged;
                AuthenticationTargetRegistry.RegistrationsChanged -=
                    OnRegistrationsChanged;
            }
        }

        [Test]
        public void ThrowingObserversCannotOrphanOrWedgeRegistration()
        {
            string id = "target-" + Guid.NewGuid().ToString("N");
            void ThrowOnRegistrationsChanged()
            {
                throw new InvalidOperationException("Observer failed.");
            }

            void ThrowOnTargetsChanged()
            {
                throw new InvalidOperationException("Observer failed.");
            }

            AuthenticationTargetRegistry.RegistrationsChanged +=
                ThrowOnRegistrationsChanged;
            AuthenticationTargetRegistry.TargetsChanged +=
                ThrowOnTargetsChanged;
            IDisposable registration = null;
            try
            {
                Assert.DoesNotThrow(() =>
                    registration = AuthenticationTargetRegistry.Register(
                        id,
                        "Observable",
                        AuthenticationSession.CreateTransient()));
                Assert.That(registration, Is.Not.Null);
                Assert.That(
                    AuthenticationTargetRegistry.TryGet(id, out _),
                    Is.True);

                Assert.DoesNotThrow(() => registration.Dispose());
                Assert.That(
                    AuthenticationTargetRegistry.TryGet(id, out _),
                    Is.False);

                IDisposable replacement = null;
                Assert.DoesNotThrow(() =>
                    replacement = AuthenticationTargetRegistry.Register(
                        id,
                        "Replacement",
                        AuthenticationSession.CreateTransient()));
                replacement.Dispose();
            }
            finally
            {
                registration?.Dispose();
                AuthenticationTargetRegistry.RegistrationsChanged -=
                    ThrowOnRegistrationsChanged;
                AuthenticationTargetRegistry.TargetsChanged -=
                    ThrowOnTargetsChanged;
            }
        }

        private sealed class StubAcquisitionProvider :
            IAuthenticationAcquisitionProvider
        {
            public string DisplayName
            {
                get { return "Get Test Token"; }
            }

            public Task<SessionResult> AcquireAsync(
                ISessionService sessionService,
                CancellationToken cancellationToken =
                    default(CancellationToken))
            {
                return sessionService.ReplaceAccessTokenAsync(
                    "acquired-token",
                    null,
                    cancellationToken);
            }
        }

        private sealed class StubValidationProvider :
            IAuthenticationValidationProvider
        {
            public string DisplayName
            {
                get { return "Validate Test Token"; }
            }

            public Task<AuthenticationValidationResult> ValidateAsync(
                ISessionService sessionService,
                CancellationToken cancellationToken =
                    default(CancellationToken))
            {
                return Task.FromResult(
                    AuthenticationValidationResult.Verified());
            }
        }
    }
}
