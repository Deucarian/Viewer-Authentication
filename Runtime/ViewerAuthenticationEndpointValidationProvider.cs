using System;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.API;
using Deucarian.API.Core;
using Deucarian.Session;
using Deucarian.Session.APIIntegration;

namespace Deucarian.ViewerAuthentication
{
    /// <summary>
    /// Generic validation provider backed by a separate bearer-authenticated
    /// token endpoint profile. HTTP 401/403 is distinguished from transport,
    /// server, configuration, and response failures by Session API Integration.
    /// </summary>
    public sealed class ViewerAuthenticationEndpointValidationProvider :
        IViewerAuthenticationValidationProvider
    {
        private readonly SessionTokenEndpointConfig config;
        private readonly IApiClient apiClient;

        /// <summary>Creates a provider from an API client and profile asset.</summary>
        public ViewerAuthenticationEndpointValidationProvider(
            IApiClient apiClient,
            SessionTokenEndpointProfile profile)
            : this(
                apiClient,
                profile == null
                    ? throw new ArgumentNullException(nameof(profile))
                    : profile.CreateConfig())
        {
        }

        /// <summary>Creates a provider from an immutable endpoint config.</summary>
        public ViewerAuthenticationEndpointValidationProvider(
            IApiClient apiClient,
            SessionTokenEndpointConfig endpointConfig)
        {
            if (apiClient == null)
            {
                throw new ArgumentNullException(nameof(apiClient));
            }

            config = endpointConfig ??
                throw new ArgumentNullException(nameof(endpointConfig));
            if (!config.UseCurrentAccessTokenAsBearer)
            {
                throw new ArgumentException(
                    "A validation endpoint must use the current access token as bearer authentication.",
                    nameof(endpointConfig));
            }

            this.apiClient = apiClient;
        }

        /// <inheritdoc />
        public string DisplayName
        {
            get { return "Server validation"; }
        }

        /// <summary>Gets the credential-free configured endpoint template.</summary>
        public string EndpointTemplate
        {
            get { return config.EndpointTemplate; }
        }

        /// <summary>Gets the configured HTTP method.</summary>
        public HttpMethod Method
        {
            get { return config.Method; }
        }

        /// <inheritdoc />
        public async Task<ViewerAuthenticationValidationResult> ValidateAsync(
            ISessionService sessionService,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            SessionData current = sessionService?.CurrentSession;
            if (current == null)
            {
                return ViewerAuthenticationValidationResult.Inconclusive();
            }

            try
            {
                var validationService =
                    new SessionTokenEndpointRefreshService(apiClient, config);
                SessionResult result = await validationService.RefreshAsync(
                    current,
                    cancellationToken);
                if (result == null || result.IsFailure || result.Session == null)
                {
                    if (string.Equals(
                            result?.Error?.Code,
                            SessionTokenEndpointErrorCodes
                                .AuthenticationRejected,
                            StringComparison.Ordinal))
                    {
                        return ViewerAuthenticationValidationResult.Rejected();
                    }

                    return ViewerAuthenticationValidationResult.Inconclusive();
                }

                SessionResult applied =
                    await sessionService.ReplaceAccessTokenAsync(
                        result.Session.AccessToken,
                        result.Session.ExpiresAtUtc,
                        cancellationToken);
                if (applied == null || applied.IsFailure)
                {
                    return ViewerAuthenticationValidationResult.Inconclusive();
                }

                return ViewerAuthenticationValidationResult.Verified(
                    applied.Session?.ExpiresAtUtc);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return ViewerAuthenticationValidationResult.Inconclusive();
            }
        }

    }
}
