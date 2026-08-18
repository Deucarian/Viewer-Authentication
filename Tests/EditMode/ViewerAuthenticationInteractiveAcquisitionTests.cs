using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.API;
using Deucarian.API.Core;
using Deucarian.API.Models;
using Deucarian.Session;
using Deucarian.Session.APIIntegration;
using Deucarian.ViewerAuthentication.Editor;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Deucarian.ViewerAuthentication.Tests
{
    public sealed class ViewerAuthenticationInteractiveAcquisitionTests
    {
        [Test]
        public async Task EndpointProviderMapsDescriptorsAndReacquiresSession()
        {
            var apiClient = new TokenEndpointApiClient();
            var config = new SessionTokenEndpointConfig(
                "https://viewer-authentication.invalid/token",
                new[]
                {
                    new SessionTokenEndpointInputDefinition(
                        "account",
                        "account",
                        "Account",
                        isRequired: true),
                    new SessionTokenEndpointInputDefinition(
                        "credential",
                        "credential",
                        "Credential",
                        isSecret: true,
                        isRequired: true)
                },
                new SessionTokenEndpointResponseMapping());
            var provider = new ViewerAuthenticationEndpointProvider(
                apiClient,
                config);
            ViewerAuthenticationSession authentication =
                ViewerAuthenticationSession.CreateTransient();
            var inputValues = new ViewerAuthenticationInputValues(
                new[]
                {
                    new KeyValuePair<string, string>(
                        "account",
                        "test-account"),
                    new KeyValuePair<string, string>(
                        "credential",
                        "test-credential")
                });

            SessionResult result = await provider.AcquireAsync(
                authentication.SessionService,
                inputValues,
                CancellationToken.None);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(authentication.AccessToken, Is.EqualTo("test-token"));
            Assert.That(inputValues.IsCleared, Is.True);
            Assert.That(provider.InputDescriptors.Count, Is.EqualTo(2));
            Assert.That(provider.InputDescriptors[1].IsSecret, Is.True);
            Assert.That(provider.InputDescriptors[1].IsRequired, Is.True);
            Assert.That(apiClient.LastRequest, Is.Not.Null);
            Assert.That(
                apiClient.ObservedCredential,
                Is.EqualTo("test-credential"));
            Assert.That(apiClient.LastRequest.Body, Is.Null);
        }

        [Test]
        public void TransientEditorStateClearsSecretFieldsOnDispatch()
        {
            var descriptors = new[]
            {
                new ViewerAuthenticationInputDescriptor(
                    "account",
                    isSecret: false),
                new ViewerAuthenticationInputDescriptor(
                    "credential",
                    isSecret: true)
            };
            var state = new ViewerAuthenticationTransientInputState();
            state.SetValue("account", "test-account");
            state.SetValue("credential", "test-credential");

            ViewerAuthenticationInputValues dispatched =
                state.CreateValues(descriptors);
            state.ClearSecrets(descriptors);

            Assert.That(state.GetValue("account"), Is.EqualTo("test-account"));
            Assert.That(state.GetValue("credential"), Is.Empty);
            Assert.That(
                dispatched.GetValueOrDefault("credential"),
                Is.EqualTo("test-credential"));

            dispatched.Dispose();
            Assert.That(dispatched.IsCleared, Is.True);
            Assert.That(
                dispatched.GetValueOrDefault("credential"),
                Is.Null);
        }

        [Test]
        public void MissingConventionalProfileReturnsFalseWithoutLogging()
        {
            bool created =
                ViewerAuthenticationEndpointProviderFactory
                    .TryCreateFromResources(
                        out ViewerAuthenticationEndpointProvider provider,
                        resourcePath:
                            "Deucarian/Tests/MissingTokenEndpointProfile");

            Assert.That(created, Is.False);
            Assert.That(provider, Is.Null);
        }

        private sealed class TokenEndpointApiClient : IApiClient
        {
            public ApiRequest LastRequest { get; private set; }
            public string ObservedCredential { get; private set; }

            public Task<ApiResult<TResponse>> SendAsync<TResponse>(
                ApiRequest request,
                CancellationToken cancellationToken = default(CancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                LastRequest = request;
                if (request.Body is IDictionary<string, string> body)
                {
                    body.TryGetValue(
                        "credential",
                        out string credential);
                    ObservedCredential = credential;
                }

                var response = new JObject
                {
                    ["access_token"] = "test-token"
                };
                ApiResult<JObject> result = ApiResult<JObject>.Success(
                    response,
                    HttpMethod.POST,
                    200,
                    "https://viewer-authentication.invalid/token",
                    null);
                return Task.FromResult(
                    (ApiResult<TResponse>)(object)result);
            }

            public Task<ApiResult<TResponse>> SendAsync<TResponse>(
                ApiEndpoint endpoint,
                CancellationToken cancellationToken = default(CancellationToken))
            {
                return SendAsync<TResponse>(
                    endpoint.CreateRequest(),
                    cancellationToken);
            }

            public Task<ApiResult<TResponse>> SendAsync<TResponse>(
                ApiEndpoint endpoint,
                object body,
                CancellationToken cancellationToken = default(CancellationToken))
            {
                return SendAsync<TResponse>(
                    endpoint.CreateRequest(body),
                    cancellationToken);
            }

            public Task<ApiResult<TResponse>> GetAsync<TResponse>(
                string endpoint,
                CancellationToken cancellationToken = default(CancellationToken))
            {
                return SendAsync<TResponse>(
                    new ApiRequest(endpoint, HttpMethod.GET),
                    cancellationToken);
            }

            public Task<ApiResult<TResponse>> PostAsync<TResponse>(
                string endpoint,
                object body,
                CancellationToken cancellationToken = default(CancellationToken))
            {
                return SendWithBody<TResponse>(
                    endpoint,
                    HttpMethod.POST,
                    body,
                    cancellationToken);
            }

            public Task<ApiResult<TResponse>> PutAsync<TResponse>(
                string endpoint,
                object body,
                CancellationToken cancellationToken = default(CancellationToken))
            {
                return SendWithBody<TResponse>(
                    endpoint,
                    HttpMethod.PUT,
                    body,
                    cancellationToken);
            }

            public Task<ApiResult<TResponse>> PatchAsync<TResponse>(
                string endpoint,
                object body,
                CancellationToken cancellationToken = default(CancellationToken))
            {
                return SendWithBody<TResponse>(
                    endpoint,
                    HttpMethod.PATCH,
                    body,
                    cancellationToken);
            }

            public Task<ApiResult<TResponse>> DeleteAsync<TResponse>(
                string endpoint,
                CancellationToken cancellationToken = default(CancellationToken))
            {
                return SendAsync<TResponse>(
                    new ApiRequest(endpoint, HttpMethod.DELETE),
                    cancellationToken);
            }

            private Task<ApiResult<TResponse>> SendWithBody<TResponse>(
                string endpoint,
                HttpMethod method,
                object body,
                CancellationToken cancellationToken)
            {
                var request = new ApiRequest(endpoint, method)
                {
                    Body = body
                };
                return SendAsync<TResponse>(request, cancellationToken);
            }
        }
    }
}
