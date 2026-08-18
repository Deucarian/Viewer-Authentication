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

        internal void SetSelectedTarget(string targetId)
        {
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

            selectedTargetId = targetId ?? string.Empty;
            rememberedAccessToken = accessToken;
            Save(true);
        }

        internal void ClearRememberedToken()
        {
            rememberedAccessToken = string.Empty;
            Save(true);
        }
    }
}
