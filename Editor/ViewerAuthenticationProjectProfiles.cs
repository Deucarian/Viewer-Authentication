namespace Deucarian.ViewerAuthentication.Editor
{
    /// <summary>
    /// Credential-free project profile discovery shared by the window's live
    /// and Edit Mode workspaces.
    /// </summary>
    internal sealed class ViewerAuthenticationProjectProfiles
    {
        private ViewerAuthenticationProjectProfiles(
            ViewerAuthenticationEndpointProvider acquisitionProvider,
            ViewerAuthenticationEndpointValidationProvider validationProvider)
        {
            AcquisitionProvider = acquisitionProvider;
            ValidationProvider = validationProvider;
        }

        internal ViewerAuthenticationEndpointProvider AcquisitionProvider
        {
            get;
        }

        internal ViewerAuthenticationEndpointValidationProvider
            ValidationProvider
        {
            get;
        }

        internal static ViewerAuthenticationProjectProfiles Discover()
        {
            ViewerAuthenticationEndpointProviderFactory.TryCreateFromResources(
                out ViewerAuthenticationEndpointProvider acquisition);
            ViewerAuthenticationValidationProviderFactory.TryCreateFromResources(
                out ViewerAuthenticationEndpointValidationProvider validation);
            return new ViewerAuthenticationProjectProfiles(
                acquisition,
                validation);
        }

        internal static ViewerAuthenticationProjectProfiles CreateForTests(
            ViewerAuthenticationEndpointProvider acquisitionProvider,
            ViewerAuthenticationEndpointValidationProvider validationProvider)
        {
            return new ViewerAuthenticationProjectProfiles(
                acquisitionProvider,
                validationProvider);
        }
    }
}
