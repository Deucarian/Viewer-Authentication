using System;

namespace Deucarian.ViewerAuthentication
{
    /// <summary>
    /// Token-free validation outcome safe for editor presentation. Providers
    /// must never include response bodies, tokens, or credentials in this value.
    /// </summary>
    public sealed class ViewerAuthenticationValidationResult
    {
        private ViewerAuthenticationValidationResult(
            ViewerAuthenticationValidationStatus status,
            DateTimeOffset? expiresAtUtc)
        {
            Status = status;
            ExpiresAtUtc = expiresAtUtc.HasValue
                ? expiresAtUtc.Value.ToUniversalTime()
                : (DateTimeOffset?)null;
        }

        /// <summary>Gets the sanitized validation outcome.</summary>
        public ViewerAuthenticationValidationStatus Status { get; }

        /// <summary>
        /// Gets server-returned expiry metadata for the validated token, when
        /// the provider can establish it without exposing the token.
        /// </summary>
        public DateTimeOffset? ExpiresAtUtc { get; }

        /// <summary>Creates a server-verified result.</summary>
        public static ViewerAuthenticationValidationResult Verified(
            DateTimeOffset? expiresAtUtc = null)
        {
            return new ViewerAuthenticationValidationResult(
                ViewerAuthenticationValidationStatus.Verified,
                expiresAtUtc);
        }

        /// <summary>Creates an explicit server-rejection result.</summary>
        public static ViewerAuthenticationValidationResult Rejected()
        {
            return new ViewerAuthenticationValidationResult(
                ViewerAuthenticationValidationStatus.Rejected,
                null);
        }

        /// <summary>
        /// Creates an inconclusive result for transport, configuration, or
        /// response failures that must not be presented as token rejection.
        /// </summary>
        public static ViewerAuthenticationValidationResult Inconclusive()
        {
            return new ViewerAuthenticationValidationResult(
                ViewerAuthenticationValidationStatus.Inconclusive,
                null);
        }
    }
}
