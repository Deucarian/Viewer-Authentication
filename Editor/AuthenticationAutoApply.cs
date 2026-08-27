using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;

namespace Deucarian.Authentication.Editor
{
    [InitializeOnLoad]
    internal static class AuthenticationAutoApply
    {
        private static readonly HashSet<string> Applying =
            new HashSet<string>(StringComparer.Ordinal);

        static AuthenticationAutoApply()
        {
            AuthenticationTargetRegistry.TargetsChanged += Schedule;
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
            AuthenticationLocalSettings settings =
                AuthenticationLocalSettings.instance;
            if (!settings.AutoApply ||
                !settings.HasRememberedAccessToken ||
                !TryResolveTarget(
                    settings,
                    out AuthenticationTarget target))
            {
                return;
            }

            AuthenticationStatus state = target.Session.Status.Status;
            if (state != AuthenticationStatus.Missing &&
                state != AuthenticationStatus.Expired)
            {
                return;
            }

            if (!Applying.Add(target.Id))
            {
                return;
            }

            ApplyAsync(target, settings.RememberedSession);
        }

        private static bool TryResolveTarget(
            AuthenticationLocalSettings settings,
            out AuthenticationTarget target)
        {
            if (!string.IsNullOrWhiteSpace(settings.RememberedTargetId) &&
                AuthenticationTargetRegistry.TryGet(
                    settings.RememberedTargetId,
                    out target))
            {
                return true;
            }

            target = null;
            return false;
        }

        private static async void ApplyAsync(
            AuthenticationTarget target,
            Deucarian.Session.SessionData persistedSession)
        {
            try
            {
                if (persistedSession != null)
                {
                    await target.Session.ApplyPersistedSessionAsync(
                        persistedSession,
                        CancellationToken.None);
                }
            }
            catch (Exception)
            {
                // Deliberately silent: this background dev convenience must
                // never risk including credential material in a log path.
            }
            finally
            {
                Applying.Remove(target.Id);
                persistedSession = null;
            }
        }
    }
}
