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


        public static void Open() =>
            DeucarianEditorWindowPages.ShowStandalone<AuthenticationWindow>("Authentication", new Vector2(400f, 460f));

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

        internal void OnFocus()
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

        internal void OnGUI()
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
                    int nextIndex = DeucarianEditorInputGUI.Popup(
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
    }
}
