using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deucarian.ViewerAuthentication
{
    public enum ViewerRuntimeConnectionResolutionStatus
    {
        None,
        Resolved,
        Failed,
        Ambiguous
    }

    /// <summary>
    /// Optional provider for one authoritative runtime session/API
    /// composition. Registration is explicit; no assembly scanning occurs.
    /// </summary>
    public interface IViewerRuntimeConnectionProvider
    {
        string Id { get; }

        bool TryCreate(
            out ViewerRuntimeConnection connection,
            out string error);
    }

    public sealed class ViewerRuntimeConnectionResolution
    {
        private ViewerRuntimeConnectionResolution(
            ViewerRuntimeConnectionResolutionStatus status,
            ViewerRuntimeConnection connection,
            string message)
        {
            Status = status;
            Connection = connection;
            Message = message ?? string.Empty;
        }

        public ViewerRuntimeConnectionResolutionStatus Status { get; }

        /// <summary>
        /// Created connection owned by the caller when status is Resolved.
        /// </summary>
        public ViewerRuntimeConnection Connection { get; }

        /// <summary>Sanitized provider/registry diagnostic.</summary>
        public string Message { get; }

        internal static ViewerRuntimeConnectionResolution None() =>
            new ViewerRuntimeConnectionResolution(
                ViewerRuntimeConnectionResolutionStatus.None,
                null,
                string.Empty);

        internal static ViewerRuntimeConnectionResolution Resolved(
            ViewerRuntimeConnection connection) =>
            new ViewerRuntimeConnectionResolution(
                ViewerRuntimeConnectionResolutionStatus.Resolved,
                connection,
                string.Empty);

        internal static ViewerRuntimeConnectionResolution Failure(
            ViewerRuntimeConnectionResolutionStatus status,
            string message) =>
            new ViewerRuntimeConnectionResolution(status, null, message);
    }

    /// <summary>
    /// Process-local registry for optional runtime viewer connection
    /// composition. Zero providers means use the consumer's normal fallback;
    /// any registered-provider failure or ambiguity fails closed.
    /// </summary>
    public static class ViewerRuntimeConnectionProviderRegistry
    {
        private static readonly object Gate = new object();
        private static readonly List<IViewerRuntimeConnectionProvider> Providers =
            new List<IViewerRuntimeConnectionProvider>();

        public static IDisposable Register(
            IViewerRuntimeConnectionProvider provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            string providerId = NormalizeRequired(provider.Id, nameof(provider));
            lock (Gate)
            {
                for (int i = 0; i < Providers.Count; i++)
                {
                    if (string.Equals(
                            Providers[i].Id?.Trim(),
                            providerId,
                            StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            "A runtime connection provider with ID '" +
                            providerId + "' is already registered.");
                    }
                }

                Providers.Add(provider);
            }

            return new Registration(provider);
        }

        public static ViewerRuntimeConnectionResolution Resolve()
        {
            IViewerRuntimeConnectionProvider[] snapshot;
            lock (Gate)
            {
                snapshot = Providers.ToArray();
            }

            if (snapshot.Length == 0)
            {
                return ViewerRuntimeConnectionResolution.None();
            }

            if (snapshot.Length != 1)
            {
                return ViewerRuntimeConnectionResolution.Failure(
                    ViewerRuntimeConnectionResolutionStatus.Ambiguous,
                    "Multiple runtime connection providers are registered; " +
                    "exactly one is required.");
            }

            ViewerRuntimeConnection connection = null;
            try
            {
                if (!snapshot[0].TryCreate(
                        out connection,
                        out string error) ||
                    connection == null)
                {
                    TryDispose(connection);
                    return ViewerRuntimeConnectionResolution.Failure(
                        ViewerRuntimeConnectionResolutionStatus.Failed,
                        string.IsNullOrWhiteSpace(error)
                            ? "The runtime connection provider could not create a connection."
                            : error);
                }

                return ViewerRuntimeConnectionResolution.Resolved(connection);
            }
            catch (Exception exception)
            {
                TryDispose(connection);
                return ViewerRuntimeConnectionResolution.Failure(
                    ViewerRuntimeConnectionResolutionStatus.Failed,
                    "The runtime connection provider failed (" +
                    exception.GetType().Name + ").");
            }
        }

        private static void TryDispose(ViewerRuntimeConnection connection)
        {
            if (connection == null)
            {
                return;
            }

            try
            {
                connection.Dispose();
            }
            catch (Exception)
            {
                // Provider cleanup must not replace the fail-closed result.
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForRuntime()
        {
            lock (Gate)
            {
                Providers.Clear();
            }
        }

        private static string NormalizeRequired(
            string value,
            string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "A non-empty stable provider ID is required.",
                    parameterName);
            }

            return value.Trim();
        }

        private sealed class Registration : IDisposable
        {
            private IViewerRuntimeConnectionProvider provider;

            internal Registration(IViewerRuntimeConnectionProvider provider)
            {
                this.provider = provider;
            }

            public void Dispose()
            {
                IViewerRuntimeConnectionProvider registered = provider;
                if (registered == null)
                {
                    return;
                }

                provider = null;
                lock (Gate)
                {
                    Providers.Remove(registered);
                }
            }
        }
    }
}
