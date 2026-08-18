using System;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.Session;
using NUnit.Framework;

namespace Deucarian.ViewerAuthentication.Tests
{
    public sealed class ViewerAuthenticationTargetRegistryTests
    {
        [Test]
        public void RegistrationIsExplicitDiscoverableAndIdempotentlyDisposable()
        {
            string id = "target-" + Guid.NewGuid().ToString("N");
            ViewerAuthenticationSession session =
                ViewerAuthenticationSession.CreateTransient();
            var acquisition = new StubAcquisitionProvider();
            var validation = new StubValidationProvider();
            IDisposable registration =
                ViewerAuthenticationTargetRegistry.Register(
                    id,
                    "Test Viewer",
                    session,
                    acquisition,
                    validation);

            Assert.That(
                ViewerAuthenticationTargetRegistry.TryGet(
                    id,
                    out ViewerAuthenticationTarget target),
                Is.True);
            Assert.That(target.DisplayName, Is.EqualTo("Test Viewer"));
            Assert.That(target.Session, Is.SameAs(session));
            Assert.That(target.AcquisitionProvider, Is.SameAs(acquisition));
            Assert.That(target.ValidationProvider, Is.SameAs(validation));

            registration.Dispose();
            registration.Dispose();
            Assert.That(
                ViewerAuthenticationTargetRegistry.TryGet(id, out target),
                Is.False);
        }

        [Test]
        public void DuplicateLiveIdIsRejectedWithoutReplacingTarget()
        {
            string id = "target-" + Guid.NewGuid().ToString("N");
            IDisposable registration =
                ViewerAuthenticationTargetRegistry.Register(
                    id,
                    "First",
                    ViewerAuthenticationSession.CreateTransient());
            try
            {
                Assert.Throws<InvalidOperationException>(() =>
                    ViewerAuthenticationTargetRegistry.Register(
                        id,
                        "Second",
                        ViewerAuthenticationSession.CreateTransient()));
                Assert.That(
                    ViewerAuthenticationTargetRegistry.TryGet(
                        id,
                        out ViewerAuthenticationTarget target),
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
            ViewerAuthenticationSession session =
                ViewerAuthenticationSession.CreateTransient();
            int changeCount = 0;
            void OnTargetsChanged()
            {
                changeCount++;
            }

            ViewerAuthenticationTargetRegistry.TargetsChanged +=
                OnTargetsChanged;
            IDisposable registration = null;
            try
            {
                registration = ViewerAuthenticationTargetRegistry.Register(
                    id,
                    "Observable",
                    session);
                Assert.That(changeCount, Is.EqualTo(1));

                await session.ReplaceAccessTokenAsync("first-token");
                Assert.That(changeCount, Is.EqualTo(2));

                registration.Dispose();
                Assert.That(changeCount, Is.EqualTo(3));
                await session.ReplaceAccessTokenAsync("second-token");
                Assert.That(changeCount, Is.EqualTo(3));
            }
            finally
            {
                if (registration != null)
                {
                    registration.Dispose();
                }

                ViewerAuthenticationTargetRegistry.TargetsChanged -=
                    OnTargetsChanged;
            }
        }

        private sealed class StubAcquisitionProvider :
            IViewerAuthenticationAcquisitionProvider
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
            IViewerAuthenticationValidationProvider
        {
            public string DisplayName
            {
                get { return "Validate Test Token"; }
            }

            public Task<ViewerAuthenticationValidationResult> ValidateAsync(
                ISessionService sessionService,
                CancellationToken cancellationToken =
                    default(CancellationToken))
            {
                return Task.FromResult(
                    ViewerAuthenticationValidationResult.Verified());
            }
        }
    }
}
