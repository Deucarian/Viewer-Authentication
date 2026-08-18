using System;
using System.Collections.Generic;
using Deucarian.Session;

namespace Deucarian.ViewerAuthentication
{
    /// <summary>
    /// Holds explicit live target registrations for editor tooling. It stores
    /// references only and never copies token data.
    /// </summary>
    public static class ViewerAuthenticationTargetRegistry
    {
        private static readonly object Gate = new object();
        private static readonly List<ViewerAuthenticationTarget> Registered =
            new List<ViewerAuthenticationTarget>();

        /// <summary>Raised after targets are registered or disposed.</summary>
        public static event Action TargetsChanged;

        /// <summary>Gets an immutable snapshot of current targets.</summary>
        public static IReadOnlyList<ViewerAuthenticationTarget> Targets
        {
            get
            {
                lock (Gate)
                {
                    return Registered.ToArray();
                }
            }
        }

        /// <summary>
        /// Registers a live viewer authentication target.
        /// </summary>
        /// <returns>An idempotent handle that removes this exact registration.</returns>
        public static IDisposable Register(
            string id,
            string displayName,
            IViewerAuthenticationSession session,
            IViewerAuthenticationAcquisitionProvider provider = null)
        {
            return Register(
                id,
                displayName,
                session,
                provider,
                null);
        }

        /// <summary>
        /// Registers a live viewer authentication target with optional token
        /// acquisition and server-side validation providers.
        /// </summary>
        public static IDisposable Register(
            string id,
            string displayName,
            IViewerAuthenticationSession session,
            IViewerAuthenticationAcquisitionProvider provider,
            IViewerAuthenticationValidationProvider validationProvider)
        {
            string normalizedId = NormalizeRequired(id, nameof(id));
            string normalizedDisplayName = string.IsNullOrWhiteSpace(displayName)
                ? normalizedId
                : displayName.Trim();
            var target = new ViewerAuthenticationTarget(
                normalizedId,
                normalizedDisplayName,
                session,
                provider,
                validationProvider);

            lock (Gate)
            {
                for (int i = 0; i < Registered.Count; i++)
                {
                    if (string.Equals(
                            Registered[i].Id,
                            normalizedId,
                            StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            "A viewer authentication target with id '" +
                            normalizedId +
                            "' is already registered.");
                    }
                }

                Registered.Add(target);
            }

            RaiseTargetsChanged();
            return new Registration(target);
        }

        /// <summary>Attempts to resolve a target by stable identifier.</summary>
        public static bool TryGet(
            string id,
            out ViewerAuthenticationTarget target)
        {
            lock (Gate)
            {
                for (int i = 0; i < Registered.Count; i++)
                {
                    if (string.Equals(
                            Registered[i].Id,
                            id,
                            StringComparison.Ordinal))
                    {
                        target = Registered[i];
                        return true;
                    }
                }
            }

            target = null;
            return false;
        }

        private static string NormalizeRequired(
            string value,
            string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "A non-empty target id is required.",
                    parameterName);
            }

            return value.Trim();
        }

        private static void Remove(ViewerAuthenticationTarget target)
        {
            bool removed;
            lock (Gate)
            {
                removed = Registered.Remove(target);
            }

            if (removed)
            {
                RaiseTargetsChanged();
            }
        }

        private static void RaiseTargetsChanged()
        {
            Action handler = TargetsChanged;
            if (handler != null)
            {
                handler();
            }
        }

        private sealed class Registration : IDisposable
        {
            private ViewerAuthenticationTarget target;

            internal Registration(ViewerAuthenticationTarget registeredTarget)
            {
                target = registeredTarget;
                target.Session.SessionService.SessionChanged +=
                    OnSessionChanged;
            }

            public void Dispose()
            {
                ViewerAuthenticationTarget registeredTarget = target;
                if (registeredTarget == null)
                {
                    return;
                }

                target = null;
                registeredTarget.Session.SessionService.SessionChanged -=
                    OnSessionChanged;
                Remove(registeredTarget);
            }

            private void OnSessionChanged(
                object sender,
                SessionChangedEventArgs eventArgs)
            {
                RaiseTargetsChanged();
            }
        }
    }
}
