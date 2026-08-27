using System;
using Deucarian.API.Core;
using Deucarian.Session.APIIntegration;

namespace Deucarian.Authentication
{
    /// <summary>
    /// Creates a validation provider from an explicitly assigned profile.
    /// </summary>
    public static class AuthenticationValidationProviderFactory
    {
        /// <summary>Creates a provider from an explicitly assigned profile.</summary>
        public static AuthenticationEndpointValidationProvider Create(
            SessionTokenEndpointProfile profile,
            IApiClient apiClient = null)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            return new AuthenticationEndpointValidationProvider(
                apiClient ?? ApiClientFactory.CreateDefault(),
                profile);
        }
    }
}
