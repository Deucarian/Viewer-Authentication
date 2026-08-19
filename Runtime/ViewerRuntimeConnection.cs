using System;
using System.Collections.Generic;
using Deucarian.API.Core;

namespace Deucarian.ViewerAuthentication
{
    /// <summary>
    /// One authoritative runtime authentication/API composition leased from
    /// an optional viewer connection provider.
    /// </summary>
    public sealed class ViewerRuntimeConnection : IDisposable
    {
        private readonly IDisposable lifetime;
        private bool disposed;

        public ViewerRuntimeConnection(
            string targetId,
            IViewerAuthenticationSession session,
            IApiClient apiClient,
            string apiBaseUrl,
            IEnumerable<string> authenticatedOrigins,
            IDisposable lifetime)
        {
            if (string.IsNullOrWhiteSpace(targetId))
            {
                throw new ArgumentException(
                    "A stable authentication target ID is required.",
                    nameof(targetId));
            }

            TargetId = targetId.Trim();
            Session = session ?? throw new ArgumentNullException(nameof(session));
            ApiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            ApiBaseUrl = NormalizeBaseUrl(apiBaseUrl);
            AuthenticatedOrigins = ResolveAuthenticatedOrigins(
                ApiBaseUrl,
                authenticatedOrigins);
            this.lifetime = lifetime ??
                throw new ArgumentNullException(nameof(lifetime));
        }

        public string TargetId { get; }

        public IViewerAuthenticationSession Session { get; }

        public IApiClient ApiClient { get; }

        /// <summary>
        /// Resolved API base URL. Consumers should present only sanitized
        /// environment status, not this transport configuration.
        /// </summary>
        public string ApiBaseUrl { get; }

        /// <summary>
        /// Exact HTTP(S) origins to which the shared API client may attach the
        /// session bearer. The API base origin is always included.
        /// </summary>
        public IReadOnlyCollection<string> AuthenticatedOrigins { get; }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            lifetime.Dispose();
        }

        private static string NormalizeBaseUrl(string value)
        {
            if (!TryCreateHttpUri(value, out Uri uri))
            {
                throw new ArgumentException(
                    "An absolute HTTP(S) API base URL is required.",
                    nameof(value));
            }

            return uri.AbsoluteUri.TrimEnd('/');
        }

        private static IReadOnlyCollection<string> ResolveAuthenticatedOrigins(
            string apiBaseUrl,
            IEnumerable<string> additionalOrigins)
        {
            var origins = new SortedSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                NormalizeOrigin(apiBaseUrl)
            };
            if (additionalOrigins != null)
            {
                foreach (string origin in additionalOrigins)
                {
                    origins.Add(NormalizeExactOrigin(origin));
                }
            }

            string[] snapshot = new string[origins.Count];
            origins.CopyTo(snapshot);
            return snapshot;
        }

        private static string NormalizeOrigin(string value)
        {
            if (!TryCreateHttpUri(value, out Uri uri) ||
                !string.IsNullOrEmpty(uri.UserInfo))
            {
                throw new ArgumentException(
                    "Authenticated origins must be absolute HTTP(S) origins.",
                    nameof(value));
            }

            return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
        }

        private static string NormalizeExactOrigin(string value)
        {
            if (!TryCreateHttpUri(value, out Uri uri) ||
                !string.IsNullOrEmpty(uri.UserInfo) ||
                (!string.IsNullOrEmpty(uri.AbsolutePath) &&
                 !string.Equals(uri.AbsolutePath, "/", StringComparison.Ordinal)) ||
                !string.IsNullOrEmpty(uri.Query) ||
                !string.IsNullOrEmpty(uri.Fragment))
            {
                throw new ArgumentException(
                    "Additional authenticated origins must be exact HTTP(S) " +
                    "origins without a path, query, fragment, or user info.",
                    nameof(value));
            }

            return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
        }

        private static bool TryCreateHttpUri(string value, out Uri uri)
        {
            return Uri.TryCreate(
                       string.IsNullOrWhiteSpace(value)
                           ? string.Empty
                           : value.Trim(),
                       UriKind.Absolute,
                       out uri) &&
                   (uri.Scheme == Uri.UriSchemeHttp ||
                    uri.Scheme == Uri.UriSchemeHttps);
        }
    }
}
