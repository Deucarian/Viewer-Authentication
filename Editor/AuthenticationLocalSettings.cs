using System;
using Deucarian.Session;
using UnityEditor;
using UnityEngine;

namespace Deucarian.Authentication.Editor
{
    [FilePath(
        "UserSettings/DeucarianAuthenticationSettings.asset",
        FilePathAttribute.Location.ProjectFolder)]
    internal sealed partial class AuthenticationLocalSettings :
        ScriptableSingleton<AuthenticationLocalSettings>
    {
        [SerializeField] private string selectedTargetId = string.Empty;
        [SerializeField] private bool rememberAccessToken;
        [SerializeField] private bool autoApply;
        [SerializeField] private string persistedTargetId = string.Empty;
        [SerializeField] private string persistedServiceId = string.Empty;
        [SerializeField] private string persistedEnvironmentId = string.Empty;
        [SerializeField] private string persistedAuthority = string.Empty;
        [SerializeField] private string persistedClientId = string.Empty;
        [SerializeField] private string persistedAccountId = string.Empty;
        [SerializeField]
        private string persistedConfigurationFingerprint = string.Empty;

        [NonSerialized]
        private Func<
            AuthenticationPersistenceIdentity,
            AuthenticationSecureSessionStore> secureStoreFactory;

        [NonSerialized]
        private Action persistOverride;

        internal string SelectedTargetId => selectedTargetId ?? string.Empty;
        internal bool RememberAccessToken => rememberAccessToken;
        internal bool AutoApply => rememberAccessToken && autoApply;
        internal string RememberedTargetId => HasRememberedAccessToken
            ? persistedTargetId ?? string.Empty
            : string.Empty;

        internal bool HasRememberedAccessToken
        {
            get
            {
                try
                {
                    return TryCreateStore(out AuthenticationSecureSessionStore store) &&
                           store.Exists;
                }
                catch
                {
                    return false;
                }
            }
        }

        internal bool TryGetRememberedAccessTokenFor(
            string targetId,
            out string accessToken)
        {
            accessToken = null;
            if (!TryResolveTarget(targetId, out AuthenticationTarget target) ||
                !TryLoadFor(target, out SessionData session))
            {
                return false;
            }

            accessToken = session.AccessToken;
            return !string.IsNullOrWhiteSpace(accessToken);
        }

        internal bool TryGetRememberedSessionFor(
            AuthenticationTarget target,
            out SessionData session)
        {
            return TryLoadFor(target, out session);
        }

        internal bool HasRememberedAccessTokenFor(string targetId)
        {
            return TryResolveTarget(targetId, out AuthenticationTarget target) &&
                   HasRememberedAccessTokenFor(target);
        }

        internal bool HasRememberedAccessTokenFor(
            AuthenticationTarget target)
        {
            return TryCreatePersistedIdentity(
                       out AuthenticationPersistenceIdentity identity) &&
                   AuthenticationRememberedTokenBinding.Matches(
                       persistedTargetId,
                       identity,
                       target) &&
                   HasRememberedAccessToken;
        }

        internal void SetSelectedTarget(string targetId)
        {
            selectedTargetId = targetId ?? string.Empty;
            PersistSettings();
        }

        internal void SetRememberAccessToken(bool value)
        {
            rememberAccessToken = value;
            if (!value)
            {
                autoApply = false;
                ClearRememberedToken();
                return;
            }

            PersistSettings();
        }

        internal void SetAutoApply(bool value)
        {
            autoApply = rememberAccessToken && value;
            PersistSettings();
        }

        internal bool RememberToken(string targetId, string accessToken)
        {
            if (!rememberAccessToken ||
                !TryResolveIdentity(
                    targetId,
                    out AuthenticationPersistenceIdentity identity) ||
                !AccessTokenInput.TryNormalize(
                    accessToken,
                    out string normalized))
            {
                return false;
            }

            try
            {
                return SaveSession(
                    targetId,
                    identity,
                    new SessionData(normalized));
            }
            catch
            {
                return false;
            }
            finally
            {
                normalized = null;
                accessToken = null;
            }
        }

        internal bool RememberSession(AuthenticationTarget target)
        {
            if (!rememberAccessToken ||
                target?.PersistenceIdentity == null ||
                target.Session?.SessionService?.CurrentSession == null)
            {
                return false;
            }

            return SaveSession(
                target.Id,
                target.PersistenceIdentity,
                target.Session.SessionService.CurrentSession);
        }

        internal bool TryMigrateLegacyToken(
            string targetId,
            string accessToken)
        {
            bool previousRemember = rememberAccessToken;
            bool previousAutoApply = autoApply;
            rememberAccessToken = true;
            autoApply = true;
            if (RememberToken(targetId, accessToken))
            {
                return true;
            }

            rememberAccessToken = previousRemember;
            autoApply = previousAutoApply;
            return false;
        }

        internal bool TryRebindRememberedTokenOwner(
            string expectedCurrentTargetId,
            string targetId)
        {
            if (!AuthenticationRememberedTokenBinding.TryRebindOwner(
                    persistedTargetId,
                    expectedCurrentTargetId,
                    targetId,
                    HasRememberedAccessToken,
                    out string reboundOwnerId) ||
                !TryCreatePersistedIdentity(
                    out AuthenticationPersistenceIdentity persistedIdentity) ||
                !TryResolveTarget(targetId, out AuthenticationTarget target) ||
                !AuthenticationRememberedTokenBinding.IdentityMatches(
                    persistedIdentity,
                    target.PersistenceIdentity))
            {
                return false;
            }

            persistedTargetId = reboundOwnerId;
            selectedTargetId = reboundOwnerId;
            PersistSettings();
            return true;
        }

        internal void ClearRememberedToken()
        {
            try
            {
                if (TryCreateStore(out AuthenticationSecureSessionStore store))
                {
                    store.ClearAsync().GetAwaiter().GetResult();
                }
            }
            catch
            {
                // Clearing metadata still prevents a stale secret from loading.
            }

            persistedTargetId = string.Empty;
            persistedServiceId = string.Empty;
            persistedEnvironmentId = string.Empty;
            persistedAuthority = string.Empty;
            persistedClientId = string.Empty;
            persistedAccountId = string.Empty;
            persistedConfigurationFingerprint = string.Empty;
            PersistSettings();
        }

        private bool TryLoadFor(
            AuthenticationTarget target,
            out SessionData session)
        {
            session = null;
            if (!TryCreatePersistedIdentity(
                    out AuthenticationPersistenceIdentity identity) ||
                !AuthenticationRememberedTokenBinding.Matches(
                    persistedTargetId,
                    identity,
                    target))
            {
                return false;
            }

            return TryLoad(out session);
        }

        private bool TryLoad(out SessionData session)
        {
            session = null;
            if (!rememberAccessToken ||
                !TryCreateStore(out AuthenticationSecureSessionStore store) ||
                !store.Exists)
            {
                return false;
            }

            try
            {
                session = store.LoadAsync().GetAwaiter().GetResult();
                return session != null;
            }
            catch
            {
                session = null;
                return false;
            }
        }

        private bool SaveSession(
            string targetId,
            AuthenticationPersistenceIdentity identity,
            SessionData session)
        {
            try
            {
                AuthenticationSecureSessionStore store = CreateStore(identity);
                store.SaveAsync(session).GetAwaiter().GetResult();
                SessionData verified = store.LoadAsync()
                    .GetAwaiter()
                    .GetResult();
                if (verified == null || !verified.Equals(session))
                {
                    return false;
                }

                CaptureIdentity(targetId, identity);
                PersistSettings();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool TryCreateStore(
            out AuthenticationSecureSessionStore store)
        {
            store = null;
            try
            {
                if (!TryCreatePersistedIdentity(
                        out AuthenticationPersistenceIdentity identity))
                {
                    return false;
                }

                store = CreateStore(identity);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private AuthenticationSecureSessionStore CreateStore(
            AuthenticationPersistenceIdentity identity)
        {
            return secureStoreFactory != null
                ? secureStoreFactory(identity)
                : new AuthenticationSecureSessionStore(identity);
        }

        private void PersistSettings()
        {
            if (persistOverride != null)
            {
                persistOverride();
                return;
            }

            Save(true);
        }

        private bool TryCreatePersistedIdentity(
            out AuthenticationPersistenceIdentity identity)
        {
            identity = null;
            try
            {
                identity = string.IsNullOrWhiteSpace(
                        persistedConfigurationFingerprint)
                    ? new AuthenticationPersistenceIdentity(
                        persistedServiceId,
                        persistedEnvironmentId,
                        persistedAuthority,
                        persistedClientId,
                        persistedAccountId)
                    : new AuthenticationPersistenceIdentity(
                        persistedServiceId,
                        persistedEnvironmentId,
                        persistedAuthority,
                        persistedClientId,
                        persistedAccountId,
                        persistedConfigurationFingerprint);
                return true;
            }
            catch
            {
                identity = null;
                return false;
            }
        }

        private static bool TryResolveIdentity(
            string targetId,
            out AuthenticationPersistenceIdentity identity)
        {
            identity = null;
            return !string.IsNullOrWhiteSpace(targetId) &&
                   AuthenticationTargetRegistry.TryGet(
                       targetId,
                       out AuthenticationTarget target) &&
                   (identity = target.PersistenceIdentity) != null;
        }

        private static bool TryResolveTarget(
            string targetId,
            out AuthenticationTarget target)
        {
            target = null;
            return !string.IsNullOrWhiteSpace(targetId) &&
                   AuthenticationTargetRegistry.TryGet(
                       targetId.Trim(),
                       out target);
        }

        private void CaptureIdentity(
            string targetId,
            AuthenticationPersistenceIdentity identity)
        {
            persistedTargetId = targetId.Trim();
            selectedTargetId = persistedTargetId;
            persistedServiceId = identity.ServiceId;
            persistedEnvironmentId = identity.EnvironmentId;
            persistedAuthority = identity.Authority;
            persistedClientId = identity.ClientId;
            persistedAccountId = identity.AccountId;
            persistedConfigurationFingerprint =
                identity.ConfigurationFingerprint;
        }
    }

    internal static class AuthenticationRememberedTokenBinding
    {
        internal static string ResolveOwner(
            string explicitOwnerId,
            string legacySelectedTargetId,
            bool hasRememberedToken)
        {
            if (!hasRememberedToken)
            {
                return string.Empty;
            }

            return !string.IsNullOrWhiteSpace(explicitOwnerId)
                ? explicitOwnerId.Trim()
                : legacySelectedTargetId?.Trim() ?? string.Empty;
        }

        internal static bool Matches(string ownerId, string targetId)
        {
            return !string.IsNullOrWhiteSpace(ownerId) &&
                   !string.IsNullOrWhiteSpace(targetId) &&
                   string.Equals(
                       ownerId.Trim(),
                       targetId.Trim(),
                       StringComparison.Ordinal);
        }

        internal static bool Matches(
            string ownerId,
            AuthenticationPersistenceIdentity persistedIdentity,
            AuthenticationTarget target)
        {
            return target != null &&
                   Matches(ownerId, target.Id) &&
                   IdentityMatches(
                       persistedIdentity,
                       target.PersistenceIdentity);
        }

        internal static bool IdentityMatches(
            AuthenticationPersistenceIdentity persistedIdentity,
            AuthenticationPersistenceIdentity currentIdentity)
        {
            return persistedIdentity != null &&
                   currentIdentity != null &&
                   persistedIdentity.Equals(currentIdentity);
        }

        internal static bool TryRebindOwner(
            string currentOwnerId,
            string expectedCurrentOwnerId,
            string targetId,
            bool hasRememberedToken,
            out string reboundOwnerId)
        {
            reboundOwnerId = currentOwnerId?.Trim() ?? string.Empty;
            if (!hasRememberedToken ||
                reboundOwnerId.Length == 0 ||
                !Matches(reboundOwnerId, expectedCurrentOwnerId) ||
                string.IsNullOrWhiteSpace(targetId))
            {
                return false;
            }

            reboundOwnerId = targetId.Trim();
            return true;
        }
    }
}
