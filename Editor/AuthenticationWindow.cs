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
    public sealed partial class AuthenticationWindow : EditorWindow
    {
        private readonly AuthenticationTransientInputState
            interactiveInputs =
                new AuthenticationTransientInputState();
        private readonly AuthenticationAssessmentController assessment =
            new AuthenticationAssessmentController();
        private readonly AuthenticationDisclosureState disclosures =
            new AuthenticationDisclosureState();

        private AuthenticationPage nativePage;
        private DeucarianEditorPageSession pageSession;
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
    }
}
