using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Deucarian.Authentication.Editor
{
    /// <summary>
    /// Ephemeral window-owned authentication workspace. It is deliberately not
    /// registered as a live target and is discarded when the window closes.
    /// </summary>
    internal sealed class AuthenticationEditModeWorkspace : IDisposable
    {
        internal const string DefaultTargetId = "project-authentication";

        private bool disposed;

        internal AuthenticationEditModeWorkspace(
            string selectedTargetId,
            AuthenticationProjectProfiles profiles,
            string projectDisplayName = null)
        {
            Profiles = profiles ?? throw new ArgumentNullException(
                nameof(profiles));
            string id = ResolveTargetId(selectedTargetId);
            Target = new AuthenticationTarget(
                id,
                CreateDisplayName(projectDisplayName),
                AuthenticationSession.CreateTransient(),
                profiles.AcquisitionProvider,
                profiles.ValidationProvider,
                null);
        }

        internal AuthenticationTarget Target { get; private set; }

        internal AuthenticationProjectProfiles Profiles { get; }

        internal async Task LoadRememberedSessionForInspectionAsync(
            Deucarian.Session.SessionData persistedSession,
            CancellationToken cancellationToken)
        {
            if (disposed || persistedSession == null)
            {
                return;
            }

            try
            {
                await Target.Session.ApplyPersistedSessionAsync(
                    persistedSession,
                    cancellationToken);
            }
            finally
            {
                persistedSession = null;
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            AuthenticationTarget target = Target;
            Target = null;
            if (target != null)
            {
                _ = target.Session.ClearAsync(CancellationToken.None);
            }
        }

        internal static string ResolveTargetId(string selectedTargetId)
        {
            return string.IsNullOrWhiteSpace(selectedTargetId)
                ? DefaultTargetId
                : selectedTargetId.Trim();
        }

        internal static string CreateDisplayName(string projectDisplayName)
        {
            if (string.IsNullOrWhiteSpace(projectDisplayName))
            {
                return "Project Authentication";
            }

            string normalized = projectDisplayName.Trim()
                .Replace('-', ' ')
                .Replace('_', ' ');
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(
                normalized.ToLowerInvariant());
        }
    }
}
