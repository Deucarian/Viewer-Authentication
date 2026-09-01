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
            AccountId = NormalizeOptional(accountId, nameof(accountId));
            ConfigurationFingerprint = string.Empty;
        }

        /// <summary>
        /// Creates an identity bound to a stable, non-secret fingerprint of
        /// the complete authentication/API composition.
        /// </summary>
        /// <remarks>
        /// The fingerprint must change whenever any backend selection that can
        /// affect authentication changes. Supply a digest, never raw hosts,
        /// headers, routes, credentials, or other configuration payloads.
        /// </remarks>
        public AuthenticationPersistenceIdentity(
            string serviceId,
            string environmentId,
            string authority,
            string clientId,
            string accountId,
            string configurationFingerprint)
            : this(
                serviceId,
                environmentId,
                authority,
                clientId,
                accountId)
        {
            ConfigurationFingerprint = NormalizeFingerprint(
                configurationFingerprint,
                nameof(configurationFingerprint));
        }

        public string ServiceId { get; }
        public string EnvironmentId { get; }
        public string Authority { get; }
        public string ClientId { get; }
        public string AccountId { get; }

        /// <summary>
        /// Gets the stable, non-secret full-composition fingerprint, or an
        /// empty string for a source-compatible legacy identity.
        /// </summary>
        public string ConfigurationFingerprint { get; }

        /// <summary>Canonical identity input for hashing and secure storage.</summary>
        public string StableKey =>
            ServiceId + "\n" + EnvironmentId + "\n" + Authority + "\n" +
            ClientId + "\n" + AccountId +
            (ConfigurationFingerprint.Length == 0
                ? string.Empty
                : "\n" + ConfigurationFingerprint);

        public bool Equals(AuthenticationPersistenceIdentity other)
        {
            return other != null &&
                   string.Equals(
                       ServiceId,
                       other.ServiceId,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       EnvironmentId,
                       other.EnvironmentId,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       Authority,
                       other.Authority,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       ClientId,
                       other.ClientId,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       AccountId,
                       other.AccountId,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       ConfigurationFingerprint,
                       other.ConfigurationFingerprint,
                       StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as AuthenticationPersistenceIdentity);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + OrdinalHash(ServiceId);
                hash = hash * 31 + OrdinalHash(EnvironmentId);
                hash = hash * 31 + OrdinalHash(Authority);
                hash = hash * 31 + OrdinalHash(ClientId);
                hash = hash * 31 + OrdinalHash(AccountId);
                hash = hash * 31 + OrdinalHash(
                    ConfigurationFingerprint);
                return hash;
            }
        }

        private static string NormalizeRequired(
            string value,
            string parameterName)
        {
            string normalized = NormalizeOptional(value, parameterName);
            if (normalized.Length == 0)
            {
                throw new ArgumentException(
                    "A stable authentication identity value is required.",
                    parameterName);
            }

            return normalized;
        }

        private static string NormalizeOptional(
            string value,
            string parameterName)
        {
            string raw = value ?? string.Empty;
            RejectNewlines(raw, parameterName);
            return raw.Trim().ToLowerInvariant();
        }

        private static string NormalizeFingerprint(
            string value,
            string parameterName)
        {
            string raw = value ?? string.Empty;
            RejectNewlines(raw, parameterName);
            string normalized = raw.Trim();
            if (normalized.Length == 0)
            {
                throw new ArgumentException(
                    "A non-empty configuration fingerprint is required when " +
                    "using the fingerprint-bound identity overload.",
                    parameterName);
            }

            return normalized;
        }

        private static void RejectNewlines(
            string value,
            string parameterName)
        {
            if (value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0)
            {
                throw new ArgumentException(
                    "Authentication identity values cannot contain newlines.",
                    parameterName);
            }
        }

        private static int OrdinalHash(string value)
        {
            return StringComparer.Ordinal.GetHashCode(value);
        }
    }
}
