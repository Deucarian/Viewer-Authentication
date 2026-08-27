using System;

namespace Deucarian.Authentication
{
    /// <summary>
    /// Stable, non-secret identity for one persisted authentication session.
    /// It deliberately excludes transient UI or integration registration IDs.
    /// </summary>
    public sealed class AuthenticationPersistenceIdentity :
        IEquatable<AuthenticationPersistenceIdentity>
    {
        public AuthenticationPersistenceIdentity(
            string serviceId,
            string environmentId,
            string authority,
            string clientId,
            string accountId = null)
        {
            ServiceId = NormalizeRequired(serviceId, nameof(serviceId));
            EnvironmentId = NormalizeRequired(
                environmentId,
                nameof(environmentId));
            Authority = NormalizeRequired(authority, nameof(authority));
            ClientId = NormalizeRequired(clientId, nameof(clientId));
            AccountId = NormalizeOptional(accountId);
        }

        public string ServiceId { get; }
        public string EnvironmentId { get; }
        public string Authority { get; }
        public string ClientId { get; }
        public string AccountId { get; }

        /// <summary>Canonical identity input for hashing and secure storage.</summary>
        public string StableKey =>
            ServiceId + "\n" + EnvironmentId + "\n" + Authority + "\n" +
            ClientId + "\n" + AccountId;

        public bool Equals(AuthenticationPersistenceIdentity other)
        {
            return other != null &&
                   string.Equals(StableKey, other.StableKey,
                       StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as AuthenticationPersistenceIdentity);
        }

        public override int GetHashCode()
        {
            return StableKey.GetHashCode();
        }

        private static string NormalizeRequired(
            string value,
            string parameterName)
        {
            string normalized = NormalizeOptional(value);
            if (normalized.Length == 0)
            {
                throw new ArgumentException(
                    "A stable authentication identity value is required.",
                    parameterName);
            }

            return normalized;
        }

        private static string NormalizeOptional(string value)
        {
            return value?.Trim().ToLowerInvariant() ?? string.Empty;
        }
    }
}
