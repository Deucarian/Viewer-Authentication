using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.Editor;
using Deucarian.Session;
using UnityEditor;
using UnityEngine;

namespace Deucarian.Authentication.Editor
{
    public sealed partial class AuthenticationWindow
    {


        private void DrawInteractiveInputs(
            IReadOnlyList<AuthenticationInputDescriptor> descriptors)
        {
            if (descriptors == null)
            {
                return;
            }

            for (int i = 0; i < descriptors.Count; i++)
            {
                AuthenticationInputDescriptor descriptor =
                    descriptors[i];
                if (descriptor == null)
                {
                    continue;
                }

                DeucarianEditorFieldRow.Draw(
                    descriptor.DisplayName,
                    () =>
                    {
                        string current =
                            interactiveInputs.GetValue(descriptor.Key);
                        string next = descriptor.IsSecret
                            ? EditorGUILayout.PasswordField(current)
                            : EditorGUILayout.TextField(current);
                        interactiveInputs.SetValue(descriptor.Key, next);
                    },
                    descriptor.Description);
            }
        }

        private void AcquireToken(
            AuthenticationTarget target,
            IAuthenticationAcquisitionProvider provider,
            IInteractiveAuthenticationAcquisitionProvider
                interactiveProvider,
            IReadOnlyList<AuthenticationInputDescriptor> descriptors)
        {
            if (interactiveProvider != null)
            {
                AuthenticationInputValues inputValues =
                    interactiveInputs.CreateValues(descriptors);
                interactiveInputs.ClearSecrets(descriptors);
                GUI.FocusControl(null);
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

        private void DrawManualToolsContent(AuthenticationTarget target)
        {
            AuthenticationStatusSnapshot status = target.Session.Status;
            GUILayout.Space(DeucarianEditorSpacing.Small);
            DeucarianEditorFieldRow.Draw(
                "Access token",
                () => replacementToken =
                    EditorGUILayout.PasswordField(replacementToken),
                "Raw token or a Bearer-prefixed value. Cleared immediately after submission.");

            using (new EditorGUILayout.HorizontalScope())
            {
                if (DeucarianEditorButtons.Secondary(
                        "Clear session",
                        status.HasAccessToken &&
                        !operationInProgress &&
                        !assessment.IsInProgress(target.Id),
                        GUILayout.Width(110f)))
                {
                    RunOperation(
                        target,
                        target.Session.ClearAsync,
                        "Authentication session cleared.",
                        rememberOnSuccess: false,
                        clearRememberedOnSuccess: true);
                }

                if (DeucarianEditorButtons.Primary(
                        "Replace token",
                        !operationInProgress &&
                        !assessment.IsInProgress(target.Id) &&
                        !string.IsNullOrWhiteSpace(replacementToken),
                        GUILayout.ExpandWidth(true)))
                {
                    string token = replacementToken;
                    replacementToken = string.Empty;
                    RunOperation(
                        target,
                        cancellationToken =>
                            target.Session.ReplaceAccessTokenAsync(
                                token,
                                null,
                                cancellationToken),
                        "Access token replaced.",
                        rememberOnSuccess: true,
                        clearRememberedOnSuccess: false);
                    token = null;
                }
            }
        }

        private void DrawStandaloneLocalStorage(
            AuthenticationTarget target)
        {
            DeucarianEditorCards.DrawCard(
                null,
                () =>
                {
                    AuthenticationLocalSettings settings =
                        AuthenticationLocalSettings.instance;
                    disclosures.LocalStorageExpanded =
                        DrawDisclosureHeader(
                            disclosures.LocalStorageExpanded,
                            "Local storage",
                            ResolveLocalStorageSummary(settings, target));
                    if (disclosures.LocalStorageExpanded)
                    {
                        DrawLocalStorageContent(target);
                    }
                });
        }

        private void DrawLocalStorageContent(
            AuthenticationTarget target)
        {
            AuthenticationLocalSettings settings =
                AuthenticationLocalSettings.instance;
            bool rememberedForTarget = target != null &&
                settings.HasRememberedAccessTokenFor(target);
            GUILayout.Space(DeucarianEditorSpacing.Small);
            EditorGUILayout.HelpBox(
                "Opt-in storage encrypts the session for the current OS " +
                "account. Raw tokens are never written to Unity settings.",
                MessageType.Info);

            bool remember = DeucarianEditorFieldRow.Toggle(
                "Remember access token",
                settings.RememberAccessToken,
                "Keeps the token hidden and local to this Unity project.");
            if (remember != settings.RememberAccessToken)
            {
                settings.SetRememberAccessToken(remember);
            }

            bool autoApply = settings.AutoApply;
            DeucarianEditorFieldRow.Draw(
                "Auto-apply when missing",
                () => autoApply = EditorGUILayout.Toggle(autoApply),
                "Restores the protected session when its target starts.",
                enabled: remember);
            if (remember && autoApply != settings.AutoApply)
            {
                settings.SetAutoApply(autoApply);
            }

            DeucarianEditorFieldRow.Draw(
                "Stored token",
                () => EditorGUILayout.LabelField(
                    settings.HasRememberedAccessToken
                        ? target == null || rememberedForTarget
                            ? "Present (hidden)"
                            : "Saved for another target (hidden)"
                        : "None"));

            using (new EditorGUILayout.HorizontalScope())
            {
                AuthenticationStatus targetStatus =
                    target?.Session.Status.Status ??
                    AuthenticationStatus.Missing;
                bool targetNeedsToken =
                    targetStatus == AuthenticationStatus.Missing ||
                    targetStatus == AuthenticationStatus.Expired;
                if (DeucarianEditorButtons.Secondary(
                        "Apply saved token",
                        target != null &&
                        targetNeedsToken &&
                        rememberedForTarget &&
                        !operationInProgress &&
                        !assessment.IsInProgress(target.Id),
                        GUILayout.ExpandWidth(true)))
                {
                    if (settings.TryGetRememberedSessionFor(
                            target,
                            out SessionData rememberedSession))
                    {
                        RunOperation(
                            target,
                            cancellationToken =>
                                target.Session.ApplyPersistedSessionAsync(
                                    rememberedSession,
                                    cancellationToken),
                            "Remembered token applied.",
                            rememberOnSuccess: false,
                            clearRememberedOnSuccess: false);
                        rememberedSession = null;
                    }
                }

                if (DeucarianEditorButtons.Secondary(
                        "Forget",
                        settings.HasRememberedAccessToken &&
                        !operationInProgress,
                        GUILayout.Width(82f)))
                {
                    settings.ClearRememberedToken();
                    operationMessage = "Local remembered token cleared.";
                    operationFailed = false;
                }
            }
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

        private void DrawUnavailableState()
        {
            DeucarianEditorCards.DrawCard(
                "Authentication session is starting",
                () => EditorGUILayout.HelpBox(
                    "No live authentication session is registered yet. Configure a service integration and wait for it to register its target.",
                    MessageType.Info));
        }

        private void DrawInlineOperationMessage()
        {
            if (operationInProgress)
            {
                GUILayout.Space(DeucarianEditorSpacing.Small);
                EditorGUILayout.HelpBox(
                    "Authentication operation in progress...",
                    MessageType.Info);
            }
            else if (!string.IsNullOrWhiteSpace(operationMessage))
            {
                GUILayout.Space(DeucarianEditorSpacing.Small);
                EditorGUILayout.HelpBox(
                    operationMessage,
                    operationFailed
                        ? MessageType.Warning
                        : MessageType.Info);
            }
        }

        private void DrawFooter()
        {
            GUILayout.Space(DeucarianEditorSpacing.Tiny);
            DeucarianEditorChrome.DrawFooterVersion(
                "com.deucarian.authentication",
                ResolvePackageVersion());
            GUILayout.Space(DeucarianEditorSpacing.Small);
        }

        private static string ResolvePackageVersion()
        {
            UnityEditor.PackageManager.PackageInfo package =
                UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                    typeof(AuthenticationWindow).Assembly);
            return string.IsNullOrWhiteSpace(package?.version)
                ? "development"
                : package.version;
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
