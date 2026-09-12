using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.Editor;
using Deucarian.Session;
using Deucarian.Session.APIIntegration;
using UnityEditor;
using UnityEngine;

namespace Deucarian.Authentication.Editor
{
    public sealed partial class AuthenticationWindow
    {


        private async void RunScheduledAssessment()
        {
            if (!windowEnabled)
            {
                return;
            }

            if (assessmentCancellation != null)
            {
                assessmentScheduled = true;
                return;
            }

            assessmentScheduled = false;
            bool forceServerProbe = scheduledAssessmentIsForced;
            scheduledAssessmentIsForced = false;
            IReadOnlyList<AuthenticationTarget> targets =
                ResolveAvailableTargets();
            int selectedIndex = AuthenticationMenuModel
                .ResolveSelectedIndex(
                    targets,
                    AuthenticationLocalSettings.instance
                        .SelectedTargetId);
            if (selectedIndex < 0)
            {
                return;
            }

            AuthenticationTarget target = targets[selectedIndex];
            int assessmentGeneration = contextGeneration;
            var cancellation = new CancellationTokenSource();
            assessmentCancellation = cancellation;
            try
            {
                if (editModeWorkspace?.Target == target &&
                    !target.Session.Status.HasAccessToken)
                {
                    AuthenticationLocalSettings settings =
                        AuthenticationLocalSettings.instance;
                    if (settings.TryGetRememberedSessionFor(
                            target,
                            out SessionData persisted))
                    {
                        await editModeWorkspace
                            .LoadRememberedSessionForInspectionAsync(
                                 persisted,
                                cancellation.Token);
                        persisted = null;
                    }
                }

                await assessment.AssessAsync(
                    target,
                    ResolveValidationProvider(target),
                    forceServerProbe,
                    cancellation.Token);
                if (!cancellation.IsCancellationRequested &&
                    assessmentGeneration == contextGeneration)
                {
                    RememberVerifiedSessionWhenEnabled(target);
                }
            }
            catch (OperationCanceledException)
            {
                // Window shutdown and mode changes are normal cancellation paths.
            }
            finally
            {
                cancellation.Dispose();
                if (ReferenceEquals(assessmentCancellation, cancellation))
                {
                    assessmentCancellation = null;
                }

                if (assessmentScheduled && windowEnabled)
                {
                    EditorApplication.delayCall -= RunScheduledAssessment;
                    EditorApplication.delayCall += RunScheduledAssessment;
                }
                if (assessmentGeneration == contextGeneration)
                {
                    Repaint();
                }
            }
        }

        private async void RunOperation(
            AuthenticationTarget target,
            Func<CancellationToken, Task<SessionResult>> operation,
            string successMessage,
            bool rememberOnSuccess,
            bool clearRememberedOnSuccess,
            bool completeAcquisitionOnSuccess = false,
            IDisposable sensitiveState = null)
        {
            if (operationInProgress)
            {
                sensitiveState?.Dispose();
                return;
            }

            operationInProgress = true;
            operationMessage = string.Empty;
            operationFailed = false;
            int operationGeneration = contextGeneration;
            var cancellation = new CancellationTokenSource();
            operationCancellation = cancellation;
            Repaint();

            try
            {
                SessionResult result = await operation(
                    cancellation.Token);
                if (cancellation.IsCancellationRequested ||
                    operationGeneration != contextGeneration)
                {
                    return;
                }

                if (result == null || result.IsFailure)
                {
                    operationFailed = true;
                    operationMessage = SessionTokenEndpointFailures.Describe(result?.Error?.Code);
                    return;
                }

                AuthenticationLocalSettings settings =
                    AuthenticationLocalSettings.instance;
                if (clearRememberedOnSuccess)
                {
                    settings.ClearRememberedToken();
                }
                else if (rememberOnSuccess &&
                         settings.RememberAccessToken)
                {
                    settings.RememberSession(target);
                }

                assessment.Clear(target.Id);
                if (completeAcquisitionOnSuccess)
                {
                    disclosures.CompleteAcquisition();
                    interactiveInputs.ClearAll();
                }
                operationMessage = successMessage;
            }
            catch (OperationCanceledException)
            {
                if (operationGeneration == contextGeneration)
                {
                    operationFailed = true;
                    operationMessage =
                        "The authentication operation was cancelled.";
                }
            }
            catch (Exception)
            {
                if (operationGeneration == contextGeneration)
                {
                    operationFailed = true;
                    operationMessage =
                        "The authentication operation failed unexpectedly.";
                }
            }
            finally
            {
                sensitiveState?.Dispose();
                cancellation.Dispose();
                if (ReferenceEquals(operationCancellation, cancellation))
                {
                    operationCancellation = null;
                }

                if (operationGeneration == contextGeneration)
                {
                    operationInProgress = false;
                    ScheduleAutomaticAssessment(forceServerProbe: true);
                    Repaint();
                }
            }
        }

        private static void CancelAndDetach(
            ref CancellationTokenSource cancellation)
        {
            if (cancellation == null)
            {
                return;
            }

            cancellation.Cancel();
            cancellation = null;
        }

        private static void CancelWithoutDetaching(
            CancellationTokenSource cancellation)
        {
            cancellation?.Cancel();
        }

        private void RememberVerifiedSessionWhenEnabled(
            AuthenticationTarget target)
        {
            AuthenticationLocalSettings settings =
                AuthenticationLocalSettings.instance;
            assessment.TryGetSnapshot(
                    target,
                    out AuthenticationAssessmentSnapshot snapshot);
            if (!AuthenticationMenuModel
                .ShouldRememberVerifiedSession(
                    settings.RememberAccessToken,
                    target.Session.Status.HasAccessToken,
                    snapshot))
            {
                return;
            }

            settings.RememberSession(target);
        }

        private static string ResolveValidationLabel(
            IAuthenticationValidationProvider provider,
            AuthenticationAssessmentSnapshot validation,
            bool checking)
        {
            if (checking)
            {
                return "Checking...";
            }

            if (validation == null)
            {
                return provider == null ? "Not configured" : "Not checked yet";
            }

            switch (validation.Result.Status)
            {
                case AuthenticationValidationStatus.Verified:
                    return "Accepted";
                case AuthenticationValidationStatus.Rejected:
                    return "Rejected";
                default:
                    return "Unable to check";
            }
        }
    }
}
