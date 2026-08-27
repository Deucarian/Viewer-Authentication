using System;
using Deucarian.API.Core;
using Deucarian.Session.APIIntegration;

namespace Deucarian.Authentication
{
    /// <summary>
    /// Creates an acquisition provider from an explicitly assigned profile.
    /// </summary>
    public static class AuthenticationEndpointProviderFactory
    {
        /// <summary>Creates a provider from an explicitly assigned profile.</summary>
        public static AuthenticationEndpointProvider Create(
            SessionTokenEndpointProfile profile,
            IApiClient apiClient = null)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            return new AuthenticationEndpointProvider(
                apiClient ?? ApiClientFactory.CreateDefault(),
                profile);
        }
    }
}
