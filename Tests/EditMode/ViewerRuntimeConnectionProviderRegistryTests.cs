using System;
using Deucarian.API.Core;
using NUnit.Framework;

namespace Deucarian.ViewerAuthentication.Tests
{
    public sealed class ViewerRuntimeConnectionProviderRegistryTests
    {
        [Test]
        public void NoProviderLeavesConsumerFallbackAvailable()
        {
            ViewerRuntimeConnectionResolution resolution =
                ViewerRuntimeConnectionProviderRegistry.Resolve();

            Assert.That(
                resolution.Status,
                Is.EqualTo(ViewerRuntimeConnectionResolutionStatus.None));
            Assert.That(resolution.Connection, Is.Null);
        }

        [Test]
        public void SoleProviderResolvesAuthoritativeComposition()
        {
            var session = ViewerAuthenticationSession.CreateTransient();
            IApiClient client = ApiClientFactory.CreateDefault();
            var lifetime = new TrackingDisposable();
            var expected = new ViewerRuntimeConnection(
                "stable-viewer",
                session,
                client,
                "https://api.example.test/v2",
                new[] { "https://assets.example.test/path-is-normalized" },
                lifetime);
            IDisposable registration =
                ViewerRuntimeConnectionProviderRegistry.Register(
                    new StubProvider("provider-a", expected));
            try
            {
                ViewerRuntimeConnectionResolution resolution =
                    ViewerRuntimeConnectionProviderRegistry.Resolve();

                Assert.That(
                    resolution.Status,
                    Is.EqualTo(ViewerRuntimeConnectionResolutionStatus.Resolved));
                Assert.That(resolution.Connection, Is.SameAs(expected));
                Assert.That(
                    resolution.Connection.AuthenticatedOrigins,
                    Is.EquivalentTo(new[]
                    {
                        "https://api.example.test",
                        "https://assets.example.test"
                    }));

                resolution.Connection.Dispose();
                Assert.That(lifetime.Disposed, Is.True);
            }
            finally
            {
                registration.Dispose();
            }
        }

        [Test]
        public void RegisteredProviderFailureDoesNotFallBack()
        {
            IDisposable registration =
                ViewerRuntimeConnectionProviderRegistry.Register(
                    new StubProvider("provider-failed", "Unavailable."));
            try
            {
                ViewerRuntimeConnectionResolution resolution =
                    ViewerRuntimeConnectionProviderRegistry.Resolve();

                Assert.That(
                    resolution.Status,
                    Is.EqualTo(ViewerRuntimeConnectionResolutionStatus.Failed));
                Assert.That(resolution.Connection, Is.Null);
                Assert.That(resolution.Message, Is.EqualTo("Unavailable."));
            }
            finally
            {
                registration.Dispose();
            }
        }

        [Test]
        public void MultipleProvidersFailClosedWithoutCreatingEither()
        {
            var first = new StubProvider("provider-a", "unused");
            var second = new StubProvider("provider-b", "unused");
            IDisposable firstRegistration =
                ViewerRuntimeConnectionProviderRegistry.Register(first);
            IDisposable secondRegistration =
                ViewerRuntimeConnectionProviderRegistry.Register(second);
            try
            {
                ViewerRuntimeConnectionResolution resolution =
                    ViewerRuntimeConnectionProviderRegistry.Resolve();

                Assert.That(
                    resolution.Status,
                    Is.EqualTo(ViewerRuntimeConnectionResolutionStatus.Ambiguous));
                Assert.That(first.CreateCount, Is.Zero);
                Assert.That(second.CreateCount, Is.Zero);
            }
            finally
            {
                secondRegistration.Dispose();
                firstRegistration.Dispose();
            }
        }

        private sealed class StubProvider : IViewerRuntimeConnectionProvider
        {
            private readonly ViewerRuntimeConnection connection;
            private readonly string error;

            internal StubProvider(
                string id,
                ViewerRuntimeConnection connection)
            {
                Id = id;
                this.connection = connection;
            }

            internal StubProvider(string id, string error)
            {
                Id = id;
                this.error = error;
            }

            public string Id { get; }

            internal int CreateCount { get; private set; }

            public bool TryCreate(
                out ViewerRuntimeConnection created,
                out string message)
            {
                CreateCount++;
                created = connection;
                message = error;
                return created != null;
            }
        }

        private sealed class TrackingDisposable : IDisposable
        {
            internal bool Disposed { get; private set; }

            public void Dispose()
            {
                Disposed = true;
            }
        }
    }
}
