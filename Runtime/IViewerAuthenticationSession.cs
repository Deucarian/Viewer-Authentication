using System;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.API.Authentication;
using Deucarian.Session;

namespace Deucarian.ViewerAuthentication
{
    /// <summary>
    /// Viewer-facing facade over the authoritative Deucarian session.
    /// </summary>
    public interface IViewerAuthenticationSession : IViewerAccessTokenSource
    {
        /// <summary>Gets the authoritative session service.</summary>
        ISessionService SessionService { get; }

        /// <summary>
        /// Gets the API auth provider backed by the same live session.
        /// </summary>
        IApiAuthProvider ApiAuthProvider { get; }

        /// <summary>
        /// Gets whether a refresh service was supplied to this composition.
        /// </summary>
        bool CanRefresh { get; }

        /// <summary>Gets a token-free status snapshot.</summary>
        ViewerAuthenticationStatusSnapshot Status { get; }

        /// <summary>
        /// Normalizes and replaces the active access token.
        /// </summary>
        Task<SessionResult> ReplaceAccessTokenAsync(
            string accessToken,
            DateTimeOffset? expiresAtUtc = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>Refreshes through the configured session refresh service.</summary>
        Task<SessionResult> RefreshAsync(
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>Clears the active authentication session.</summary>
        Task<SessionResult> ClearAsync(
            CancellationToken cancellationToken = default(CancellationToken));
    }
}
