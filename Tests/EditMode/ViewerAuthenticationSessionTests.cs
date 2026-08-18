using System;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.Session;
using NUnit.Framework;

namespace Deucarian.ViewerAuthentication.Tests
{
    public sealed class ViewerAuthenticationSessionTests
    {
        [Test]
        public async Task TransientSessionNormalizesBearerAndNeverStoresPrefix()
        {
            ViewerAuthenticationSession session =
                ViewerAuthenticationSession.CreateTransient();

            SessionResult result = await session.ReplaceAccessTokenAsync(
                "  Bearer test-token  ");

            Assert.That(result.Succeeded, Is.True);
            Assert.That(session.AccessToken, Is.EqualTo("test-token"));
            Assert.That(session.Status.Status,
                Is.EqualTo(ViewerAuthenticationStatus.ExpiryUnknown));
            Assert.That(session.Status.HasAccessToken, Is.True);
            Assert.That(session.CanRefresh, Is.False);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("Bearer ")]
        [TestCase("Bearer\t")]
        [TestCase("token with spaces")]
        public async Task InvalidReplacementDoesNotCreateSession(string value)
        {
            ViewerAuthenticationSession session =
                ViewerAuthenticationSession.CreateTransient();

            SessionResult result =
                await session.ReplaceAccessTokenAsync(value);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code,
                Is.EqualTo(ViewerAccessTokenInput.InvalidCode));
            Assert.That(session.Status.Status,
                Is.EqualTo(ViewerAuthenticationStatus.Missing));
        }

        [Test]
        public async Task StatusDistinguishesActiveExpiringAndExpired()
        {
            ViewerAuthenticationSession session =
                ViewerAuthenticationSession.CreateTransient(
                    expiryLeeway: TimeSpan.FromMinutes(2));

            await session.ReplaceAccessTokenAsync(
                "active-token",
                DateTimeOffset.UtcNow.AddHours(1));
            Assert.That(session.Status.Status,
                Is.EqualTo(ViewerAuthenticationStatus.Active));

            await session.ReplaceAccessTokenAsync(
                "expiring-token",
                DateTimeOffset.UtcNow.AddSeconds(30));
            Assert.That(session.Status.Status,
                Is.EqualTo(ViewerAuthenticationStatus.Expiring));

            await session.ReplaceAccessTokenAsync(
                "expired-token",
                DateTimeOffset.UtcNow.AddMinutes(-1));
            Assert.That(session.Status.Status,
                Is.EqualTo(ViewerAuthenticationStatus.Expired));
        }

        [Test]
        public async Task ApiProviderRefreshesOnlyWhenRefreshServiceWasSupplied()
        {
            var refresh = new RecordingRefreshService();
            ViewerAuthenticationSession session =
                ViewerAuthenticationSession.CreateTransient(
                    refresh,
                    TimeSpan.FromMinutes(2));
            await session.ReplaceAccessTokenAsync(
                "expiring-token",
                DateTimeOffset.UtcNow.AddSeconds(30));

            string token = await session.ApiAuthProvider.GetAccessTokenAsync(
                CancellationToken.None);

            Assert.That(session.CanRefresh, Is.True);
            Assert.That(refresh.CallCount, Is.EqualTo(1));
            Assert.That(token, Is.EqualTo("refreshed-token"));
            Assert.That(session.AccessToken, Is.EqualTo("refreshed-token"));
        }

        [Test]
        public async Task ClearRemovesAccessTokenAndReturnsMissingStatus()
        {
            ViewerAuthenticationSession session =
                ViewerAuthenticationSession.CreateTransient();
            await session.ReplaceAccessTokenAsync("temporary-token");

            SessionResult result = await session.ClearAsync();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(session.AccessToken, Is.Null);
            Assert.That(session.Status.Status,
                Is.EqualTo(ViewerAuthenticationStatus.Missing));
        }

        private sealed class RecordingRefreshService : ISessionRefreshService
        {
            public int CallCount { get; private set; }

            public Task<SessionResult> RefreshAsync(
                SessionData currentSession,
                CancellationToken cancellationToken =
                    default(CancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                CallCount++;
                return Task.FromResult(
                    SessionResult.Success(
                        new SessionData(
                            "refreshed-token",
                            currentSession.RefreshToken,
                            DateTimeOffset.UtcNow.AddHours(1))));
            }
        }
    }
}
