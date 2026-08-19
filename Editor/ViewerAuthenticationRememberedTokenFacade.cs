namespace Deucarian.ViewerAuthentication.Editor
{
    /// <summary>
    /// Explicit Editor-only access to the opt-in remembered development token.
    /// This is intended for local migration and ignored development exports;
    /// it never logs or previews tokens. Ordinary imports never silently
    /// enable persistence; the explicitly named migration operation does.
    /// </summary>
    public static class ViewerAuthenticationRememberedTokenFacade
    {
        /// <summary>Gets whether local remembering was explicitly enabled.</summary>
        public static bool IsRememberingEnabled
        {
            get
            {
                return ViewerAuthenticationLocalSettings.instance
                    .RememberAccessToken;
            }
        }

        /// <summary>
        /// Imports a legacy development token into ignored UserSettings when
        /// local remembering was already enabled by the user.
        /// </summary>
        public static bool TryImport(
            string targetId,
            string accessToken)
        {
            ViewerAuthenticationLocalSettings settings =
                ViewerAuthenticationLocalSettings.instance;
            string normalized = null;
            if (!settings.RememberAccessToken ||
                string.IsNullOrWhiteSpace(targetId) ||
                !ViewerAccessTokenInput.TryNormalize(
                    accessToken,
                    out normalized))
            {
                normalized = null;
                return false;
            }

            settings.RememberToken(targetId.Trim(), normalized);
            normalized = null;
            accessToken = null;
            return true;
        }

        /// <summary>
        /// Explicitly migrates a normalized legacy development token into the
        /// ignored UserSettings store. Unlike <see cref="TryImport"/>, this
        /// method enables local remembering as part of the requested migration.
        /// The caller must retain its legacy source unless this returns true.
        /// </summary>
        public static bool TryMigrateLegacyToken(
            string targetId,
            string accessToken)
        {
            string normalized = null;
            if (string.IsNullOrWhiteSpace(targetId) ||
                !ViewerAccessTokenInput.TryNormalize(
                    accessToken,
                    out normalized))
            {
                normalized = null;
                return false;
            }

            bool migrated = ViewerAuthenticationLocalSettings.instance
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
            ViewerAuthenticationLocalSettings settings =
                ViewerAuthenticationLocalSettings.instance;
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

            return ViewerAuthenticationLocalSettings.instance
                .TryRebindRememberedTokenOwner(
                    expectedCurrentTargetId.Trim(),
                    targetId.Trim());
        }
    }
}
