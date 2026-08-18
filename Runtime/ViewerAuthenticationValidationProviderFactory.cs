using System;
using Deucarian.API.Core;
using Deucarian.Session.APIIntegration;
using UnityEngine;

namespace Deucarian.ViewerAuthentication
{
    /// <summary>
    /// Resolves the optional conventional validation profile used by shared
    /// editor tooling without adding a backend-specific endpoint to the package.
    /// </summary>
    public static class ViewerAuthenticationValidationProviderFactory
    {
        /// <summary>Conventional Resources path for a validation profile.</summary>
        public const string DefaultProfileResourcePath =
            "Deucarian/ViewerAuthenticationTokenValidationEndpointProfile";

        /// <summary>Creates a provider from an explicitly assigned profile.</summary>
        public static ViewerAuthenticationEndpointValidationProvider Create(
            SessionTokenEndpointProfile profile,
            IApiClient apiClient = null)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            return new ViewerAuthenticationEndpointValidationProvider(
                apiClient ?? ApiClientFactory.CreateDefault(),
                profile);
        }

        /// <summary>
        /// Attempts to load the optional conventional profile. Absence or an
        /// invalid configuration remains silent and yields no provider.
        /// </summary>
        public static bool TryCreateFromResources(
            out ViewerAuthenticationEndpointValidationProvider provider,
            IApiClient apiClient = null,
            string resourcePath = DefaultProfileResourcePath)
        {
            provider = null;
            if (string.IsNullOrWhiteSpace(resourcePath))
            {
                return false;
            }

            SessionTokenEndpointProfile profile =
                Resources.Load<SessionTokenEndpointProfile>(
                    resourcePath.Trim());
            if (profile == null)
            {
                return false;
            }

            try
            {
                provider = Create(profile, apiClient);
                return true;
            }
            catch (Exception)
            {
                provider = null;
                return false;
            }
        }
    }
}
