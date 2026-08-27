using System;

namespace Deucarian.Authentication
{
    /// <summary>
    /// Explicitly registered authentication target discoverable by
    /// development tooling.
    /// </summary>
    public sealed class AuthenticationTarget
    {
        internal AuthenticationTarget(
            string id,
            string displayName,
            IAuthenticationSession session,
            IAuthenticationAcquisitionProvider acquisitionProvider,
            IAuthenticationValidationProvider validationProvider,
            AuthenticationPersistenceIdentity persistenceIdentity)
        {
            Id = id;
            DisplayName = displayName;
            Session = session ?? throw new ArgumentNullException(nameof(session));
            AcquisitionProvider = acquisitionProvider;
            ValidationProvider = validationProvider;
            PersistenceIdentity = persistenceIdentity;
        }

        /// <summary>Gets the stable target identifier.</summary>
        public string Id { get; }

        /// <summary>Gets the human-readable target name.</summary>
        public string DisplayName { get; }

        /// <summary>Gets the target authentication session.</summary>
        public IAuthenticationSession Session { get; }

        /// <summary>Gets the optional backend-specific acquisition provider.</summary>
        public IAuthenticationAcquisitionProvider AcquisitionProvider
        {
            get;
        }

        /// <summary>Gets the optional server-side validation provider.</summary>
        public IAuthenticationValidationProvider ValidationProvider
        {
            get;
        }

        /// <summary>
        /// Gets the stable identity used for secure persistence, or null when
        /// this target deliberately opts out of persistence.
        /// </summary>
        public AuthenticationPersistenceIdentity PersistenceIdentity { get; }
    }
}
