using UnityEditor;

namespace Deucarian.ViewerAuthentication.Editor
{
    [FilePath(
        "UserSettings/DeucarianViewerAuthenticationSettings.asset",
        FilePathAttribute.Location.ProjectFolder)]
    internal sealed class ViewerAuthenticationLocalSettings :
        ScriptableSingleton<ViewerAuthenticationLocalSettings>
    {
        [UnityEngine.SerializeField]
        private string selectedTargetId = string.Empty;

        [UnityEngine.SerializeField]
        private bool rememberAccessToken;

        [UnityEngine.SerializeField]
        private bool autoApply;

        [UnityEngine.SerializeField]
        private string rememberedAccessToken = string.Empty;

        [UnityEngine.SerializeField]
        private string rememberedTargetId = string.Empty;

        internal string SelectedTargetId
        {
            get { return selectedTargetId ?? string.Empty; }
        }

        internal bool RememberAccessToken
        {
            get { return rememberAccessToken; }
        }

        internal bool AutoApply
        {
            get { return rememberAccessToken && autoApply; }
        }

        internal bool HasRememberedAccessToken
        {
            get
            {
                return rememberAccessToken &&
                       !string.IsNullOrWhiteSpace(rememberedAccessToken);
            }
        }

        internal string RememberedAccessToken
        {
            get
            {
                return HasRememberedAccessToken
                    ? rememberedAccessToken
                    : null;
            }
        }

        internal string RememberedTargetId
        {
            get
            {
                if (!HasRememberedAccessToken)
                {
                    return string.Empty;
                }

                return ViewerAuthenticationRememberedTokenBinding
                    .ResolveOwner(
                        rememberedTargetId,
                        SelectedTargetId,
                        hasRememberedToken: true);
            }
        }

        internal bool HasRememberedAccessTokenFor(string targetId)
        {
            return HasRememberedAccessToken &&
                   ViewerAuthenticationRememberedTokenBinding.Matches(
                       RememberedTargetId,
                       targetId);
        }

        internal void SetSelectedTarget(string targetId)
        {
            CaptureLegacyRememberedTarget();
            selectedTargetId = targetId ?? string.Empty;
            Save(true);
        }

        internal void SetRememberAccessToken(bool value)
        {
            rememberAccessToken = value;
            if (!value)
            {
                autoApply = false;
                rememberedAccessToken = string.Empty;
                rememberedTargetId = string.Empty;
            }

            Save(true);
        }

        internal void SetAutoApply(bool value)
        {
            autoApply = rememberAccessToken && value;
            Save(true);
        }

        internal void RememberToken(
            string targetId,
            string accessToken)
        {
            if (!rememberAccessToken ||
                string.IsNullOrWhiteSpace(accessToken))
            {
                return;
            }

            rememberedTargetId = targetId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(selectedTargetId))
            {
                selectedTargetId = rememberedTargetId;
            }

            rememberedAccessToken = accessToken;
            Save(true);
        }

        internal bool TryMigrateLegacyToken(
            string targetId,
            string accessToken)
        {
            string previousTargetId = selectedTargetId;
            string previousRememberedTargetId = rememberedTargetId;
            string previousToken = rememberedAccessToken;
            bool previousRemember = rememberAccessToken;
            bool previousAutoApply = autoApply;
            bool migrated = false;
            try
            {
                selectedTargetId = targetId;
                rememberedTargetId = targetId;
                rememberedAccessToken = accessToken;
                rememberAccessToken = true;
                autoApply = true;
                Save(true);
                migrated = rememberAccessToken &&
                           autoApply &&
                            string.Equals(
                                rememberedTargetId,
                                targetId,
                               System.StringComparison.Ordinal) &&
                           string.Equals(
                               rememberedAccessToken,
                               accessToken,
                               System.StringComparison.Ordinal);
            }
            catch
            {
                selectedTargetId = previousTargetId;
                rememberedTargetId = previousRememberedTargetId;
                rememberedAccessToken = previousToken;
                rememberAccessToken = previousRemember;
                autoApply = previousAutoApply;
                try
                {
                    Save(true);
                }
                catch
                {
                    // The caller retains the legacy source when migration
                    // cannot be confirmed. Never log credential state here.
                }
            }
            finally
            {
                previousToken = null;
                accessToken = null;
            }

            return migrated;
        }

        internal bool TryRebindRememberedTokenOwner(
            string expectedCurrentTargetId,
            string targetId)
        {
            if (!ViewerAuthenticationRememberedTokenBinding.TryRebindOwner(
                    RememberedTargetId,
                    expectedCurrentTargetId,
                    targetId,
                    HasRememberedAccessToken,
                    out string reboundOwner))
            {
                return false;
            }

            string previousSelectedTargetId = selectedTargetId;
            string previousRememberedTargetId = rememberedTargetId;
            try
            {
                rememberedTargetId = reboundOwner;
                selectedTargetId = reboundOwner;
                Save(true);
                return HasRememberedAccessTokenFor(reboundOwner);
            }
            catch
            {
                selectedTargetId = previousSelectedTargetId;
                rememberedTargetId = previousRememberedTargetId;
                try
                {
                    Save(true);
                }
                catch
                {
                    // Keep the existing in-memory ownership. No token value is
                    // read or copied by an owner-only rebind operation.
                }

                return false;
            }
        }

        internal void ClearRememberedToken()
        {
            rememberedAccessToken = string.Empty;
            rememberedTargetId = string.Empty;
            Save(true);
        }

        private void CaptureLegacyRememberedTarget()
        {
            if (HasRememberedAccessToken &&
                string.IsNullOrWhiteSpace(rememberedTargetId))
            {
                rememberedTargetId =
                    ViewerAuthenticationRememberedTokenBinding.ResolveOwner(
                        rememberedTargetId,
                        selectedTargetId,
                        hasRememberedToken: true);
            }
        }
    }

    internal static class ViewerAuthenticationRememberedTokenBinding
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

        internal static bool Matches(string rememberedTargetId, string targetId)
        {
            return !string.IsNullOrWhiteSpace(rememberedTargetId) &&
                   !string.IsNullOrWhiteSpace(targetId) &&
                   string.Equals(
                       rememberedTargetId.Trim(),
                       targetId.Trim(),
                       System.StringComparison.Ordinal);
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
                string.IsNullOrWhiteSpace(expectedCurrentOwnerId) ||
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
