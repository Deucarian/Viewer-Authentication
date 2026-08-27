using System;
using Deucarian.Session;

namespace Deucarian.Authentication
{
    /// <summary>
    /// Token-free authentication state safe for UI, commands, events, and
    /// diagnostics-style presentation.
    /// </summary>
    public sealed class AuthenticationStatusSnapshot
    {
        /// <summary>Creates a sanitized status snapshot.</summary>
        public AuthenticationStatusSnapshot(
            AuthenticationStatus status,
            bool hasAccessToken,
            bool canRefresh,
            DateTimeOffset? expiresAtUtc)
        {
            Status = status;
            HasAccessToken = hasAccessToken;
            CanRefresh = canRefresh;
            ExpiresAtUtc = expiresAtUtc.HasValue
                ? expiresAtUtc.Value.ToUniversalTime()
                : (DateTimeOffset?)null;
        }

        /// <summary>Gets the sanitized lifecycle state.</summary>
        public AuthenticationStatus Status { get; }

        /// <summary>
        /// Gets whether a token exists without exposing the token value.
        /// </summary>
        public bool HasAccessToken { get; }

        /// <summary>Gets whether refresh behavior is configured.</summary>
        public bool CanRefresh { get; }

        /// <summary>Gets the known UTC expiry, or null when unknown.</summary>
        public DateTimeOffset? ExpiresAtUtc { get; }

        internal static AuthenticationStatusSnapshot Create(
            ISessionService sessionService,
            bool canRefresh)
        {
            if (sessionService == null || sessionService.CurrentSession == null)
            {
                return new AuthenticationStatusSnapshot(
                    AuthenticationStatus.Missing,
                    false,
                    canRefresh,
                    null);
            }

            SessionData session = sessionService.CurrentSession;
            AuthenticationStatus status;
            if (sessionService.IsAccessTokenExpired)
            {
                status = AuthenticationStatus.Expired;
            }
            else if (!session.ExpiresAtUtc.HasValue)
            {
                status = AuthenticationStatus.ExpiryUnknown;
            }
            else if (sessionService.IsAccessTokenExpiringSoon)
            {
                status = AuthenticationStatus.Expiring;
            }
            else
            {
                status = AuthenticationStatus.Active;
            }

            return new AuthenticationStatusSnapshot(
                status,
                true,
                canRefresh,
                session.ExpiresAtUtc);
        }
    }
}
