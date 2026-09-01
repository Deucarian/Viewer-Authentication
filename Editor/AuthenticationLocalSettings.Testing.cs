using System;

namespace Deucarian.Authentication.Editor
{
    internal sealed partial class AuthenticationLocalSettings
    {
        internal IDisposable BeginIsolatedTestScope(
            Func<
                AuthenticationPersistenceIdentity,
                AuthenticationSecureSessionStore> storeFactory)
        {
            return new IsolatedTestScope(
                this,
                storeFactory ??
                throw new ArgumentNullException(nameof(storeFactory)));
        }

        private sealed class IsolatedTestScope : IDisposable
        {
            private readonly AuthenticationLocalSettings settings;
            private readonly string originalSelectedTargetId;
            private readonly bool originalRememberAccessToken;
            private readonly bool originalAutoApply;
            private readonly string originalPersistedTargetId;
            private readonly string originalPersistedServiceId;
            private readonly string originalPersistedEnvironmentId;
            private readonly string originalPersistedAuthority;
            private readonly string originalPersistedClientId;
            private readonly string originalPersistedAccountId;
            private readonly string originalConfigurationFingerprint;
            private readonly Func<
                AuthenticationPersistenceIdentity,
                AuthenticationSecureSessionStore> originalStoreFactory;
            private readonly Action originalPersistOverride;
            private bool disposed;

            internal IsolatedTestScope(
                AuthenticationLocalSettings settings,
                Func<
                    AuthenticationPersistenceIdentity,
                    AuthenticationSecureSessionStore> storeFactory)
            {
                this.settings = settings;
                originalSelectedTargetId = settings.selectedTargetId;
                originalRememberAccessToken = settings.rememberAccessToken;
                originalAutoApply = settings.autoApply;
                originalPersistedTargetId = settings.persistedTargetId;
                originalPersistedServiceId = settings.persistedServiceId;
                originalPersistedEnvironmentId =
                    settings.persistedEnvironmentId;
                originalPersistedAuthority = settings.persistedAuthority;
                originalPersistedClientId = settings.persistedClientId;
                originalPersistedAccountId = settings.persistedAccountId;
                originalConfigurationFingerprint =
                    settings.persistedConfigurationFingerprint;
                originalStoreFactory = settings.secureStoreFactory;
                originalPersistOverride = settings.persistOverride;

                settings.persistOverride = () => { };
                settings.secureStoreFactory = storeFactory;
                settings.selectedTargetId = string.Empty;
                settings.rememberAccessToken = false;
                settings.autoApply = false;
                settings.persistedTargetId = string.Empty;
                settings.persistedServiceId = string.Empty;
                settings.persistedEnvironmentId = string.Empty;
                settings.persistedAuthority = string.Empty;
                settings.persistedClientId = string.Empty;
                settings.persistedAccountId = string.Empty;
                settings.persistedConfigurationFingerprint = string.Empty;
            }

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                try
                {
                    settings.ClearRememberedToken();
                }
                finally
                {
                    settings.selectedTargetId = originalSelectedTargetId;
                    settings.rememberAccessToken =
                        originalRememberAccessToken;
                    settings.autoApply = originalAutoApply;
                    settings.persistedTargetId = originalPersistedTargetId;
                    settings.persistedServiceId = originalPersistedServiceId;
                    settings.persistedEnvironmentId =
                        originalPersistedEnvironmentId;
                    settings.persistedAuthority = originalPersistedAuthority;
                    settings.persistedClientId = originalPersistedClientId;
                    settings.persistedAccountId = originalPersistedAccountId;
                    settings.persistedConfigurationFingerprint =
                        originalConfigurationFingerprint;
                    settings.secureStoreFactory = originalStoreFactory;
                    settings.persistOverride = originalPersistOverride;
                }
            }
        }
    }
}
