using System;

namespace Deucarian.Authentication
{
    /// <summary>
    /// Token-free validation outcome safe for editor presentation. Providers
    /// must never include response bodies, tokens, or credentials in this value.
    /// </summary>
    public sealed class AuthenticationValidationResult
    {
        private AuthenticationValidationResult(
            AuthenticationValidationStatus status,
            DateTimeOffset? expiresAtUtc)
        {
            Status = status;
            ExpiresAtUtc = expiresAtUtc.HasValue
                ? expiresAtUtc.Value.ToUniversalTime()
                : (DateTimeOffset?)null;
        }

        /// <summary>Gets the sanitized validation outcome.</summary>
        public AuthenticationValidationStatus Status { get; }

        /// <summary>
        /// Gets server-returned expiry metadata for the validated token, when
        /// the provider can establish it without exposing the token.
        /// </summary>
        public DateTimeOffset? ExpiresAtUtc { get; }

        /// <summary>Creates a server-verified result.</summary>
        public static AuthenticationValidationResult Verified(
            DateTimeOffset? expiresAtUtc = null)
        {
            return new AuthenticationValidationResult(
                AuthenticationValidationStatus.Verified,
                expiresAtUtc);
        }

        /// <summary>Creates an explicit server-rejection result.</summary>
        public static AuthenticationValidationResult Rejected()
        {
            return new AuthenticationValidationResult(
                AuthenticationValidationStatus.Rejected,
                null);
        }

        /// <summary>
        /// Creates an inconclusive result for transport, configuration, or
        /// response failures that must not be presented as token rejection.
        /// </summary>
        public static AuthenticationValidationResult Inconclusive()
        {
            return new AuthenticationValidationResult(
                AuthenticationValidationStatus.Inconclusive,
                null);
        }
    }
}
