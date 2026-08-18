using System;

namespace Deucarian.ViewerAuthentication
{
    /// <summary>
    /// Explicitly registered viewer authentication target discoverable by
    /// development tooling.
    /// </summary>
    public sealed class ViewerAuthenticationTarget
    {
        internal ViewerAuthenticationTarget(
            string id,
            string displayName,
            IViewerAuthenticationSession session,
            IViewerAuthenticationAcquisitionProvider acquisitionProvider)
        {
            Id = id;
            DisplayName = displayName;
            Session = session ?? throw new ArgumentNullException(nameof(session));
            AcquisitionProvider = acquisitionProvider;
        }

        /// <summary>Gets the stable target identifier.</summary>
        public string Id { get; }

        /// <summary>Gets the human-readable target name.</summary>
        public string DisplayName { get; }

        /// <summary>Gets the target authentication session.</summary>
        public IViewerAuthenticationSession Session { get; }

        /// <summary>Gets the optional backend-specific acquisition provider.</summary>
        public IViewerAuthenticationAcquisitionProvider AcquisitionProvider
        {
            get;
        }
    }
}
