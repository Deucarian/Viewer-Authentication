using System;
using Deucarian.Session;

namespace Deucarian.ViewerAuthentication
{
    /// <summary>
    /// Token-free authentication state safe for UI, commands, events, and
    /// diagnostics-style presentation.
    /// </summary>
    public sealed class ViewerAuthenticationStatusSnapshot
    {
        /// <summary>Creates a sanitized status snapshot.</summary>
        public ViewerAuthenticationStatusSnapshot(
            ViewerAuthenticationStatus status,
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
        public ViewerAuthenticationStatus Status { get; }

        /// <summary>
        /// Gets whether a token exists without exposing the token value.
        /// </summary>
        public bool HasAccessToken { get; }

        /// <summary>Gets whether refresh behavior is configured.</summary>
        public bool CanRefresh { get; }

        /// <summary>Gets the known UTC expiry, or null when unknown.</summary>
        public DateTimeOffset? ExpiresAtUtc { get; }

        internal static ViewerAuthenticationStatusSnapshot Create(
            ISessionService sessionService,
            bool canRefresh)
        {
            if (sessionService == null || sessionService.CurrentSession == null)
            {
                return new ViewerAuthenticationStatusSnapshot(
                    ViewerAuthenticationStatus.Missing,
                    false,
                    canRefresh,
                    null);
            }

            SessionData session = sessionService.CurrentSession;
            ViewerAuthenticationStatus status;
            if (sessionService.IsAccessTokenExpired)
            {
                status = ViewerAuthenticationStatus.Expired;
            }
            else if (!session.ExpiresAtUtc.HasValue)
            {
                status = ViewerAuthenticationStatus.ExpiryUnknown;
            }
            else if (sessionService.IsAccessTokenExpiringSoon)
            {
                status = ViewerAuthenticationStatus.Expiring;
            }
            else
            {
                status = ViewerAuthenticationStatus.Active;
            }

            return new ViewerAuthenticationStatusSnapshot(
                status,
                true,
                canRefresh,
                session.ExpiresAtUtc);
        }
    }
}
