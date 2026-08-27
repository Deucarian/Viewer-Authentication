namespace Deucarian.Authentication.Editor
{
    /// <summary>
    /// Explicit Editor-only access to an opt-in platform-protected session.
    /// It never logs or previews tokens. Ordinary imports never silently
    /// enable persistence; the explicitly named migration operation does.
    /// </summary>
    public static class AuthenticationRememberedTokenFacade
    {
        /// <summary>Gets whether local remembering was explicitly enabled.</summary>
        public static bool IsRememberingEnabled
        {
            get
            {
                return AuthenticationLocalSettings.instance
                    .RememberAccessToken;
            }
        }

        /// <summary>
        /// Imports a development token into secure local storage when local
        /// remembering was already enabled by the user.
        /// </summary>
        public static bool TryImport(
            string targetId,
            string accessToken)
        {
            AuthenticationLocalSettings settings =
                AuthenticationLocalSettings.instance;
            string normalized = null;
            if (!settings.RememberAccessToken ||
                string.IsNullOrWhiteSpace(targetId) ||
                !AccessTokenInput.TryNormalize(
                    accessToken,
                    out normalized))
            {
                normalized = null;
                return false;
            }

            bool imported = settings.RememberToken(
                targetId.Trim(),
                normalized);
            normalized = null;
            accessToken = null;
            return imported;
        }

        /// <summary>
        /// Explicitly migrates a normalized legacy development token into the
        /// platform-protected store. Unlike <see cref="TryImport"/>, this
        /// method enables local remembering as part of the requested migration.
        /// The caller must retain its legacy source unless this returns true.
        /// </summary>
        public static bool TryMigrateLegacyToken(
            string targetId,
            string accessToken)
        {
            string normalized = null;
            if (string.IsNullOrWhiteSpace(targetId) ||
                !AccessTokenInput.TryNormalize(
                    accessToken,
                    out normalized))
            {
                normalized = null;
                return false;
            }

            bool migrated = AuthenticationLocalSettings.instance
                .TryMigrateLegacyToken(targetId.Trim(), normalized);
            normalized = null;
            accessToken = null;
            return migrated;
        }

        /// <summary>
        /// Retrieves a remembered token for the exact stable target id. The
        /// caller must clear its reference immediately after local use.
        /// </summary>
        public static bool TryGet(
            string targetId,
            out string accessToken)
        {
            accessToken = null;
            AuthenticationLocalSettings settings =
                AuthenticationLocalSettings.instance;
            if (string.IsNullOrWhiteSpace(targetId) ||
                !settings.HasRememberedAccessTokenFor(targetId))
            {
                return false;
            }

            accessToken = settings.RememberedAccessToken;
            return !string.IsNullOrWhiteSpace(accessToken);
        }

        /// <summary>
        /// Rebinds an existing remembered token to another stable target ID
        /// without exposing or replacing the token value. This does not enable
        /// local remembering and returns false when no token is remembered.
        /// </summary>
        public static bool TryRebindOwner(
            string expectedCurrentTargetId,
            string targetId)
        {
            if (string.IsNullOrWhiteSpace(expectedCurrentTargetId) ||
                string.IsNullOrWhiteSpace(targetId))
            {
                return false;
            }

            return AuthenticationLocalSettings.instance
                .TryRebindRememberedTokenOwner(
                    expectedCurrentTargetId.Trim(),
                    targetId.Trim());
        }
    }
}
