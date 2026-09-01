using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.Session;

namespace Deucarian.Authentication.Editor
{
    internal sealed class AuthenticationAssessmentSnapshot
    {
        internal AuthenticationAssessmentSnapshot(
            AuthenticationValidationResult result,
            DateTimeOffset checkedAtUtc)
        {
            Result = result;
            CheckedAtUtc = checkedAtUtc.ToUniversalTime();
        }

        internal AuthenticationValidationResult Result { get; }

        internal DateTimeOffset CheckedAtUtc { get; }
    }

    /// <summary>
    /// Enriches unknown JWT expiry locally and throttles optional server probes
    /// so opening and immediately focusing a window does not duplicate requests.
    /// </summary>
    internal sealed class AuthenticationAssessmentController
    {
        internal static readonly TimeSpan AutomaticProbeCooldown =
            TimeSpan.FromMinutes(1);

        private readonly Dictionary<string, DateTimeOffset> lastAttempts =
            new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);
        private readonly Dictionary<string, AuthenticationAssessmentSnapshot>
            snapshots =
                new Dictionary<string, AuthenticationAssessmentSnapshot>(
                    StringComparer.Ordinal);
        private readonly Dictionary<string, SessionData> lastValidatedSessions =
            new Dictionary<string, SessionData>(StringComparer.Ordinal);
        private readonly HashSet<string> inProgress =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Func<DateTimeOffset> utcNowProvider;

        internal AuthenticationAssessmentController(
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
            out AuthenticationAssessmentSnapshot snapshot)
        {
            return snapshots.TryGetValue(targetId ?? string.Empty, out snapshot);
        }

        internal bool TryGetSnapshot(
            AuthenticationTarget target,
            out AuthenticationAssessmentSnapshot snapshot)
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
            AuthenticationTarget target,
            IAuthenticationValidationProvider validationProvider,
            bool forceServerProbe,
            CancellationToken cancellationToken)
        {
            if (target == null)
            {
                return;
            }

            await AuthenticationTokenExpiryAssessment
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
                AuthenticationValidationResult result;
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
                    result = AuthenticationValidationResult
                        .Inconclusive();
                }

                cancellationToken.ThrowIfCancellationRequested();

                AuthenticationValidationResult effectiveResult =
                    result ?? AuthenticationValidationResult.Inconclusive();
                if (effectiveResult.Status ==
                    AuthenticationValidationStatus.Rejected)
                {
                    await target.Session.ClearAsync(cancellationToken);
                    AuthenticationLocalSettings settings =
                        AuthenticationLocalSettings.instance;
                    if (settings.HasRememberedAccessTokenFor(target))
                    {
                        settings.ClearRememberedToken();
                    }
                }

                snapshots[target.Id] =
                    new AuthenticationAssessmentSnapshot(
                        effectiveResult,
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

        internal void ClearAll()
        {
            snapshots.Clear();
            lastAttempts.Clear();
            lastValidatedSessions.Clear();
            // Keep active membership until each cancelled assessment reaches
            // its own finally block. This prevents a replacement assessment
            // for the same target from overlapping the cancelled request.
        }

    }
}
