using System;
using Deucarian.API.Core;
using Deucarian.Session.APIIntegration;
using UnityEngine;

namespace Deucarian.ViewerAuthentication
{
    /// <summary>
    /// Resolves the conventional credential-free endpoint profile and creates
    /// the shared acquisition provider used by viewer compositions.
    /// </summary>
    public static class ViewerAuthenticationEndpointProviderFactory
    {
        /// <summary>
        /// Conventional Resources path for a project's token endpoint profile.
        /// </summary>
        public const string DefaultProfileResourcePath =
            "Deucarian/ViewerAuthenticationTokenEndpointProfile";

        /// <summary>Creates a provider from an explicitly assigned profile.</summary>
        public static ViewerAuthenticationEndpointProvider Create(
            SessionTokenEndpointProfile profile,
            IApiClient apiClient = null)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            return new ViewerAuthenticationEndpointProvider(
                apiClient ?? ApiClientFactory.CreateDefault(),
                profile);
        }

        /// <summary>
        /// Attempts to load the conventional profile and create a provider. No
        /// message is logged when the optional profile is absent or invalid.
        /// </summary>
        public static bool TryCreateFromResources(
            out ViewerAuthenticationEndpointProvider provider,
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
