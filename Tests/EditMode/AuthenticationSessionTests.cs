using System;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.Session;
using NUnit.Framework;

namespace Deucarian.Authentication.Tests
{
    public sealed class AuthenticationSessionTests
    {
        [Test]
        public async Task TransientSessionNormalizesBearerAndNeverStoresPrefix()
        {
            AuthenticationSession session =
                AuthenticationSession.CreateTransient();

            SessionResult result = await session.ReplaceAccessTokenAsync(
                "  Bearer test-token  ");

            Assert.That(result.Succeeded, Is.True);
            Assert.That(session.AccessToken, Is.EqualTo("test-token"));
            Assert.That(session.Status.Status,
                Is.EqualTo(AuthenticationStatus.ExpiryUnknown));
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
            AuthenticationSession session =
                AuthenticationSession.CreateTransient();

            SessionResult result =
                await session.ReplaceAccessTokenAsync(value);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code,
                Is.EqualTo(AccessTokenInput.InvalidCode));
            Assert.That(session.Status.Status,
                Is.EqualTo(AuthenticationStatus.Missing));
        }

        [Test]
        public async Task StatusDistinguishesActiveExpiringAndExpired()
        {
            AuthenticationSession session =
                AuthenticationSession.CreateTransient(
                    expiryLeeway: TimeSpan.FromMinutes(2));

            await session.ReplaceAccessTokenAsync(
                "active-token",
                DateTimeOffset.UtcNow.AddHours(1));
            Assert.That(session.Status.Status,
                Is.EqualTo(AuthenticationStatus.Active));

            await session.ReplaceAccessTokenAsync(
                "expiring-token",
                DateTimeOffset.UtcNow.AddSeconds(30));
            Assert.That(session.Status.Status,
                Is.EqualTo(AuthenticationStatus.Expiring));

            await session.ReplaceAccessTokenAsync(
                "expired-token",
                DateTimeOffset.UtcNow.AddMinutes(-1));
            Assert.That(session.Status.Status,
                Is.EqualTo(AuthenticationStatus.Expired));
        }

        [Test]
        public async Task ApiProviderRefreshesOnlyWhenRefreshServiceWasSupplied()
        {
            var refresh = new RecordingRefreshService();
            AuthenticationSession session =
                AuthenticationSession.CreateTransient(
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
            AuthenticationSession session =
                AuthenticationSession.CreateTransient();
            await session.ReplaceAccessTokenAsync("temporary-token");

            SessionResult result = await session.ClearAsync();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(session.AccessToken, Is.Null);
            Assert.That(session.Status.Status,
                Is.EqualTo(AuthenticationStatus.Missing));
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
