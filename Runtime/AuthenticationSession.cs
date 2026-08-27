using System;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.API.Authentication;
using Deucarian.Session;
using Deucarian.Session.APIIntegration;

namespace Deucarian.Authentication
{
    /// <summary>
    /// Default authentication composition backed by Deucarian Session
    /// and its API integration.
    /// </summary>
    public sealed class AuthenticationSession :
        IAuthenticationSession
    {
        /// <summary>
        /// Creates an in-memory authentication session.
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
        public AuthenticationSession(
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
                new InMemorySessionStore(),
                null)
        {
        }

        /// <summary>
        /// Creates an authentication session backed by the supplied store.
        /// Editor callers should use a platform-protected implementation.
        /// </summary>
        public AuthenticationSession(
            ISessionStore sessionStore,
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
                sessionStore,
                null)
        {
        }

        /// <summary>
        /// Creates the default transient in-memory authentication
        /// composition.
        /// </summary>
        public static AuthenticationSession CreateTransient(
            ISessionRefreshService refreshService = null,
            TimeSpan? expiryLeeway = null,
            SessionRefreshFailurePolicy refreshFailurePolicy =
                SessionRefreshFailurePolicy.PreserveSession,
            bool refreshBeforeApiRequests = true)
        {
            return new AuthenticationSession(
                refreshService,
                expiryLeeway,
                refreshFailurePolicy,
                refreshBeforeApiRequests,
                new InMemorySessionStore(),
                null);
        }

        internal AuthenticationSession(
            ISessionRefreshService refreshService,
            TimeSpan? expiryLeeway,
            SessionRefreshFailurePolicy refreshFailurePolicy,
            bool refreshBeforeApiRequests,
            ISessionStore sessionStore,
            Func<DateTimeOffset> utcNowProvider)
        {
            CanRefresh = refreshService != null;
            SessionService = new SessionService(
                sessionStore ?? throw new ArgumentNullException(
                    nameof(sessionStore)),
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
        public AuthenticationStatusSnapshot Status
        {
            get
            {
                return AuthenticationStatusSnapshot.Create(
                    SessionService,
                    CanRefresh);
            }
        }

        /// <inheritdoc />
        public Task<SessionResult> RestoreAsync(
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return SessionService.RestoreAsync(cancellationToken);
        }

        /// <inheritdoc />
        public Task<SessionResult> ApplyPersistedSessionAsync(
            SessionData session,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            return SessionService.LoginAsync(
                session,
                PersistedSessionLoginService.Instance,
                cancellationToken);
        }

        /// <inheritdoc />
        public Task<SessionResult> ReplaceAccessTokenAsync(
            string accessToken,
            DateTimeOffset? expiresAtUtc = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (!AccessTokenInput.TryNormalize(
                    accessToken,
                    out string normalized))
            {
                return Task.FromResult(
                    SessionResult.Failed(
                        AccessTokenInput.InvalidCode,
                        AccessTokenInput.InvalidMessage));
            }

            DateTimeOffset? effectiveExpiry = expiresAtUtc;
            if (!effectiveExpiry.HasValue &&
                SessionAccessTokenExpiryResolver.TryResolveJwtExpiry(
                    normalized,
                    out DateTimeOffset jwtExpiry))
            {
                effectiveExpiry = jwtExpiry;
            }

            return SessionService.ReplaceAccessTokenAsync(
                normalized,
                effectiveExpiry,
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

        private sealed class PersistedSessionLoginService :
            ISessionLoginService<SessionData>
        {
            internal static readonly PersistedSessionLoginService Instance =
                new PersistedSessionLoginService();

            public Task<SessionResult> LoginAsync(
                SessionData request,
                CancellationToken cancellationToken =
                    default(CancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(SessionResult.Success(request));
            }
        }
    }
}
