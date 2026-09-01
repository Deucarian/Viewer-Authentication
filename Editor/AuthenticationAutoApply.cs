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
            _ = TryApplyRememberedTokenAsync(
                AuthenticationLocalSettings.instance);
        }

        internal static async Task<bool> TryApplyRememberedTokenAsync(
            AuthenticationLocalSettings settings)
        {
            if (settings == null ||
                !settings.AutoApply ||
                !TryResolveTarget(
                    settings,
                    out AuthenticationTarget target))
            {
                return false;
            }

            AuthenticationStatus state = target.Session.Status.Status;
            if (state != AuthenticationStatus.Missing &&
                state != AuthenticationStatus.Expired)
            {
                return false;
            }

            if (!Applying.Add(target.Id))
            {
                return false;
            }

            if (!settings.TryGetRememberedSessionFor(
                    target,
                    out Deucarian.Session.SessionData persistedSession))
            {
                Applying.Remove(target.Id);
                return false;
            }

            return await ApplyAsync(target, persistedSession);
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

        private static async Task<bool> ApplyAsync(
            AuthenticationTarget target,
            Deucarian.Session.SessionData persistedSession)
        {
            try
            {
                if (persistedSession != null)
                {
                    Deucarian.Session.SessionResult result =
                        await target.Session.ApplyPersistedSessionAsync(
                            persistedSession,
                            CancellationToken.None);
                    return result?.Succeeded == true &&
                           target.Session.Status.HasAccessToken;
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

            return false;
        }
    }
}
