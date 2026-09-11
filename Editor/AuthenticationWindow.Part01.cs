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
        public static void Open() => DeucarianEditorToolWindow.Open(DeucarianToolIds.Authentication);
        public static IDeucarianEditorPage CreatePage() => DeucarianEditorWindowPages.Create<AuthenticationWindow>(
            (window, root) => window.BuildPage(root), activate: (window, _) => { window.OnFocus(); window.nativePage?.Activate(); },
            deactivate: window => { window.ResetAuthenticationContext(); window.nativePage?.ClearSensitiveFields(); },
            update: window => window.nativePage?.Update());

        private void CreateGUI()
        {
            pageSession?.Dispose();
            pageSession = new DeucarianEditorPageSession(this, DeucarianToolIds.Authentication, BuildPage);
        }
        private void BuildPage(UnityEngine.UIElements.VisualElement root)
        {
            nativePage?.Dispose();
            nativePage = new AuthenticationPage(root, this);
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
            pageSession?.Dispose(); pageSession = null;
            nativePage?.Dispose(); nativePage = null;
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
    }
}
