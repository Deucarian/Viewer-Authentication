using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.API;
using Deucarian.API.Core;
using Deucarian.Session;
using Deucarian.Session.APIIntegration;

namespace Deucarian.Authentication
{
    /// <summary>
    /// Generic interactive acquisition provider backed by a credential-free
    /// Session API token-endpoint profile.
    /// </summary>
    public sealed class AuthenticationEndpointProvider :
        IInteractiveAuthenticationAcquisitionProvider
    {
        private readonly SessionTokenEndpointConfig config;
        private readonly SessionTokenEndpointLoginService loginService;
        private readonly IReadOnlyList<AuthenticationInputDescriptor>
            inputDescriptors;

        /// <summary>Creates a provider from an API client and profile asset.</summary>
        public AuthenticationEndpointProvider(
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
        public AuthenticationEndpointProvider(
            IApiClient apiClient,
            SessionTokenEndpointConfig endpointConfig)
        {
            if (apiClient == null)
            {
                throw new ArgumentNullException(nameof(apiClient));
            }

            config = endpointConfig ??
                throw new ArgumentNullException(nameof(endpointConfig));
            loginService = new SessionTokenEndpointLoginService(
                apiClient,
                config);
            inputDescriptors = CreateDescriptors(config.InputDefinitions);
        }

        /// <inheritdoc />
        public string DisplayName
        {
            get { return "Get New Token"; }
        }

        /// <inheritdoc />
        public IReadOnlyList<AuthenticationInputDescriptor>
            InputDescriptors
        {
            get { return inputDescriptors; }
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
        public Task<SessionResult> AcquireAsync(
            ISessionService sessionService,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return AcquireAsync(
                sessionService,
                new AuthenticationInputValues(null),
                cancellationToken);
        }

        /// <inheritdoc />
        public async Task<SessionResult> AcquireAsync(
            ISessionService sessionService,
            AuthenticationInputValues inputValues,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (sessionService == null)
            {
                throw new ArgumentNullException(nameof(sessionService));
            }

            AuthenticationInputValues effectiveInputValues =
                inputValues ?? new AuthenticationInputValues(null);
            using (effectiveInputValues)
            using (var endpointValues = new SessionTokenEndpointInputValues())
            {
                IReadOnlyList<SessionTokenEndpointInputDefinition> definitions =
                    config.InputDefinitions;
                for (int i = 0; i < definitions.Count; i++)
                {
                    SessionTokenEndpointInputDefinition definition =
                        definitions[i];
                    if (definition != null &&
                        effectiveInputValues.TryGetValue(
                            definition.Key,
                            out string value))
                    {
                        endpointValues.Set(definition.Key, value);
                    }
                }

                return await sessionService.LoginAsync(
                    endpointValues,
                    loginService,
                    cancellationToken);
            }
        }

        private static IReadOnlyList<AuthenticationInputDescriptor>
            CreateDescriptors(
                IReadOnlyList<SessionTokenEndpointInputDefinition> definitions)
        {
            var descriptors = new List<AuthenticationInputDescriptor>();
            if (definitions != null)
            {
                for (int i = 0; i < definitions.Count; i++)
                {
                    SessionTokenEndpointInputDefinition definition =
                        definitions[i];
                    if (definition != null)
                    {
                        descriptors.Add(
                            new AuthenticationInputDescriptor(
                                definition.Key,
                                definition.DisplayName,
                                definition.IsSecret,
                                definition.IsRequired));
                    }
                }
            }

            return descriptors.AsReadOnly();
        }
    }
}
