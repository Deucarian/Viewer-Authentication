using System;
using System.Collections.Generic;
using Deucarian.Session;

namespace Deucarian.Authentication
{
    /// <summary>
    /// Holds explicit live target registrations for editor tooling. It stores
    /// references only and never copies token data.
    /// </summary>
    public static class AuthenticationTargetRegistry
    {
        private static readonly object Gate = new object();
        private static readonly List<AuthenticationTarget> Registered =
            new List<AuthenticationTarget>();

        /// <summary>
        /// Raised after registrations change or a registered session changes.
        /// </summary>
        public static event Action TargetsChanged;

        /// <summary>
        /// Raised only when the set of registered targets changes. Session
        /// mutations continue to raise <see cref="TargetsChanged"/> without
        /// raising this structural event.
        /// </summary>
        public static event Action RegistrationsChanged;

        /// <summary>Gets an immutable snapshot of current targets.</summary>
        public static IReadOnlyList<AuthenticationTarget> Targets
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
        /// Registers a live authentication target.
        /// </summary>
        /// <returns>An idempotent handle that removes this exact registration.</returns>
        public static IDisposable Register(
            string id,
            string displayName,
            IAuthenticationSession session,
            IAuthenticationAcquisitionProvider provider = null)
        {
            return Register(
                id,
                displayName,
                session,
                provider,
                null,
                null);
        }

        /// <summary>
        /// Registers a live authentication target with optional token
        /// acquisition and server-side validation providers.
        /// </summary>
        public static IDisposable Register(
            string id,
            string displayName,
            IAuthenticationSession session,
            IAuthenticationAcquisitionProvider provider,
            IAuthenticationValidationProvider validationProvider)
        {
            return Register(
                id,
                displayName,
                session,
                provider,
                validationProvider,
                null);
        }

        /// <summary>
        /// Registers a target with a stable secure-persistence identity.
        /// </summary>
        public static IDisposable Register(
            string id,
            string displayName,
            IAuthenticationSession session,
            IAuthenticationAcquisitionProvider provider,
            IAuthenticationValidationProvider validationProvider,
            AuthenticationPersistenceIdentity persistenceIdentity)
        {
            string normalizedId = NormalizeRequired(id, nameof(id));
            string normalizedDisplayName = string.IsNullOrWhiteSpace(displayName)
                ? normalizedId
                : displayName.Trim();
            var target = new AuthenticationTarget(
                normalizedId,
                normalizedDisplayName,
                session,
                provider,
                validationProvider,
                persistenceIdentity);

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
                            "A authentication target with id '" +
                            normalizedId +
                            "' is already registered.");
                    }
                }

                Registered.Add(target);
            }

            RaiseRegistrationsChanged();
            return new Registration(target);
        }

        /// <summary>Attempts to resolve a target by stable identifier.</summary>
        public static bool TryGet(
            string id,
            out AuthenticationTarget target)
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

        private static void Remove(AuthenticationTarget target)
        {
            bool removed;
            lock (Gate)
            {
                removed = Registered.Remove(target);
            }

            if (removed)
            {
                RaiseRegistrationsChanged();
            }
        }

        private static void RaiseTargetsChanged()
        {
            InvokeSubscribers(TargetsChanged);
        }

        private static void RaiseRegistrationsChanged()
        {
            InvokeSubscribers(RegistrationsChanged);
            RaiseTargetsChanged();
        }

        private static void InvokeSubscribers(Action handlers)
        {
            if (handlers == null)
            {
                return;
            }

            Delegate[] subscribers = handlers.GetInvocationList();
            for (int i = 0; i < subscribers.Length; i++)
            {
                try
                {
                    ((Action)subscribers[i])();
                }
                catch (Exception)
                {
                    // Observers must never make registry mutations partial or
                    // prevent callers from receiving/disposal of their handle.
                }
            }
        }

        private sealed class Registration : IDisposable
        {
            private AuthenticationTarget target;

            internal Registration(AuthenticationTarget registeredTarget)
            {
                target = registeredTarget;
                target.Session.SessionService.SessionChanged +=
                    OnSessionChanged;
            }

            public void Dispose()
            {
                AuthenticationTarget registeredTarget = target;
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
