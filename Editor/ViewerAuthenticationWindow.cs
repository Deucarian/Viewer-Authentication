using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.Editor;
using Deucarian.Session;
using UnityEditor;
using UnityEngine;

namespace Deucarian.ViewerAuthentication.Editor
{
    /// <summary>
    /// Local-only viewer authentication workflow. It works against live viewer
    /// sessions in Play Mode and an ephemeral project-profile session in Edit Mode.
    /// </summary>
    public sealed class ViewerAuthenticationWindow : EditorWindow
    {
        private const string MenuPath =
            "Tools/Deucarian/Viewer/Authentication";

        private readonly ViewerAuthenticationTransientInputState
            interactiveInputs =
                new ViewerAuthenticationTransientInputState();
        private readonly ViewerAuthenticationAssessmentController assessment =
            new ViewerAuthenticationAssessmentController();

        private Vector2 scrollPosition;
        private string replacementToken = string.Empty;
        private string operationMessage = string.Empty;
        private bool operationFailed;
        private bool operationInProgress;
        private bool manualToolsExpanded;
        private bool localStorageExpanded = true;
        private bool windowEnabled;
        private bool assessmentScheduled;
        private bool scheduledAssessmentIsForced;
        private CancellationTokenSource operationCancellation;
        private CancellationTokenSource assessmentCancellation;
        private ViewerAuthenticationProjectProfiles projectProfiles;
        private ViewerAuthenticationEditModeWorkspace editModeWorkspace;

        [MenuItem(MenuPath)]
        public static void Open()
        {
            ViewerAuthenticationWindow window =
                GetWindow<ViewerAuthenticationWindow>(
                    "Viewer Authentication");
            window.minSize = new Vector2(500f, 560f);
            window.Focus();
        }

        private void OnEnable()
        {
            windowEnabled = true;
            projectProfiles = ViewerAuthenticationProjectProfiles.Discover();
            ViewerAuthenticationTargetRegistry.TargetsChanged +=
                OnTargetsChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EnsureEditModeWorkspace();
            ScheduleAutomaticAssessment();
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

        private void OnDisable()
        {
            windowEnabled = false;
            EditorApplication.delayCall -= RunScheduledAssessment;
            ViewerAuthenticationTargetRegistry.TargetsChanged -=
                OnTargetsChanged;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            replacementToken = string.Empty;
            interactiveInputs.ClearAll();
            CancelAndDispose(ref operationCancellation);
            CancelAndDispose(ref assessmentCancellation);
            editModeWorkspace?.Dispose();
            editModeWorkspace = null;
            projectProfiles = null;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode ||
                state == PlayModeStateChange.EnteredPlayMode)
            {
                editModeWorkspace?.Dispose();
                editModeWorkspace = null;
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                projectProfiles =
                    ViewerAuthenticationProjectProfiles.Discover();
                EnsureEditModeWorkspace();
            }

            ScheduleAutomaticAssessment();
            Repaint();
        }

        private void OnTargetsChanged()
        {
            replacementToken = string.Empty;
            interactiveInputs.ClearAll();
            EnsureEditModeWorkspace();
            ScheduleAutomaticAssessment();
            Repaint();
        }

        private void EnsureEditModeWorkspace()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                ViewerAuthenticationTargetRegistry.Targets.Count > 0 ||
                editModeWorkspace != null)
            {
                return;
            }

            projectProfiles = projectProfiles ??
                ViewerAuthenticationProjectProfiles.Discover();
            ViewerAuthenticationLocalSettings settings =
                ViewerAuthenticationLocalSettings.instance;
            editModeWorkspace = new ViewerAuthenticationEditModeWorkspace(
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
                "Viewer Authentication",
                "Get, inspect, and locally reuse a development access token without exposing its value.",
                DeucarianEditorIcons.GetPackageIcon("viewer-authentication"));

            IReadOnlyList<ViewerAuthenticationTarget> targets =
                ResolveAvailableTargets();
            if (targets.Count == 0)
            {
                DrawUnavailableState();
                DrawLocalStorageCard(null);
                DrawOperationMessage();
                DrawFooter();
                EditorGUILayout.EndScrollView();
                return;
            }

            int selectedIndex = ViewerAuthenticationMenuModel
                .ResolveSelectedIndex(
                    targets,
                    ViewerAuthenticationLocalSettings.instance
                        .SelectedTargetId);
            selectedIndex = Math.Max(0, selectedIndex);
            PersistSelectedTargetIfNeeded(targets[selectedIndex]);

            if (ViewerAuthenticationMenuModel
                .ShouldShowConfigurationSelector(targets.Count))
            {
                selectedIndex = DrawConfigurationSelector(
                    targets,
                    selectedIndex);
            }

            ViewerAuthenticationTarget target = targets[selectedIndex];
            DrawBackendTargetCard(target);
            DrawStatusCard(target);
            DrawAcquisitionCard(target);
            DrawManualToolsCard(target);
            DrawLocalStorageCard(target);
            DrawOperationMessage();
            DrawFooter();
            EditorGUILayout.EndScrollView();
        }

        private IReadOnlyList<ViewerAuthenticationTarget>
            ResolveAvailableTargets()
        {
            IReadOnlyList<ViewerAuthenticationTarget> liveTargets =
                ViewerAuthenticationTargetRegistry.Targets;
            if (liveTargets.Count > 0)
            {
                return liveTargets;
            }

            if (!EditorApplication.isPlayingOrWillChangePlaymode &&
                editModeWorkspace?.Target != null)
            {
                return new[] { editModeWorkspace.Target };
            }

            return Array.Empty<ViewerAuthenticationTarget>();
        }

        private int DrawConfigurationSelector(
            IReadOnlyList<ViewerAuthenticationTarget> targets,
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
                        "Viewer",
                        selectedIndex,
                        displayNames);
                    if (nextIndex != selectedIndex)
                    {
                        assessment.Clear(targets[selectedIndex].Id);
                        selectedIndex = nextIndex;
                        ViewerAuthenticationLocalSettings.instance
                            .SetSelectedTarget(targets[selectedIndex].Id);
                        interactiveInputs.ClearAll();
                        operationMessage = string.Empty;
                        ScheduleAutomaticAssessment();
                    }
                },
                "Only shown because more than one live authentication configuration is available.");
            return selectedIndex;
        }

        private void DrawStatusCard(ViewerAuthenticationTarget target)
        {
            ViewerAuthenticationStatusSnapshot status = target.Session.Status;
            IViewerAuthenticationValidationProvider validationProvider =
                ResolveValidationProvider(target);
            bool hasAssessment = assessment.TryGetSnapshot(
                target,
                out ViewerAuthenticationAssessmentSnapshot validation);
            bool checking = assessment.IsInProgress(target.Id);

            DeucarianEditorCards.DrawCard(
                "Token status",
                () =>
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        DeucarianEditorStatusBadge.Draw(
                            GetStatusLabel(status, validation),
                            GetEditorStatus(status, validation),
                            GUILayout.MinWidth(170f));
                        GUILayout.FlexibleSpace();
                        if (validationProvider != null &&
                            status.HasAccessToken &&
                            DeucarianEditorButtons.Secondary(
                                checking ? "Checking..." : "Check Now",
                                !checking && !operationInProgress,
                                GUILayout.Width(112f)))
                        {
                            ScheduleAutomaticAssessment(forceServerProbe: true);
                        }
                    }

                    DeucarianEditorFieldRow.Draw(
                        "Project / viewer",
                        () => EditorGUILayout.LabelField(
                            target.DisplayName));
                    DeucarianEditorFieldRow.Draw(
                        "Access token",
                        () => EditorGUILayout.LabelField(
                            status.HasAccessToken
                                ? "Present (hidden)"
                                : "Missing"));
                    DeucarianEditorFieldRow.Draw(
                        "Expiry",
                        () => EditorGUILayout.LabelField(
                            ResolveExpiryLabel(status, validation)));
                    DeucarianEditorFieldRow.Draw(
                        "Server verification",
                        () => EditorGUILayout.LabelField(
                            ResolveValidationLabel(
                                validationProvider,
                                validation,
                                checking)));
                    if (hasAssessment)
                    {
                        DeucarianEditorFieldRow.Draw(
                            "Last checked",
                            () => EditorGUILayout.LabelField(
                                validation.CheckedAtUtc
                                    .ToLocalTime()
                                    .ToString("u")));
                    }

                    DrawStatusExplanation(
                        status,
                        validationProvider,
                        validation,
                        checking);
                },
                "Expiry metadata is checked locally; a configured validation endpoint verifies server acceptance.");
        }

        private void DrawBackendTargetCard(ViewerAuthenticationTarget target)
        {
            ViewerAuthenticationEndpointProvider acquisition =
                ResolveAcquisitionProvider(target) as
                    ViewerAuthenticationEndpointProvider;
            ViewerAuthenticationEndpointValidationProvider validation =
                ResolveValidationProvider(target) as
                    ViewerAuthenticationEndpointValidationProvider;
            ViewerAuthenticationEndpointTargetSummary summary =
                ViewerAuthenticationEndpointTargetSummary.Create(
                    acquisition?.Method.ToString(),
                    acquisition?.EndpointTemplate,
                    validation?.Method.ToString(),
                    validation?.EndpointTemplate);
            if (!summary.HasAnyEndpoint)
            {
                return;
            }

            DeucarianEditorCards.DrawCard(
                "Backend target",
                () =>
                {
                    if (summary.HasDifferentOrigins)
                    {
                        DrawEndpointValue(
                            "Sign-in server",
                            summary.SignIn.Origin);
                        DrawEndpointValue(
                            "Token-check server",
                            summary.TokenCheck.Origin);
                        EditorGUILayout.HelpBox(
                            "Sign-in and token-check endpoints target " +
                            "different backend origins. Verify that this " +
                            "is intentional.",
                            MessageType.Warning);
                    }
                    else if (!string.IsNullOrWhiteSpace(
                                 summary.SharedOrigin))
                    {
                        DrawEndpointValue(
                            "Current server",
                            summary.SharedOrigin);
                    }
                    else
                    {
                        DrawEndpointValue(
                            "Current server",
                            "Resolved at request time; the endpoint " +
                            "templates do not expose one common HTTP origin.");
                    }

                    if (summary.SignIn != null)
                    {
                        DrawEndpointValue(
                            "Sign in",
                            summary.SignIn.DisplayValue);
                    }

                    if (summary.TokenCheck != null)
                    {
                        DrawEndpointValue(
                            "Token check",
                            summary.TokenCheck.DisplayValue);
                    }
                },
                "Configured by this project's endpoint profiles. " +
                "Environment switching is not enabled yet.");
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

        private void DrawAcquisitionCard(ViewerAuthenticationTarget target)
        {
            IViewerAuthenticationAcquisitionProvider provider =
                ResolveAcquisitionProvider(target);
            IInteractiveViewerAuthenticationAcquisitionProvider
                interactiveProvider = provider as
                    IInteractiveViewerAuthenticationAcquisitionProvider;
            IReadOnlyList<ViewerAuthenticationInputDescriptor> descriptors =
                interactiveProvider?.InputDescriptors;

            DeucarianEditorCards.DrawCard(
                "Get a new token",
                () =>
                {
                    if (provider == null)
                    {
                        EditorGUILayout.HelpBox(
                            "No acquisition profile was found at Resources/Deucarian/ViewerAuthenticationTokenEndpointProfile. Manual token entry remains available below.",
                            MessageType.Info);
                        return;
                    }

                    if (interactiveProvider != null)
                    {
                        DrawInteractiveInputs(descriptors);
                    }

                    bool requiredValuesPresent =
                        interactiveProvider == null ||
                        interactiveInputs.HasRequiredValues(descriptors);
                    bool canAcquire =
                        requiredValuesPresent &&
                        !operationInProgress &&
                        !assessment.IsInProgress(target.Id);
                    if (DeucarianEditorButtons.Primary(
                            "Get New Token",
                            canAcquire,
                            GUILayout.ExpandWidth(true)))
                    {
                        AcquireToken(
                            target,
                            provider,
                            interactiveProvider,
                            descriptors);
                    }

                    EditorGUILayout.HelpBox(
                        "This signs in again through the configured endpoint. It does not claim that the backend has a refresh-token route.",
                        MessageType.None);
                },
                "Use the project's credential-free endpoint profile in both Edit Mode and Play Mode.");
        }

        private void DrawInteractiveInputs(
            IReadOnlyList<ViewerAuthenticationInputDescriptor> descriptors)
        {
            if (descriptors == null)
            {
                return;
            }

            for (int i = 0; i < descriptors.Count; i++)
            {
                ViewerAuthenticationInputDescriptor descriptor =
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
            ViewerAuthenticationTarget target,
            IViewerAuthenticationAcquisitionProvider provider,
            IInteractiveViewerAuthenticationAcquisitionProvider
                interactiveProvider,
            IReadOnlyList<ViewerAuthenticationInputDescriptor> descriptors)
        {
            if (interactiveProvider != null)
            {
                ViewerAuthenticationInputValues inputValues =
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
                clearRememberedOnSuccess: false);
        }

        private void DrawManualToolsCard(ViewerAuthenticationTarget target)
        {
            ViewerAuthenticationStatusSnapshot status = target.Session.Status;
            DeucarianEditorCards.DrawCard(
                "Manual & advanced",
                () =>
                {
                    manualToolsExpanded = EditorGUILayout.Foldout(
                        manualToolsExpanded,
                        "Paste, replace, or clear a token",
                        true);
                    if (!manualToolsExpanded)
                    {
                        return;
                    }

                    GUILayout.Space(DeucarianEditorSpacing.Small);
                    DeucarianEditorFieldRow.Draw(
                        "Access token",
                        () => replacementToken =
                            EditorGUILayout.PasswordField(replacementToken),
                        "Raw token or a value prefixed with Bearer. The input is cleared immediately after submission.");

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        bool canReplace =
                            !operationInProgress &&
                            !assessment.IsInProgress(target.Id) &&
                            !string.IsNullOrWhiteSpace(replacementToken);
                        if (DeucarianEditorButtons.Primary(
                                "Replace Token",
                                canReplace,
                                GUILayout.MinWidth(150f)))
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

                        GUILayout.FlexibleSpace();
                        if (DeucarianEditorButtons.Secondary(
                                "Clear Session",
                                status.HasAccessToken &&
                                !operationInProgress &&
                                !assessment.IsInProgress(target.Id),
                                GUILayout.Width(120f)))
                        {
                            RunOperation(
                                target,
                                target.Session.ClearAsync,
                                "Authentication session cleared.",
                                rememberOnSuccess: false,
                                clearRememberedOnSuccess: true);
                        }
                    }
                },
                "Hidden by default so the normal sign-in path stays clear.");
        }

        private void DrawLocalStorageCard(ViewerAuthenticationTarget target)
        {
            ViewerAuthenticationLocalSettings settings =
                ViewerAuthenticationLocalSettings.instance;
            DeucarianEditorCards.DrawCard(
                "Local development storage",
                () =>
                {
                    localStorageExpanded = EditorGUILayout.Foldout(
                        localStorageExpanded,
                        "Private project-local convenience",
                        true);
                    if (!localStorageExpanded)
                    {
                        return;
                    }

                    GUILayout.Space(DeucarianEditorSpacing.Small);
                    EditorGUILayout.HelpBox(
                        "Opt-in storage writes only to this project's ignored UserSettings folder. It is not an OS credential vault.",
                        MessageType.Warning);

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
                        "Applies the remembered token when a live viewer session starts.",
                        enabled: remember);
                    if (remember && autoApply != settings.AutoApply)
                    {
                        settings.SetAutoApply(autoApply);
                    }

                    DeucarianEditorFieldRow.Draw(
                        "Stored token",
                        () => EditorGUILayout.LabelField(
                            settings.HasRememberedAccessToken
                                ? "Present (hidden)"
                                : "None"));

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        ViewerAuthenticationStatus targetStatus =
                            target?.Session.Status.Status ??
                            ViewerAuthenticationStatus.Missing;
                        bool targetNeedsToken =
                            targetStatus == ViewerAuthenticationStatus.Missing ||
                            targetStatus == ViewerAuthenticationStatus.Expired;
                        if (DeucarianEditorButtons.Primary(
                                "Apply Remembered Token",
                                target != null &&
                                targetNeedsToken &&
                                settings.HasRememberedAccessToken &&
                                !operationInProgress,
                                GUILayout.MinWidth(180f)))
                        {
                            string rememberedToken =
                                settings.RememberedAccessToken;
                            RunOperation(
                                target,
                                cancellationToken =>
                                    target.Session.ReplaceAccessTokenAsync(
                                        rememberedToken,
                                        null,
                                        cancellationToken),
                                "Remembered token applied.",
                                rememberOnSuccess: false,
                                clearRememberedOnSuccess: false);
                            rememberedToken = null;
                        }

                        GUILayout.FlexibleSpace();
                        if (DeucarianEditorButtons.Secondary(
                                "Forget",
                                settings.HasRememberedAccessToken &&
                                !operationInProgress,
                                GUILayout.Width(86f)))
                        {
                            settings.ClearRememberedToken();
                            operationMessage =
                                "Local remembered token cleared.";
                            operationFailed = false;
                        }
                    }
                },
                "Never stored in an asset or source control.");
        }

        private void DrawUnavailableState()
        {
            DeucarianEditorCards.DrawCard(
                "Viewer session is starting",
                () => EditorGUILayout.HelpBox(
                    "No live viewer authentication session is registered yet. In Edit Mode the project-profile workspace appears automatically; in Play Mode wait for the viewer to initialize.",
                    MessageType.Info));
        }

        private void DrawOperationMessage()
        {
            if (operationInProgress)
            {
                DeucarianEditorStatusPanel.DrawStatusCard(
                    "Authentication operation in progress...",
                    DeucarianEditorStatus.Info);
            }
            else if (!string.IsNullOrWhiteSpace(operationMessage))
            {
                DeucarianEditorStatusPanel.DrawStatusCard(
                    operationMessage,
                    operationFailed
                        ? DeucarianEditorStatus.Warning
                        : DeucarianEditorStatus.Success);
            }
        }

        private void DrawFooter()
        {
            GUILayout.Space(DeucarianEditorSpacing.Tiny);
            DeucarianEditorChrome.DrawFooterVersion(
                "com.deucarian.viewer-authentication",
                ResolvePackageVersion());
            GUILayout.Space(DeucarianEditorSpacing.Small);
        }

        private static string ResolvePackageVersion()
        {
            UnityEditor.PackageManager.PackageInfo package =
                UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                    typeof(ViewerAuthenticationWindow).Assembly);
            return string.IsNullOrWhiteSpace(package?.version)
                ? "development"
                : package.version;
        }

        private void PersistSelectedTargetIfNeeded(
            ViewerAuthenticationTarget target)
        {
            ViewerAuthenticationLocalSettings settings =
                ViewerAuthenticationLocalSettings.instance;
            if (!string.Equals(
                    settings.SelectedTargetId,
                    target.Id,
                    StringComparison.Ordinal))
            {
                settings.SetSelectedTarget(target.Id);
            }
        }

        private IViewerAuthenticationAcquisitionProvider
            ResolveAcquisitionProvider(ViewerAuthenticationTarget target)
        {
            return target?.AcquisitionProvider ??
                   projectProfiles?.AcquisitionProvider;
        }

        private IViewerAuthenticationValidationProvider
            ResolveValidationProvider(ViewerAuthenticationTarget target)
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
            IReadOnlyList<ViewerAuthenticationTarget> targets =
                ResolveAvailableTargets();
            int selectedIndex = ViewerAuthenticationMenuModel
                .ResolveSelectedIndex(
                    targets,
                    ViewerAuthenticationLocalSettings.instance
                        .SelectedTargetId);
            if (selectedIndex < 0)
            {
                return;
            }

            ViewerAuthenticationTarget target = targets[selectedIndex];
            var cancellation = new CancellationTokenSource();
            assessmentCancellation = cancellation;
            try
            {
                if (editModeWorkspace?.Target == target &&
                    !target.Session.Status.HasAccessToken)
                {
                    ViewerAuthenticationLocalSettings settings =
                        ViewerAuthenticationLocalSettings.instance;
                    if (settings.HasRememberedAccessToken)
                    {
                        string token = settings.RememberedAccessToken;
                        await editModeWorkspace
                            .LoadRememberedTokenForInspectionAsync(
                                 token,
                                cancellation.Token);
                        token = null;
                    }
                }

                await assessment.AssessAsync(
                    target,
                    ResolveValidationProvider(target),
                    forceServerProbe,
                    cancellation.Token);
                RememberVerifiedSessionWhenEnabled(target);
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
                Repaint();
            }
        }

        private async void RunOperation(
            ViewerAuthenticationTarget target,
            Func<CancellationToken, Task<SessionResult>> operation,
            string successMessage,
            bool rememberOnSuccess,
            bool clearRememberedOnSuccess,
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
            var cancellation = new CancellationTokenSource();
            operationCancellation = cancellation;
            Repaint();

            try
            {
                SessionResult result = await operation(
                    cancellation.Token);
                if (result == null || result.IsFailure)
                {
                    operationFailed = true;
                    operationMessage =
                        "The authentication operation failed. Check the configured endpoint and supplied values.";
                    return;
                }

                ViewerAuthenticationLocalSettings settings =
                    ViewerAuthenticationLocalSettings.instance;
                if (clearRememberedOnSuccess)
                {
                    settings.ClearRememberedToken();
                }
                else if (rememberOnSuccess &&
                         settings.RememberAccessToken)
                {
                    settings.RememberToken(
                        target.Id,
                        target.Session.AccessToken);
                }

                assessment.Clear(target.Id);
                operationMessage = successMessage;
            }
            catch (OperationCanceledException)
            {
                operationFailed = true;
                operationMessage =
                    "The authentication operation was cancelled.";
            }
            catch (Exception)
            {
                operationFailed = true;
                operationMessage =
                    "The authentication operation failed unexpectedly.";
            }
            finally
            {
                sensitiveState?.Dispose();
                operationInProgress = false;
                cancellation.Dispose();
                if (ReferenceEquals(operationCancellation, cancellation))
                {
                    operationCancellation = null;
                }

                ScheduleAutomaticAssessment(forceServerProbe: true);
                Repaint();
            }
        }

        private static void CancelAndDispose(
            ref CancellationTokenSource cancellation)
        {
            if (cancellation == null)
            {
                return;
            }

            cancellation.Cancel();
            cancellation.Dispose();
            cancellation = null;
        }

        private void RememberVerifiedSessionWhenEnabled(
            ViewerAuthenticationTarget target)
        {
            ViewerAuthenticationLocalSettings settings =
                ViewerAuthenticationLocalSettings.instance;
            assessment.TryGetSnapshot(
                    target,
                    out ViewerAuthenticationAssessmentSnapshot snapshot);
            if (!ViewerAuthenticationMenuModel
                .ShouldRememberVerifiedSession(
                    settings.RememberAccessToken,
                    target.Session.Status.HasAccessToken,
                    snapshot))
            {
                return;
            }

            settings.RememberToken(
                target.Id,
                target.Session.AccessToken);
        }

        private static void DrawStatusExplanation(
            ViewerAuthenticationStatusSnapshot status,
            IViewerAuthenticationValidationProvider provider,
            ViewerAuthenticationAssessmentSnapshot validation,
            bool checking)
        {
            if (checking)
            {
                EditorGUILayout.HelpBox(
                    "Checking whether the server accepts the current token...",
                    MessageType.Info);
                return;
            }

            if (validation != null)
            {
                switch (validation.Result.Status)
                {
                    case ViewerAuthenticationValidationStatus.Verified:
                        EditorGUILayout.HelpBox(
                            "The validation endpoint accepted the current token.",
                            MessageType.Info);
                        return;
                    case ViewerAuthenticationValidationStatus.Rejected:
                        EditorGUILayout.HelpBox(
                            "The validation endpoint rejected the current token. The locally remembered token was not deleted.",
                            MessageType.Error);
                        return;
                    default:
                        EditorGUILayout.HelpBox(
                            "The server check was inconclusive. This can be a network, server, response, or configuration problem; the token was not treated as rejected.",
                            MessageType.Warning);
                        return;
                }
            }

            if (!status.HasAccessToken)
            {
                EditorGUILayout.HelpBox(
                    "Get a new token below or apply a remembered one.",
                    MessageType.Info);
            }
            else if (status.Status ==
                     ViewerAuthenticationStatus.ExpiryUnknown)
            {
                EditorGUILayout.HelpBox(
                    provider == null
                        ? "This token has no readable JWT expiry metadata. Add the optional validation profile to verify it with the server."
                        : "Expiry is not readable locally. The configured server validation will provide the authoritative acceptance check.",
                    MessageType.Warning);
            }
            else if (provider == null)
            {
                EditorGUILayout.HelpBox(
                    "The time is based on local JWT or endpoint metadata. It does not prove that the server currently accepts the token.",
                    MessageType.None);
            }
        }

        private static string GetStatusLabel(
            ViewerAuthenticationStatusSnapshot status,
            ViewerAuthenticationAssessmentSnapshot validation)
        {
            if (validation != null)
            {
                if (validation.Result.Status ==
                    ViewerAuthenticationValidationStatus.Verified)
                {
                    return "Server verified";
                }

                if (validation.Result.Status ==
                    ViewerAuthenticationValidationStatus.Rejected)
                {
                    return "Server rejected";
                }
            }

            switch (status.Status)
            {
                case ViewerAuthenticationStatus.Active:
                    return "Not expired locally";
                case ViewerAuthenticationStatus.Expiring:
                    return "Expiring soon";
                case ViewerAuthenticationStatus.Expired:
                    return "Expired locally";
                case ViewerAuthenticationStatus.ExpiryUnknown:
                    return "Expiry unknown";
                default:
                    return "Token missing";
            }
        }

        private static DeucarianEditorStatus GetEditorStatus(
            ViewerAuthenticationStatusSnapshot status,
            ViewerAuthenticationAssessmentSnapshot validation)
        {
            if (validation != null)
            {
                if (validation.Result.Status ==
                    ViewerAuthenticationValidationStatus.Verified)
                {
                    return DeucarianEditorStatus.Success;
                }

                if (validation.Result.Status ==
                    ViewerAuthenticationValidationStatus.Rejected)
                {
                    return DeucarianEditorStatus.Error;
                }
            }

            switch (status.Status)
            {
                case ViewerAuthenticationStatus.Active:
                    return DeucarianEditorStatus.Info;
                case ViewerAuthenticationStatus.Expiring:
                case ViewerAuthenticationStatus.ExpiryUnknown:
                    return DeucarianEditorStatus.Warning;
                case ViewerAuthenticationStatus.Expired:
                    return DeucarianEditorStatus.Error;
                default:
                    return DeucarianEditorStatus.Disabled;
            }
        }

        private static string ResolveExpiryLabel(
            ViewerAuthenticationStatusSnapshot status,
            ViewerAuthenticationAssessmentSnapshot validation)
        {
            DateTimeOffset? expiry = status.ExpiresAtUtc ??
                                     validation?.Result.ExpiresAtUtc;
            return expiry.HasValue
                ? expiry.Value.ToLocalTime().ToString("u")
                : "Unknown";
        }

        private static string ResolveValidationLabel(
            IViewerAuthenticationValidationProvider provider,
            ViewerAuthenticationAssessmentSnapshot validation,
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
                case ViewerAuthenticationValidationStatus.Verified:
                    return "Accepted";
                case ViewerAuthenticationValidationStatus.Rejected:
                    return "Rejected";
                default:
                    return "Unable to check";
            }
        }
    }
}
