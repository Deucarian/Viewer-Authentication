using System;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.API;
using Deucarian.API.Core;
using Deucarian.API.Models;
using Deucarian.Session.APIIntegration;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Deucarian.ViewerAuthentication.Tests
{
    public sealed class ViewerAuthenticationValidationTests
    {
        [Test]
        public async Task SuccessfulEndpointProbeIsServerVerified()
        {
            var client = new ValidationApiClient(200);
            var provider = CreateProvider(client);
            ViewerAuthenticationSession session =
                ViewerAuthenticationSession.CreateTransient();
            await session.ReplaceAccessTokenAsync("opaque-test-token");

            ViewerAuthenticationValidationResult result =
                await provider.ValidateAsync(
                    session.SessionService,
                    CancellationToken.None);

            Assert.That(
                result.Status,
                Is.EqualTo(ViewerAuthenticationValidationStatus.Verified));
            Assert.That(client.SawBearerToken, Is.True);
            Assert.That(session.Status.HasAccessToken, Is.True);
            Assert.That(
                session.AccessToken,
                Is.Not.EqualTo("opaque-test-token"));
            Assert.That(session.Status.ExpiresAtUtc, Is.Not.Null);
        }

        [TestCase(401)]
        [TestCase(403)]
        public async Task AuthorizationFailureIsRejectedWithoutClearingSession(
            long statusCode)
        {
            var provider = CreateProvider(
                new ValidationApiClient(statusCode));
            ViewerAuthenticationSession session =
                ViewerAuthenticationSession.CreateTransient();
            await session.ReplaceAccessTokenAsync("opaque-test-token");

            ViewerAuthenticationValidationResult result =
                await provider.ValidateAsync(
                    session.SessionService,
                    CancellationToken.None);

            Assert.That(
                result.Status,
                Is.EqualTo(ViewerAuthenticationValidationStatus.Rejected));
            Assert.That(session.Status.HasAccessToken, Is.True);
        }

        [Test]
        public async Task ServerFailureIsInconclusiveWithoutClearingSession()
        {
            var provider = CreateProvider(
                new ValidationApiClient(500));
            ViewerAuthenticationSession session =
                ViewerAuthenticationSession.CreateTransient();
            await session.ReplaceAccessTokenAsync("opaque-test-token");

            ViewerAuthenticationValidationResult result =
                await provider.ValidateAsync(
                    session.SessionService,
                    CancellationToken.None);

            Assert.That(
                result.Status,
                Is.EqualTo(
                    ViewerAuthenticationValidationStatus.Inconclusive));
            Assert.That(session.Status.HasAccessToken, Is.True);
        }

        [Test]
        public void ValidationProfileMustUseCurrentTokenAsBearer()
        {
            var config = new SessionTokenEndpointConfig(
                "https://viewer-authentication.invalid/validate",
                null,
                new SessionTokenEndpointResponseMapping(),
                HttpMethod.GET,
                useCurrentAccessTokenAsBearer: false);

            Assert.Throws<ArgumentException>(() =>
                new ViewerAuthenticationEndpointValidationProvider(
                    new ValidationApiClient(200),
                    config));
        }

        [Test]
        public void MissingConventionalValidationProfileIsOptional()
        {
            bool created = ViewerAuthenticationValidationProviderFactory
                .TryCreateFromResources(
                    out ViewerAuthenticationEndpointValidationProvider provider,
                    resourcePath:
                        "Deucarian/Tests/MissingTokenValidationProfile");

            Assert.That(created, Is.False);
            Assert.That(provider, Is.Null);
        }

        private static ViewerAuthenticationEndpointValidationProvider
            CreateProvider(IApiClient apiClient)
        {
            var config = new SessionTokenEndpointConfig(
                "https://viewer-authentication.invalid/validate",
                null,
                new SessionTokenEndpointResponseMapping(),
                HttpMethod.GET,
                useCurrentAccessTokenAsBearer: true);
            return new ViewerAuthenticationEndpointValidationProvider(
                apiClient,
                config);
        }

        private sealed class ValidationApiClient : IApiClient
        {
            private readonly long statusCode;

            internal ValidationApiClient(long responseStatusCode)
            {
                statusCode = responseStatusCode;
            }

            internal bool SawBearerToken { get; private set; }

            public Task<ApiResult<TResponse>> SendAsync<TResponse>(
                ApiRequest request,
                CancellationToken cancellationToken =
                    default(CancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                SawBearerToken =
                    !string.IsNullOrWhiteSpace(request.BearerTokenOverride);
                if (statusCode >= 200 && statusCode < 300)
                {
                    string validatedToken =
                        ViewerAuthenticationExpiryIntegrationTests.CreateJwt(
                            1900000000.5d);
                    var response = new JObject
                    {
                        ["access_token"] = validatedToken
                    };
                    return Task.FromResult(
                        (ApiResult<TResponse>)(object)
                        ApiResult<JObject>.Success(
                            response,
                            HttpMethod.GET,
                            statusCode,
                            request.Endpoint,
                            null));
                }

                return Task.FromResult(
                    ApiResult<TResponse>.Failure(
                        new ApiError
                        {
                            Message = "Validation request failed.",
                            HttpStatusCode = statusCode
                        },
                        HttpMethod.GET));
            }

            public Task<ApiResult<TResponse>> SendAsync<TResponse>(
                ApiEndpoint endpoint,
                CancellationToken cancellationToken =
                    default(CancellationToken))
            {
                return SendAsync<TResponse>(
                    endpoint.CreateRequest(),
                    cancellationToken);
            }

            public Task<ApiResult<TResponse>> SendAsync<TResponse>(
                ApiEndpoint endpoint,
                object body,
                CancellationToken cancellationToken =
                    default(CancellationToken))
            {
                ApiRequest request = endpoint.CreateRequest(body);
                return SendAsync<TResponse>(request, cancellationToken);
            }

            public Task<ApiResult<TResponse>> GetAsync<TResponse>(
                string endpoint,
                CancellationToken cancellationToken =
                    default(CancellationToken))
            {
                return SendAsync<TResponse>(
                    new ApiRequest(endpoint, HttpMethod.GET),
                    cancellationToken);
            }

            public Task<ApiResult<TResponse>> PostAsync<TResponse>(
                string endpoint,
                object body,
                CancellationToken cancellationToken =
                    default(CancellationToken))
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
                CancellationToken cancellationToken =
                    default(CancellationToken))
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
                CancellationToken cancellationToken =
                    default(CancellationToken))
            {
                return SendWithBody<TResponse>(
                    endpoint,
                    HttpMethod.PATCH,
                    body,
                    cancellationToken);
            }

            public Task<ApiResult<TResponse>> DeleteAsync<TResponse>(
                string endpoint,
                CancellationToken cancellationToken =
                    default(CancellationToken))
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
