using System;
using System.Collections.Generic;

namespace Deucarian.Authentication.Editor
{
    internal static class AuthenticationMenuModel
    {
        internal static bool ShouldShowConfigurationSelector(int count)
        {
            return count > 1;
        }

        internal static int ResolveSelectedIndex(
            IReadOnlyList<AuthenticationTarget> targets,
            string selectedId)
        {
            if (targets == null || targets.Count == 0)
            {
                return -1;
            }

            for (int i = 0; i < targets.Count; i++)
            {
                if (string.Equals(
                        targets[i].Id,
                        selectedId,
                        StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return 0;
        }

        internal static bool ShouldRememberVerifiedSession(
            bool rememberEnabled,
            bool hasAccessToken,
            AuthenticationAssessmentSnapshot snapshot)
        {
            return rememberEnabled &&
                   hasAccessToken &&
                   snapshot != null &&
                   snapshot.Result.Status ==
                       AuthenticationValidationStatus.Verified;
        }
    }
}
