using System;
using System.Collections.Generic;
using Deucarian.Editor;
using Deucarian.Session;
using UnityEditor;
using UnityEngine;

namespace Deucarian.Authentication.Editor
{
    public sealed partial class AuthenticationWindow
    {
        private void AcquireToken(
            AuthenticationTarget target,
            IAuthenticationAcquisitionProvider provider,
            IInteractiveAuthenticationAcquisitionProvider
                interactiveProvider,
            IReadOnlyList<AuthenticationInputDescriptor> descriptors)
        {
            if (interactiveProvider != null)
            {
                AuthenticationUsernamePreferences.instance.Remember(target, descriptors, interactiveInputs);
                AuthenticationInputValues inputValues =
                    interactiveInputs.CreateValues(descriptors);
                interactiveInputs.ClearSecrets(descriptors);
                nativePage?.ClearSensitiveFields();
                RunOperation(
                    target,
                    cancellationToken => interactiveProvider.AcquireAsync(
                        target.Session.SessionService,
                        inputValues,
                        cancellationToken),
                    "A new token was acquired and applied.",
                    rememberOnSuccess: true,
                    clearRememberedOnSuccess: false,
                    completeAcquisitionOnSuccess: true,
                    sensitiveState: inputValues);
                return;
            }

            RunOperation(
                target,
                cancellationToken => provider.AcquireAsync(
                    target.Session.SessionService,
                    cancellationToken),
                "A new token was acquired and applied.",
                rememberOnSuccess: true,
                clearRememberedOnSuccess: false,
                completeAcquisitionOnSuccess: true);
        }

        private static string ResolveLocalStorageSummary(
            AuthenticationLocalSettings settings,
            AuthenticationTarget target)
        {
            if (!settings.HasRememberedAccessToken)
            {
                return "Project-local convenience";
            }

            return target == null ||
                   settings.HasRememberedAccessTokenFor(target)
                ? "On · token saved"
                : "On · saved for another target";
        }

        private void PersistSelectedTargetIfNeeded(
            AuthenticationTarget target)
        {
            AuthenticationLocalSettings settings =
                AuthenticationLocalSettings.instance;
            if (!string.Equals(
                    settings.SelectedTargetId,
                    target.Id,
                    StringComparison.Ordinal))
            {
                settings.SetSelectedTarget(target.Id);
            }
        }

        private IAuthenticationAcquisitionProvider
            ResolveAcquisitionProvider(AuthenticationTarget target)
        {
            return target?.AcquisitionProvider ??
                   projectProfiles?.AcquisitionProvider;
        }

        private IAuthenticationValidationProvider
            ResolveValidationProvider(AuthenticationTarget target)
        {
            return target?.ValidationProvider ??
                   projectProfiles?.ValidationProvider;
        }

        private void ScheduleAutomaticAssessment(
            bool forceServerProbe = false)
        {
            if (!windowEnabled)
            {
                return;
            }

            scheduledAssessmentIsForced |= forceServerProbe;
            assessmentScheduled = true;
            EditorApplication.delayCall -= RunScheduledAssessment;
            EditorApplication.delayCall += RunScheduledAssessment;
        }
    }
}
