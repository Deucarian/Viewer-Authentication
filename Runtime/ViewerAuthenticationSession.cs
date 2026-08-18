using System;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.API.Authentication;
using Deucarian.Session;
using Deucarian.Session.APIIntegration;

namespace Deucarian.ViewerAuthentication
{
    /// <summary>
    /// Default viewer authentication composition backed by Deucarian Session
    /// and its API integration.
    /// </summary>
    public sealed class ViewerAuthenticationSession :
        IViewerAuthenticationSession
    {
        /// <summary>
        /// Creates an in-memory viewer authentication session.
        /// </summary>
        /// <param name="refreshService">
        /// Optional backend-specific refresh adapter. API refresh-before-use is
        /// enabled by default only when this service is supplied.
        /// </param>
        /// <param name="expiryLeeway">
        /// Optional threshold used to classify a token as expiring.
        /// </param>
        /// <param name="refreshFailurePolicy">
        /// Policy applied by Session when refresh fails.
        /// </param>
        /// <param name="refreshBeforeApiRequests">
        /// Allows a composition with a refresh service to opt out of automatic
        /// refresh-before-API behavior while keeping explicit refresh enabled.
        /// </param>
        public ViewerAuthenticationSession(
            ISessionRefreshService refreshService = null,
            TimeSpan? expiryLeeway = null,
            SessionRefreshFailurePolicy refreshFailurePolicy =
                SessionRefreshFailurePolicy.PreserveSession,
            bool refreshBeforeApiRequests = true)
            : this(
                refreshService,
                expiryLeeway,
                refreshFailurePolicy,
                refreshBeforeApiRequests,
                null)
        {
        }

        /// <summary>
        /// Creates the default transient in-memory viewer authentication
        /// composition.
        /// </summary>
        public static ViewerAuthenticationSession CreateTransient(
            ISessionRefreshService refreshService = null,
            TimeSpan? expiryLeeway = null,
            SessionRefreshFailurePolicy refreshFailurePolicy =
                SessionRefreshFailurePolicy.PreserveSession,
            bool refreshBeforeApiRequests = true)
        {
            return new ViewerAuthenticationSession(
                refreshService,
                expiryLeeway,
                refreshFailurePolicy,
                refreshBeforeApiRequests);
        }

        internal ViewerAuthenticationSession(
            ISessionRefreshService refreshService,
            TimeSpan? expiryLeeway,
            SessionRefreshFailurePolicy refreshFailurePolicy,
            bool refreshBeforeApiRequests,
            Func<DateTimeOffset> utcNowProvider)
        {
            CanRefresh = refreshService != null;
            SessionService = new SessionService(
                new InMemorySessionStore(),
                refreshService,
                expiryLeeway,
                refreshFailurePolicy,
                utcNowProvider);
            ApiAuthProvider = new SessionAuthProvider(
                SessionService,
                CanRefresh && refreshBeforeApiRequests);
        }

        /// <inheritdoc />
        public ISessionService SessionService { get; }

        /// <inheritdoc />
        public IApiAuthProvider ApiAuthProvider { get; }

        /// <inheritdoc />
        public string AccessToken
        {
            get
            {
                return SessionService.CurrentSession == null
                    ? null
                    : SessionService.CurrentSession.AccessToken;
            }
        }

        /// <inheritdoc />
        public bool CanRefresh { get; }

        /// <inheritdoc />
        public ViewerAuthenticationStatusSnapshot Status
        {
            get
            {
                return ViewerAuthenticationStatusSnapshot.Create(
                    SessionService,
                    CanRefresh);
            }
        }

        /// <inheritdoc />
        public Task<SessionResult> ReplaceAccessTokenAsync(
            string accessToken,
            DateTimeOffset? expiresAtUtc = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (!ViewerAccessTokenInput.TryNormalize(
                    accessToken,
                    out string normalized))
            {
                return Task.FromResult(
                    SessionResult.Failed(
                        ViewerAccessTokenInput.InvalidCode,
                        ViewerAccessTokenInput.InvalidMessage));
            }

            return SessionService.ReplaceAccessTokenAsync(
                normalized,
                expiresAtUtc,
                cancellationToken);
        }

        /// <inheritdoc />
        public Task<SessionResult> RefreshAsync(
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return SessionService.RefreshAsync(cancellationToken);
        }

        /// <inheritdoc />
        public Task<SessionResult> ClearAsync(
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return SessionService.LogoutAsync(cancellationToken);
        }
    }
}
