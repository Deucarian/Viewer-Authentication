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
    /// <summary>
    /// Local-only authentication workflow for explicitly registered services.
    /// </summary>
    public sealed class AuthenticationWindow : EditorWindow
    {
        private const string MenuPath =
            "Tools/Deucarian/Authentication";

        private readonly AuthenticationTransientInputState
            interactiveInputs =
                new AuthenticationTransientInputState();
        private readonly AuthenticationAssessmentController assessment =
            new AuthenticationAssessmentController();
        private readonly AuthenticationDisclosureState disclosures =
            new AuthenticationDisclosureState();

        private Vector2 scrollPosition;
        private string replacementToken = string.Empty;
        private string operationMessage = string.Empty;
        private bool operationFailed;
        private bool operationInProgress;
        private bool windowEnabled;
        private bool assessmentScheduled;
        private bool scheduledAssessmentIsForced;
        private int contextGeneration;
        private double nextStatusRepaintAt;
        private CancellationTokenSource operationCancellation;
        private CancellationTokenSource assessmentCancellation;
        private AuthenticationProjectProfiles projectProfiles;
        private AuthenticationEditModeWorkspace editModeWorkspace;

        [MenuItem(MenuPath)]
        public static void Open()
        {
            AuthenticationWindow window =
                GetWindow<AuthenticationWindow>(
                    "Authentication");
            window.minSize = new Vector2(400f, 460f);
            window.Focus();
        }

        private void OnEnable()
        {
            windowEnabled = true;
            projectProfiles = AuthenticationProjectProfiles.Discover();
            AuthenticationTargetRegistry.RegistrationsChanged +=
                OnTargetRegistrationsChanged;
            AuthenticationTargetRegistry.TargetsChanged +=
                OnTargetStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EnsureEditModeWorkspace();
            ScheduleAutomaticAssessment();
            nextStatusRepaintAt = EditorApplication.timeSinceStartup;
        }

        private void OnFocus()
        {
            if (!windowEnabled)
            {
                return;
            }

            EnsureEditModeWorkspace();
            ScheduleAutomaticAssessment();
        }

        private void OnInspectorUpdate()
        {
            if (!windowEnabled ||
                EditorApplication.timeSinceStartup < nextStatusRepaintAt)
            {
                return;
            }

            nextStatusRepaintAt =
                EditorApplication.timeSinceStartup + 30d;
            Repaint();
        }

        private void OnDisable()
        {
            windowEnabled = false;
            EditorApplication.delayCall -= RunScheduledAssessment;
            AuthenticationTargetRegistry.RegistrationsChanged -=
                OnTargetRegistrationsChanged;
            AuthenticationTargetRegistry.TargetsChanged -=
                OnTargetStateChanged;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            ResetAuthenticationContext();
            editModeWorkspace?.Dispose();
            editModeWorkspace = null;
            projectProfiles = null;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            ResetAuthenticationContext();
            if (state == PlayModeStateChange.ExitingEditMode ||
                state == PlayModeStateChange.EnteredPlayMode)
            {
                editModeWorkspace?.Dispose();
                editModeWorkspace = null;
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                projectProfiles =
                    AuthenticationProjectProfiles.Discover();
                EnsureEditModeWorkspace();
            }

            ScheduleAutomaticAssessment();
            Repaint();
        }

        private void OnTargetRegistrationsChanged()
        {
            ResetAuthenticationContext();
            EnsureEditModeWorkspace();
            ScheduleAutomaticAssessment();
            Repaint();
        }

        private void OnTargetStateChanged()
        {
            ScheduleAutomaticAssessment();
            Repaint();
        }

        private void EnsureEditModeWorkspace()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                AuthenticationTargetRegistry.Targets.Count > 0 ||
                editModeWorkspace != null)
            {
                return;
            }

            projectProfiles = projectProfiles ??
                AuthenticationProjectProfiles.Discover();
            AuthenticationLocalSettings settings =
                AuthenticationLocalSettings.instance;
            editModeWorkspace = new AuthenticationEditModeWorkspace(
                settings.SelectedTargetId,
                projectProfiles,
                Application.productName);
        }

        private void OnGUI()
        {
            DeucarianEditorWindowChrome.DrawImGuiWindowBackground(position);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            GUILayout.Space(DeucarianEditorSpacing.Small);

            DeucarianEditorChrome.DrawPackageHeader(
                "Authentication",
                "Connect a registered service. Token values stay hidden.",
                DeucarianEditorIcons.GetPackageIcon("authentication"));

            IReadOnlyList<AuthenticationTarget> targets =
                ResolveAvailableTargets();
            if (targets.Count == 0)
            {
                DrawUnavailableState();
                DrawStandaloneLocalStorage(null);
                DrawFooter();
                EditorGUILayout.EndScrollView();
                return;
            }

            int selectedIndex = AuthenticationMenuModel
                .ResolveSelectedIndex(
                    targets,
                    AuthenticationLocalSettings.instance
                        .SelectedTargetId);
            selectedIndex = Math.Max(0, selectedIndex);
            PersistSelectedTargetIfNeeded(targets[selectedIndex]);

            if (AuthenticationMenuModel
                .ShouldShowConfigurationSelector(targets.Count))
            {
                selectedIndex = DrawConfigurationSelector(
                    targets,
                    selectedIndex);
            }

            AuthenticationTarget target = targets[selectedIndex];
            DrawAuthenticationWorkspace(target);
            DrawFooter();
            EditorGUILayout.EndScrollView();
        }

        private IReadOnlyList<AuthenticationTarget>
            ResolveAvailableTargets()
        {
            IReadOnlyList<AuthenticationTarget> liveTargets =
                AuthenticationTargetRegistry.Targets;
            if (liveTargets.Count > 0)
            {
                return liveTargets;
            }

            if (!EditorApplication.isPlayingOrWillChangePlaymode &&
                editModeWorkspace?.Target != null)
            {
                return new[] { editModeWorkspace.Target };
            }

            return Array.Empty<AuthenticationTarget>();
        }

        private int DrawConfigurationSelector(
            IReadOnlyList<AuthenticationTarget> targets,
            int selectedIndex)
        {
            string[] displayNames = new string[targets.Count];
            for (int i = 0; i < targets.Count; i++)
            {
                displayNames[i] = targets[i].DisplayName;
            }

            DeucarianEditorCards.DrawCard(
                "Configuration",
                () =>
                {
                    int nextIndex = EditorGUILayout.Popup(
                        "Target",
                        selectedIndex,
                        displayNames);
                    if (nextIndex != selectedIndex)
                    {
                        assessment.Clear(targets[selectedIndex].Id);
                        selectedIndex = nextIndex;
                        AuthenticationLocalSettings.instance
                            .SetSelectedTarget(targets[selectedIndex].Id);
                        ResetAuthenticationContext();
                        GUI.FocusControl(null);
                        ScheduleAutomaticAssessment();
                    }
                },
                "Only shown because more than one live authentication configuration is available.");
            return selectedIndex;
        }

        private void ClearTransientWorkspaceState()
        {
            replacementToken = string.Empty;
            interactiveInputs.ClearAll();
            disclosures.Reset();
        }

        private void ResetAuthenticationContext()
        {
            contextGeneration++;
            EditorApplication.delayCall -= RunScheduledAssessment;
            assessmentScheduled = false;
            scheduledAssessmentIsForced = false;
            CancelAndDetach(ref operationCancellation);
            CancelWithoutDetaching(assessmentCancellation);
            operationInProgress = false;
            operationMessage = string.Empty;
            operationFailed = false;
            assessment.ClearAll();
            ClearTransientWorkspaceState();
        }

        private void DrawAuthenticationWorkspace(
            AuthenticationTarget target)
        {
            AuthenticationStatusSnapshot status = target.Session.Status;
            IAuthenticationAcquisitionProvider acquisitionProvider =
                ResolveAcquisitionProvider(target);
            IInteractiveAuthenticationAcquisitionProvider
                interactiveProvider = acquisitionProvider as
                    IInteractiveAuthenticationAcquisitionProvider;
            IReadOnlyList<AuthenticationInputDescriptor> descriptors =
                interactiveProvider?.InputDescriptors;
            IAuthenticationValidationProvider validationProvider =
                ResolveValidationProvider(target);
            assessment.TryGetSnapshot(
                target,
                out AuthenticationAssessmentSnapshot validation);
            bool checking = assessment.IsInProgress(target.Id);
            bool requiredValuesPresent = interactiveProvider == null ||
                interactiveInputs.HasRequiredValues(descriptors);
            AuthenticationEndpointTargetSummary endpoints =
                ResolveEndpointSummary(target);
            AuthenticationPresentationModel presentation =
                AuthenticationPresentationModel.Resolve(
                    new AuthenticationPresentationInput
                    {
                        Status = status,
                        Validation = validation,
                        Endpoints = endpoints,
                        IsChecking = checking,
                        IsBusy = operationInProgress,
                        HasValidationProvider = validationProvider != null,
                        HasAcquisitionProvider = acquisitionProvider != null,
                        HasAnyProvider = acquisitionProvider != null ||
                                         validationProvider != null,
                        HasInteractiveInputs =
                            HasInteractiveInputs(descriptors),
                        CredentialsExpanded =
                            disclosures.CredentialsExpanded,
                        ManualExpanded = disclosures.ManualToolsExpanded,
                        RequiredAcquisitionValuesPresent =
                            requiredValuesPresent,
                        UtcNow = DateTimeOffset.UtcNow
                    });

            DeucarianEditorCards.DrawCard(
                null,
                () =>
                {
                    DrawConnectionOverview(
                        target,
                        presentation,
                        endpoints,
                        acquisitionProvider,
                        interactiveProvider,
                        descriptors);
                    DrawInlineOperationMessage();

                    DrawDivider();
                    disclosures.ConnectionDetailsExpanded =
                        DrawDisclosureHeader(
                            disclosures.ConnectionDetailsExpanded,
                            "Connection details",
                            "Current server, routes, and verification");
                    if (disclosures.ConnectionDetailsExpanded)
                    {
                        DrawConnectionDetails(
                            target,
                            endpoints,
                            acquisitionProvider != null ||
                            validationProvider != null,
                            validationProvider,
                            validation,
                            checking);
                    }

                    DrawDivider();
                    bool credentialsWereExpanded =
                        disclosures.CredentialsExpanded;
                    bool credentialsExpanded = DrawDisclosureHeader(
                        disclosures.CredentialsExpanded,
                        "Get a new token",
                        acquisitionProvider == null
                            ? "No authentication provider configured"
                            : acquisitionProvider is
                                AuthenticationEndpointProvider
                                ? "Sign in through the configured endpoint"
                                : "Use the configured authentication provider");
                    if (credentialsExpanded != credentialsWereExpanded)
                    {
                        disclosures.SetCredentialsExpanded(
                            credentialsExpanded);
                        if (credentialsExpanded)
                        {
                            replacementToken = string.Empty;
                        }
                        else
                        {
                            interactiveInputs.ClearAll();
                        }

                        GUI.FocusControl(null);
                    }

                    if (disclosures.CredentialsExpanded)
                    {
                        DrawAcquisitionContent(
                            target,
                            acquisitionProvider,
                            interactiveProvider,
                            descriptors,
                            presentation);
                    }

                    DrawDivider();
                    bool manualWasExpanded =
                        disclosures.ManualToolsExpanded;
                    bool manualExpanded = DrawDisclosureHeader(
                        disclosures.ManualToolsExpanded,
                        "Replace token manually",
                        "Advanced");
                    if (manualExpanded != manualWasExpanded)
                    {
                        disclosures.SetManualToolsExpanded(manualExpanded);
                        if (manualExpanded)
                        {
                            interactiveInputs.ClearAll();
                        }
                        else
                        {
                            replacementToken = string.Empty;
                        }

                        GUI.FocusControl(null);
                    }

                    if (disclosures.ManualToolsExpanded)
                    {
                        DrawManualToolsContent(target);
                    }

                    DrawDivider();
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
            EditorGUILayout.LabelField(
                presentation.TargetLabel,
                CreateOverviewTargetStyle());
            EditorGUILayout.LabelField(
                presentation.ExpiryLabel + "  ·  " +
                presentation.StatusDetail,
                CreateOverviewDetailStyle(),
                GUILayout.ExpandWidth(true));

            if (endpoints.HasDifferentOrigins)
            {
                EditorGUILayout.HelpBox(
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
                EditorGUILayout.HelpBox(
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
                () => EditorGUILayout.LabelField(target.DisplayName));
            DeucarianEditorFieldRow.Draw(
                "Verification",
                () => EditorGUILayout.LabelField(
                    ResolveValidationLabel(
                        validationProvider,
                        validation,
                        checking)));
            if (validation != null)
            {
                DeucarianEditorFieldRow.Draw(
                    "Last checked",
                    () => EditorGUILayout.LabelField(
                        validation.CheckedAtUtc.ToLocalTime().ToString("u")));
            }

            if (summary.HasAnyEndpoint)
            {
                EditorGUILayout.HelpBox(
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
                EditorGUILayout.HelpBox(
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

            EditorGUILayout.LabelField(
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
            bool next = EditorGUILayout.Foldout(
                expanded,
                title,
                true,
                CreateDisclosureStyle());
            if (!string.IsNullOrWhiteSpace(summary))
            {
                EditorGUILayout.LabelField(
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
            var style = new GUIStyle(EditorStyles.boldLabel)
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
            var style = new GUIStyle(EditorStyles.foldout)
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
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
            var style = new GUIStyle(EditorStyles.textArea)
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
                settings.HasRememberedAccessTokenFor(target.Id);
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
                    SessionData rememberedSession = settings.RememberedSession;
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
                   settings.HasRememberedAccessTokenFor(target.Id)
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
                    if (settings.HasRememberedAccessTokenFor(target.Id))
                    {
                        SessionData persisted = settings.RememberedSession;
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
                    operationMessage =
                        "The authentication operation failed. Check the configured endpoint and supplied values.";
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
