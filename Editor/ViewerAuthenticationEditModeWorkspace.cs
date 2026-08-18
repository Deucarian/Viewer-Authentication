using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Deucarian.ViewerAuthentication.Editor
{
    /// <summary>
    /// Ephemeral window-owned authentication workspace. It is deliberately not
    /// registered as a live viewer target and is discarded when the window closes.
    /// </summary>
    internal sealed class ViewerAuthenticationEditModeWorkspace : IDisposable
    {
        internal const string DefaultTargetId = "project-viewer";

        private bool disposed;

        internal ViewerAuthenticationEditModeWorkspace(
            string selectedTargetId,
            ViewerAuthenticationProjectProfiles profiles,
            string projectDisplayName = null)
        {
            Profiles = profiles ?? throw new ArgumentNullException(
                nameof(profiles));
            string id = ResolveTargetId(selectedTargetId);
            Target = new ViewerAuthenticationTarget(
                id,
                CreateDisplayName(projectDisplayName),
                ViewerAuthenticationSession.CreateTransient(),
                profiles.AcquisitionProvider,
                profiles.ValidationProvider);
        }

        internal ViewerAuthenticationTarget Target { get; private set; }

        internal ViewerAuthenticationProjectProfiles Profiles { get; }

        internal async Task LoadRememberedTokenForInspectionAsync(
            string accessToken,
            CancellationToken cancellationToken)
        {
            if (disposed || string.IsNullOrWhiteSpace(accessToken))
            {
                return;
            }

            try
            {
                await Target.Session.ReplaceAccessTokenAsync(
                    accessToken,
                    null,
                    cancellationToken);
            }
            finally
            {
                accessToken = null;
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            ViewerAuthenticationTarget target = Target;
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
                return "Project Viewer";
            }

            string normalized = projectDisplayName.Trim()
                .Replace('-', ' ')
                .Replace('_', ' ');
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(
                normalized.ToLowerInvariant());
        }
    }
}
