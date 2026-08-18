using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;

namespace Deucarian.ViewerAuthentication.Editor
{
    [InitializeOnLoad]
    internal static class ViewerAuthenticationAutoApply
    {
        private static readonly HashSet<string> Applying =
            new HashSet<string>(StringComparer.Ordinal);

        static ViewerAuthenticationAutoApply()
        {
            ViewerAuthenticationTargetRegistry.TargetsChanged += Schedule;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            Schedule();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                Schedule();
            }
        }

        private static void Schedule()
        {
            EditorApplication.delayCall -= TryApplyRememberedToken;
            EditorApplication.delayCall += TryApplyRememberedToken;
        }

        private static void TryApplyRememberedToken()
        {
            ViewerAuthenticationLocalSettings settings =
                ViewerAuthenticationLocalSettings.instance;
            if (!settings.AutoApply ||
                !settings.HasRememberedAccessToken ||
                !TryResolveTarget(
                    settings,
                    out ViewerAuthenticationTarget target))
            {
                return;
            }

            ViewerAuthenticationStatus state = target.Session.Status.Status;
            if (state != ViewerAuthenticationStatus.Missing &&
                state != ViewerAuthenticationStatus.Expired)
            {
                return;
            }

            if (!Applying.Add(target.Id))
            {
                return;
            }

            ApplyAsync(target, settings.RememberedAccessToken);
        }

        private static bool TryResolveTarget(
            ViewerAuthenticationLocalSettings settings,
            out ViewerAuthenticationTarget target)
        {
            if (!string.IsNullOrWhiteSpace(settings.SelectedTargetId) &&
                ViewerAuthenticationTargetRegistry.TryGet(
                    settings.SelectedTargetId,
                    out target))
            {
                return true;
            }

            IReadOnlyList<ViewerAuthenticationTarget> targets =
                ViewerAuthenticationTargetRegistry.Targets;
            if (targets.Count != 1)
            {
                target = null;
                return false;
            }

            target = targets[0];
            settings.SetSelectedTarget(target.Id);
            return true;
        }

        private static async void ApplyAsync(
            ViewerAuthenticationTarget target,
            string accessToken)
        {
            try
            {
                await target.Session.ReplaceAccessTokenAsync(
                    accessToken,
                    null,
                    CancellationToken.None);
            }
            catch (Exception)
            {
                // Deliberately silent: this background dev convenience must
                // never risk including credential material in a log path.
            }
            finally
            {
                Applying.Remove(target.Id);
                accessToken = null;
            }
        }
    }
}
