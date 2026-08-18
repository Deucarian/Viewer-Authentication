using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.Session;

namespace Deucarian.ViewerAuthentication.Editor
{
    internal sealed class ViewerAuthenticationAssessmentSnapshot
    {
        internal ViewerAuthenticationAssessmentSnapshot(
            ViewerAuthenticationValidationResult result,
            DateTimeOffset checkedAtUtc)
        {
            Result = result;
            CheckedAtUtc = checkedAtUtc.ToUniversalTime();
        }

        internal ViewerAuthenticationValidationResult Result { get; }

        internal DateTimeOffset CheckedAtUtc { get; }
    }

    /// <summary>
    /// Enriches unknown JWT expiry locally and throttles optional server probes
    /// so opening and immediately focusing a window does not duplicate requests.
    /// </summary>
    internal sealed class ViewerAuthenticationAssessmentController
    {
        internal static readonly TimeSpan AutomaticProbeCooldown =
            TimeSpan.FromMinutes(1);

        private readonly Dictionary<string, DateTimeOffset> lastAttempts =
            new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);
        private readonly Dictionary<string, ViewerAuthenticationAssessmentSnapshot>
            snapshots =
                new Dictionary<string, ViewerAuthenticationAssessmentSnapshot>(
                    StringComparer.Ordinal);
        private readonly Dictionary<string, SessionData> lastValidatedSessions =
            new Dictionary<string, SessionData>(StringComparer.Ordinal);
        private readonly HashSet<string> inProgress =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Func<DateTimeOffset> utcNowProvider;

        internal ViewerAuthenticationAssessmentController(
            Func<DateTimeOffset> utcNowProvider = null)
        {
            this.utcNowProvider = utcNowProvider ??
                (() => DateTimeOffset.UtcNow);
        }

        internal bool IsInProgress(string targetId)
        {
            return !string.IsNullOrWhiteSpace(targetId) &&
                   inProgress.Contains(targetId);
        }

        internal bool TryGetSnapshot(
            string targetId,
            out ViewerAuthenticationAssessmentSnapshot snapshot)
        {
            return snapshots.TryGetValue(targetId ?? string.Empty, out snapshot);
        }

        internal bool TryGetSnapshot(
            ViewerAuthenticationTarget target,
            out ViewerAuthenticationAssessmentSnapshot snapshot)
        {
            snapshot = null;
            return target != null &&
                   lastValidatedSessions.TryGetValue(
                       target.Id,
                       out SessionData validatedSession) &&
                   ReferenceEquals(
                       validatedSession,
                       target.Session.SessionService.CurrentSession) &&
                   snapshots.TryGetValue(target.Id, out snapshot);
        }

        internal async Task AssessAsync(
            ViewerAuthenticationTarget target,
            IViewerAuthenticationValidationProvider validationProvider,
            bool forceServerProbe,
            CancellationToken cancellationToken)
        {
            if (target == null)
            {
                return;
            }

            await ViewerAuthenticationTokenExpiryAssessment
                .TryApplyIfMissingAsync(
                    target.Session.SessionService,
                    cancellationToken);

            if (!target.Session.Status.HasAccessToken)
            {
                snapshots.Remove(target.Id);
                lastAttempts.Remove(target.Id);
                lastValidatedSessions.Remove(target.Id);
                return;
            }

            if (validationProvider == null)
            {
                snapshots.Remove(target.Id);
                lastValidatedSessions.Remove(target.Id);
                return;
            }

            DateTimeOffset now = utcNowProvider().ToUniversalTime();
            SessionData currentSession =
                target.Session.SessionService.CurrentSession;
            bool sameSession = lastValidatedSessions.TryGetValue(
                target.Id,
                out SessionData lastValidated) &&
                ReferenceEquals(lastValidated, currentSession);
            if (inProgress.Contains(target.Id) ||
                (!forceServerProbe &&
                 sameSession &&
                 lastAttempts.TryGetValue(
                     target.Id,
                     out DateTimeOffset lastAttempt) &&
                 now - lastAttempt < AutomaticProbeCooldown))
            {
                return;
            }

            lastAttempts[target.Id] = now;
            inProgress.Add(target.Id);
            try
            {
                ViewerAuthenticationValidationResult result;
                try
                {
                    result = await validationProvider.ValidateAsync(
                        target.Session.SessionService,
                        cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception)
                {
                    result = ViewerAuthenticationValidationResult
                        .Inconclusive();
                }

                snapshots[target.Id] =
                    new ViewerAuthenticationAssessmentSnapshot(
                        result ?? ViewerAuthenticationValidationResult
                            .Inconclusive(),
                        utcNowProvider());
                lastValidatedSessions[target.Id] =
                    target.Session.SessionService.CurrentSession;
            }
            catch (OperationCanceledException)
            {
                lastAttempts.Remove(target.Id);
                lastValidatedSessions.Remove(target.Id);
                throw;
            }
            finally
            {
                inProgress.Remove(target.Id);
            }
        }

        internal void Clear(string targetId)
        {
            if (string.IsNullOrWhiteSpace(targetId))
            {
                return;
            }

            snapshots.Remove(targetId);
            lastAttempts.Remove(targetId);
            lastValidatedSessions.Remove(targetId);
            inProgress.Remove(targetId);
        }

    }
}
