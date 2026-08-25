using System;
using Deucarian.Session;
using UnityEditor;
using UnityEngine;

namespace Deucarian.ViewerAuthentication.Editor
{
    /// <summary>
    /// Carries one authenticated viewer session across Editor domain reloads.
    /// State is scoped to the current Unity Editor process and is never saved
    /// to project assets or UserSettings.
    /// </summary>
    public static class ViewerAuthenticationEditorSessionHandoff
    {
        /// <summary>
        /// Captures the current access token for an exact caller-owned binding.
        /// Capturing a cleared session clears the matching handoff.
        /// </summary>
        public static void Capture(
            string bindingId,
            IViewerAuthenticationSession session)
        {
            if (!TryNormalizeBinding(bindingId, out string binding) ||
                session == null)
            {
                return;
            }

            string token = session.AccessToken;
            if (!ViewerAccessTokenInput.TryNormalize(token, out string normalized))
            {
                ViewerAuthenticationEditorSessionHandoffState.Clear(binding);
                return;
            }

            ViewerAuthenticationEditorSessionHandoffState.Set(
                binding,
                normalized);
            normalized = null;
            token = null;
        }

        /// <summary>
        /// Applies a matching Editor-session handoff to an existing transient
        /// authentication session.
        /// </summary>
        public static bool TryApply(
            string bindingId,
            ViewerAuthenticationSession session)
        {
            if (session == null ||
                !TryTakeSnapshot(bindingId, out string binding, out string token))
            {
                return false;
            }

            try
            {
                SessionResult result = session.ReplaceAccessTokenAsync(token)
                    .GetAwaiter()
                    .GetResult();
                if (result?.Succeeded == true &&
                    session.Status.HasAccessToken)
                {
                    return true;
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                token = null;
            }

            ViewerAuthenticationEditorSessionHandoffState.Clear(binding);
            return false;
        }

        /// <summary>
        /// Creates a transient authentication session and applies a matching
        /// Editor-session handoff when one exists.
        /// </summary>
        public static bool TryCreateSession(
            string bindingId,
            out ViewerAuthenticationSession session)
        {
            session = ViewerAuthenticationSession.CreateTransient();
            return TryApply(bindingId, session);
        }

        /// <summary>Clears the handoff for an exact caller-owned binding.</summary>
        public static void Clear(string bindingId)
        {
            if (TryNormalizeBinding(bindingId, out string binding))
            {
                ViewerAuthenticationEditorSessionHandoffState.Clear(binding);
            }
        }

        internal static void ClearAllForTests()
        {
            ViewerAuthenticationEditorSessionHandoffState.ClearAll();
        }

        private static bool TryTakeSnapshot(
            string bindingId,
            out string binding,
            out string token)
        {
            token = null;
            if (!TryNormalizeBinding(bindingId, out binding) ||
                !ViewerAuthenticationEditorSessionHandoffState.TryGet(
                    binding,
                    out string candidate) ||
                !ViewerAccessTokenInput.TryNormalize(candidate, out token))
            {
                candidate = null;
                token = null;
                return false;
            }

            candidate = null;
            return true;
        }

        private static bool TryNormalizeBinding(
            string bindingId,
            out string binding)
        {
            binding = string.IsNullOrWhiteSpace(bindingId)
                ? null
                : bindingId.Trim();
            return binding != null;
        }

    }

    internal sealed class ViewerAuthenticationEditorSessionHandoffState :
        ScriptableSingleton<ViewerAuthenticationEditorSessionHandoffState>
    {
        [SerializeField]
        private string binding = string.Empty;

        [SerializeField]
        private string accessToken = string.Empty;

        internal static void Set(string owner, string token)
        {
            instance.binding = owner;
            instance.accessToken = token;
        }

        internal static bool TryGet(string owner, out string token)
        {
            token = null;
            if (!string.Equals(
                    instance.binding,
                    owner,
                    StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(instance.accessToken))
            {
                return false;
            }

            token = instance.accessToken;
            return true;
        }

        internal static void Clear(string owner)
        {
            if (string.Equals(
                    instance.binding,
                    owner,
                    StringComparison.Ordinal))
            {
                ClearAll();
            }
        }

        internal static void ClearAll()
        {
            instance.binding = string.Empty;
            instance.accessToken = string.Empty;
        }
    }
}
