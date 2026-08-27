namespace Deucarian.Authentication.Editor
{
    /// <summary>
    /// Explicit project providers shared by the window's live and Edit Mode
    /// workspaces. Import never performs Resources-based discovery.
    /// </summary>
    internal sealed class AuthenticationProjectProfiles
    {
        private AuthenticationProjectProfiles(
            AuthenticationEndpointProvider acquisitionProvider,
            AuthenticationEndpointValidationProvider validationProvider)
        {
            AcquisitionProvider = acquisitionProvider;
            ValidationProvider = validationProvider;
        }

        internal AuthenticationEndpointProvider AcquisitionProvider
        {
            get;
        }

        internal AuthenticationEndpointValidationProvider
            ValidationProvider
        {
            get;
        }

        internal static AuthenticationProjectProfiles Discover()
        {
            return new AuthenticationProjectProfiles(null, null);
        }

        internal static AuthenticationProjectProfiles CreateForTests(
            AuthenticationEndpointProvider acquisitionProvider,
            AuthenticationEndpointValidationProvider validationProvider)
        {
            return new AuthenticationProjectProfiles(
                acquisitionProvider,
                validationProvider);
        }
    }
}
