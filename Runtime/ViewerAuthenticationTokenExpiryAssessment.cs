using System;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.Session;
using Deucarian.Session.APIIntegration;

namespace Deucarian.ViewerAuthentication
{
    /// <summary>
    /// Applies locally readable JWT expiry metadata to a viewer session. The
    /// parser itself remains owned by Session API Integration.
    /// </summary>
    public static class ViewerAuthenticationTokenExpiryAssessment
    {
        /// <summary>
        /// Adds expiry metadata only when the current session has none. Returns
        /// false for missing, opaque, malformed, or already-assessed tokens.
        /// </summary>
        public static async Task<bool> TryApplyIfMissingAsync(
            ISessionService sessionService,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            SessionData current = sessionService?.CurrentSession;
            if (current == null || current.ExpiresAtUtc.HasValue ||
                !SessionAccessTokenExpiryResolver.TryResolveJwtExpiry(
                    current.AccessToken,
                    out DateTimeOffset expiration))
            {
                return false;
            }

            SessionResult result = await sessionService.ReplaceAccessTokenAsync(
                current.AccessToken,
                expiration,
                cancellationToken);
            return result != null && result.Succeeded;
        }
    }
}
