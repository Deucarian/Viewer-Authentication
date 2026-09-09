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


        private AuthenticationEndpointTargetSummary
            ResolveEndpointSummary(AuthenticationTarget target)
        {
            AuthenticationEndpointProvider acquisition =
                ResolveAcquisitionProvider(target) as
                    AuthenticationEndpointProvider;
            AuthenticationEndpointValidationProvider validation =
                ResolveValidationProvider(target) as
                    AuthenticationEndpointValidationProvider;
            return AuthenticationEndpointTargetSummary.Create(
                acquisition?.Method.ToString(),
                acquisition?.EndpointTemplate,
                validation?.Method.ToString(),
                validation?.EndpointTemplate);
        }

        private static bool HasInteractiveInputs(
            IReadOnlyList<AuthenticationInputDescriptor> descriptors)
        {
            if (descriptors == null)
            {
                return false;
            }

            for (int i = 0; i < descriptors.Count; i++)
            {
                if (descriptors[i] != null)
                {
                    return true;
                }
            }

            return false;
        }

        private void DrawConnectionOverview(
            AuthenticationTarget target,
            AuthenticationPresentationModel presentation,
            AuthenticationEndpointTargetSummary endpoints,
            IAuthenticationAcquisitionProvider acquisitionProvider,
            IInteractiveAuthenticationAcquisitionProvider
                interactiveProvider,
            IReadOnlyList<AuthenticationInputDescriptor> descriptors)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DeucarianEditorStatusBadge.Draw(
                    presentation.StatusLabel,
                    ToEditorStatus(presentation.Tone),
                    GUILayout.MinWidth(118f));
                GUILayout.FlexibleSpace();
                DeucarianEditorStatusBadge.Draw(
                    new GUIContent(
                        presentation.TargetBadgeLabel,
                        "This is the currently configured target. " +
                        "Environment switching is not enabled yet."),
                    endpoints.HasDifferentOrigins
                        ? DeucarianEditorStatus.Warning
                        : DeucarianEditorStatus.Disabled,
                    GUILayout.MinWidth(70f));
            }

            GUILayout.Space(DeucarianEditorSpacing.Small);
            DeucarianEditorTextGUI.LabelField(
                presentation.TargetLabel,
                CreateOverviewTargetStyle());
            DeucarianEditorTextGUI.LabelField(
                presentation.ExpiryLabel + "  ·  " +
                presentation.StatusDetail,
                CreateOverviewDetailStyle(),
                GUILayout.ExpandWidth(true));

            if (endpoints.HasDifferentOrigins)
            {
                DeucarianEditorTextGUI.HelpBox(
                    "Sign-in and token-check routes point to different " +
                    "backend targets. Verify that this is intentional.",
                    MessageType.Warning);
            }

            if (presentation.PrimaryAction !=
                AuthenticationPrimaryActionKind.None)
            {
                GUILayout.Space(DeucarianEditorSpacing.Medium);
                if (DeucarianEditorButtons.Primary(
                        presentation.PrimaryActionLabel,
                        presentation.PrimaryActionEnabled,
                        GUILayout.ExpandWidth(true)))
                {
                    HandlePrimaryAction(
                        target,
                        presentation.PrimaryAction,
                        acquisitionProvider,
                        interactiveProvider,
                        descriptors);
                }
            }
        }

        private void HandlePrimaryAction(
            AuthenticationTarget target,
            AuthenticationPrimaryActionKind action,
            IAuthenticationAcquisitionProvider provider,
            IInteractiveAuthenticationAcquisitionProvider
                interactiveProvider,
            IReadOnlyList<AuthenticationInputDescriptor> descriptors)
        {
            switch (action)
            {
                case AuthenticationPrimaryActionKind.RevealCredentials:
                    disclosures.SetCredentialsExpanded(true);
                    replacementToken = string.Empty;
                    break;
                case AuthenticationPrimaryActionKind.RevealManual:
                    disclosures.SetManualToolsExpanded(true);
                    interactiveInputs.ClearAll();
                    break;
                case AuthenticationPrimaryActionKind.Acquire:
                    AcquireToken(
                        target,
                        provider,
                        interactiveProvider,
                        descriptors);
                    break;
                case AuthenticationPrimaryActionKind.CheckAgain:
                    ScheduleAutomaticAssessment(forceServerProbe: true);
                    break;
            }
        }

        private void DrawConnectionDetails(
            AuthenticationTarget target,
            AuthenticationEndpointTargetSummary summary,
            bool hasAnyProvider,
            IAuthenticationValidationProvider validationProvider,
            AuthenticationAssessmentSnapshot validation,
            bool checking)
        {
            GUILayout.Space(DeucarianEditorSpacing.Small);
            if (!summary.HasAnyEndpoint)
            {
                DeucarianEditorTextGUI.HelpBox(
                    hasAnyProvider
                        ? "The configured authentication provider does not " +
                          "expose endpoint details."
                        : "No endpoint profile is currently configured.",
                    MessageType.Info);
            }
            else
            {
                if (summary.HasDifferentOrigins)
                {
                    DrawEndpointValue(
                        "Sign-in server",
                        summary.SignIn.Origin);
                    DrawEndpointValue(
                        "Token-check server",
                        summary.TokenCheck.Origin);
                }
                else if (!string.IsNullOrWhiteSpace(summary.SharedOrigin))
                {
                    DrawEndpointValue("Current server", summary.SharedOrigin);
                }
                else
                {
                    DrawEndpointValue(
                        "Current server",
                        "Resolved at request time; the endpoint templates " +
                        "do not expose one common HTTP origin.");
                }

                if (summary.SignIn != null)
                {
                    DrawEndpointValue("Sign in", summary.SignIn.DisplayValue);
                }

                if (summary.TokenCheck != null)
                {
                    DrawEndpointValue(
                        "Token check",
                        summary.TokenCheck.DisplayValue);
                }
            }

            DeucarianEditorFieldRow.Draw(
                "Target",
                () => DeucarianEditorTextGUI.LabelField(target.DisplayName));
            DeucarianEditorFieldRow.Draw(
                "Verification",
                () => DeucarianEditorTextGUI.LabelField(
                    ResolveValidationLabel(
                        validationProvider,
                        validation,
                        checking)));
            if (validation != null)
            {
                DeucarianEditorFieldRow.Draw(
                    "Last checked",
                    () => DeucarianEditorTextGUI.LabelField(
                        validation.CheckedAtUtc.ToLocalTime().ToString("u")));
            }

            if (summary.HasAnyEndpoint)
            {
                DeucarianEditorTextGUI.HelpBox(
                    "This project currently uses fixed endpoint profiles. " +
                    "Environment switching is not enabled yet.",
                    MessageType.None);
            }
        }

        private void DrawAcquisitionContent(
            AuthenticationTarget target,
            IAuthenticationAcquisitionProvider provider,
            IInteractiveAuthenticationAcquisitionProvider
                interactiveProvider,
            IReadOnlyList<AuthenticationInputDescriptor> descriptors,
            AuthenticationPresentationModel presentation)
        {
            GUILayout.Space(DeucarianEditorSpacing.Small);
            if (provider == null)
            {
                DeucarianEditorTextGUI.HelpBox(
                    "No sign-in endpoint profile is configured. Use manual " +
                    "token entry below instead.",
                    MessageType.Info);
                return;
            }

            if (interactiveProvider != null)
            {
                DrawInteractiveInputs(descriptors);
            }

            string actionLabel = target.Session.Status.HasAccessToken
                ? "Get new token"
                : "Sign in";
            using (new EditorGUILayout.HorizontalScope())
            {
                if (DeucarianEditorButtons.Secondary(
                        "Cancel",
                        !operationInProgress,
                        GUILayout.Width(88f)))
                {
                    disclosures.SetCredentialsExpanded(false);
                    interactiveInputs.ClearAll();
                    GUI.FocusControl(null);
                }

                if (DeucarianEditorButtons.Primary(
                        actionLabel,
                        presentation.AcquisitionActionEnabled,
                        GUILayout.ExpandWidth(true)))
                {
                    AcquireToken(
                        target,
                        provider,
                        interactiveProvider,
                        descriptors);
                }
            }

            DeucarianEditorTextGUI.LabelField(
                provider is AuthenticationEndpointProvider
                    ? "This signs in again through the configured endpoint; " +
                      "it does not assume a refresh-token route."
                    : "This runs the configured authentication provider and " +
                      "applies the token it returns.",
                CreateOverviewDetailStyle(),
                GUILayout.ExpandWidth(true));
        }

        private static bool DrawDisclosureHeader(
            bool expanded,
            string title,
            string summary)
        {
            GUILayout.Space(DeucarianEditorSpacing.Small);
            bool next = DeucarianEditorInputGUI.Foldout(
                expanded,
                title,
                true,
                CreateDisclosureStyle());
            if (!string.IsNullOrWhiteSpace(summary))
            {
                DeucarianEditorTextGUI.LabelField(
                    summary,
                    DeucarianEditorStyles.MutedLabel);
            }

            return next;
        }

        private static void DrawDivider()
        {
            GUILayout.Space(DeucarianEditorSpacing.Small);
            Rect line = GUILayoutUtility.GetRect(
                1f,
                1f,
                GUILayout.ExpandWidth(true));
            if (Event.current != null &&
                Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(line, DeucarianEditorTheme.BorderSubtle);
            }
        }

        private static GUIStyle CreateOverviewTargetStyle()
        {
            var style = new GUIStyle(DeucarianEditorWorkbenchGUI.BoldLabelStyle)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            style.normal.textColor = DeucarianEditorTheme.Text;
            return style;
        }

        private static GUIStyle CreateOverviewDetailStyle()
        {
            return new GUIStyle(DeucarianEditorStyles.MutedLabel)
            {
                wordWrap = true
            };
        }

        private static GUIStyle CreateDisclosureStyle()
        {
            var style = new GUIStyle(DeucarianEditorWorkbenchGUI.FoldoutStyle)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
            style.normal.textColor = DeucarianEditorTheme.Text;
            style.onNormal.textColor = DeucarianEditorTheme.Text;
            return style;
        }

        private static DeucarianEditorStatus ToEditorStatus(
            AuthenticationPresentationTone tone)
        {
            switch (tone)
            {
                case AuthenticationPresentationTone.Success:
                    return DeucarianEditorStatus.Success;
                case AuthenticationPresentationTone.Warning:
                    return DeucarianEditorStatus.Warning;
                case AuthenticationPresentationTone.Error:
                    return DeucarianEditorStatus.Error;
                case AuthenticationPresentationTone.Disabled:
                    return DeucarianEditorStatus.Disabled;
                default:
                    return DeucarianEditorStatus.Info;
            }
        }

        private void DrawEndpointValue(string label, string value)
        {
            GUILayout.Space(DeucarianEditorSpacing.Tiny);
            DeucarianEditorTextGUI.LabelField(label, DeucarianEditorWorkbenchGUI.RowTitleStyle);
            var style = new GUIStyle(DeucarianEditorWorkbenchGUI.InputStyles.TextArea)
            {
                wordWrap = true
            };
            float availableWidth = Math.Max(
                80f,
                EditorGUIUtility.currentViewWidth - 90f);
            float height = Math.Max(
                EditorGUIUtility.singleLineHeight + 5f,
                style.CalcHeight(
                    new GUIContent(value),
                    availableWidth));
            EditorGUILayout.SelectableLabel(
                value,
                style,
                GUILayout.Height(height),
                GUILayout.ExpandWidth(true));
        }
    }
}
