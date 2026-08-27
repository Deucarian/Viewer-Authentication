using System;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.API.Authentication;
using Deucarian.Session;

namespace Deucarian.Authentication
{
    /// <summary>
    /// Authentication facade over the authoritative Deucarian session.
    /// </summary>
    public interface IAuthenticationSession : IAccessTokenSource
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
        AuthenticationStatusSnapshot Status { get; }

        /// <summary>Restores a securely persisted session when configured.</summary>
        Task<SessionResult> RestoreAsync(
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>Applies a session restored by an authorized secure store.</summary>
        Task<SessionResult> ApplyPersistedSessionAsync(
            SessionData session,
            CancellationToken cancellationToken = default(CancellationToken));

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
