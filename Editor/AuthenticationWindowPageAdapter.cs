using System;
using Deucarian.Session;

namespace Deucarian.Authentication.Editor
{
    public sealed partial class AuthenticationWindow
    {
        internal AuthenticationPageState CapturePage()
        {
            var targets = ResolveAvailableTargets();
            int index = AuthenticationMenuModel.ResolveSelectedIndex(targets, AuthenticationLocalSettings.instance.SelectedTargetId);
            var target = index >= 0 ? targets[index] : null;
            var state = new AuthenticationPageState { Targets = targets, Target = target, Busy = operationInProgress,
                Generation = contextGeneration, Credentials = disclosures.CredentialsExpanded, Manual = disclosures.ManualToolsExpanded,
                Message = operationInProgress ? "Authentication operation in progress…" : operationMessage };
            if (target == null) return state;
            PersistSelectedTargetIfNeeded(target);
            var provider = ResolveAcquisitionProvider(target);
            var interactive = provider as IInteractiveAuthenticationAcquisitionProvider;
            var validator = ResolveValidationProvider(target);
            assessment.TryGetSnapshot(target, out var validation);
            state.Endpoints = ResolveEndpointSummary(target);
            state.Inputs = interactive?.InputDescriptors;
            state.Checking = assessment.IsInProgress(target.Id);
            state.HasProvider = provider != null;
            state.Validation = validation;
            state.Verification = ResolveValidationLabel(validator, validation, state.Checking);
            state.Presentation = AuthenticationPresentationModel.Resolve(new AuthenticationPresentationInput {
                Status = target.Session.Status, Validation = validation, Endpoints = state.Endpoints,
                IsChecking = state.Checking, IsBusy = operationInProgress, HasValidationProvider = validator != null,
                HasAcquisitionProvider = provider != null, HasAnyProvider = provider != null || validator != null,
                HasInteractiveInputs = HasInteractiveInputs(state.Inputs), CredentialsExpanded = state.Credentials,
                ManualExpanded = state.Manual, RequiredAcquisitionValuesPresent = interactive == null || interactiveInputs.HasRequiredValues(state.Inputs),
                UtcNow = DateTimeOffset.UtcNow });
            return state;
        }

        internal void SelectPageTarget(AuthenticationTarget target)
        {
            if (target == null) return;
            AuthenticationLocalSettings.instance.SetSelectedTarget(target.Id);
            ResetAuthenticationContext(); ScheduleAutomaticAssessment();
        }

        internal string ReadInput(string key) => interactiveInputs.GetValue(key);
        internal void RestoreUsername(AuthenticationTarget target, AuthenticationInputDescriptor descriptor)
        {
            if (!interactiveInputs.Contains(descriptor.Key))
            {
                string remembered = AuthenticationUsernamePreferences.instance.Read(target, descriptor);
                if (!string.IsNullOrEmpty(remembered)) interactiveInputs.SetValue(descriptor.Key, remembered);
            }
        }
        internal void WriteInput(string key, string value) => interactiveInputs.SetValue(key, value);
        internal void ExpandCredentials(bool expanded)
        {
            disclosures.SetCredentialsExpanded(expanded);
            if (expanded) replacementToken = string.Empty;
            else interactiveInputs.ClearAll();
        }
        internal void ExpandManual(bool expanded)
        {
            disclosures.SetManualToolsExpanded(expanded);
            if (expanded) interactiveInputs.ClearAll();
            else replacementToken = string.Empty;
        }
        internal string Replacement { get => replacementToken; set => replacementToken = value ?? string.Empty; }

        internal void InvokePagePrimary(AuthenticationPageState state)
        {
            if (state?.Target == null || !state.Presentation.PrimaryActionEnabled) return;
            var provider = ResolveAcquisitionProvider(state.Target);
            HandlePrimaryAction(state.Target, state.Presentation.PrimaryAction, provider,
                provider as IInteractiveAuthenticationAcquisitionProvider, state.Inputs);
        }

        internal void SignIn(AuthenticationPageState state)
        {
            if (state?.Target == null || !state.Presentation.AcquisitionActionEnabled) return;
            var provider = ResolveAcquisitionProvider(state.Target);
            AcquireToken(state.Target, provider, provider as IInteractiveAuthenticationAcquisitionProvider, state.Inputs);
        }

        internal void Replace(AuthenticationTarget target)
        {
            if (target == null || operationInProgress || assessment.IsInProgress(target.Id) || string.IsNullOrWhiteSpace(replacementToken)) return;
            string token = replacementToken;
            replacementToken = string.Empty;
            nativePage?.ClearSensitiveFields();
            RunOperation(target, cancellation => target.Session.ReplaceAccessTokenAsync(token, null, cancellation),
                "Access token replaced.", true, false);
        }

        internal void ClearSession(AuthenticationTarget target)
        {
            if (target != null && !operationInProgress && !assessment.IsInProgress(target.Id))
                RunOperation(target, target.Session.ClearAsync, "Authentication session cleared.", false, true);
        }

        internal void ApplySaved(AuthenticationTarget target)
        {
            if (target == null || operationInProgress || assessment.IsInProgress(target.Id)) return;
            if (AuthenticationLocalSettings.instance.TryGetRememberedSessionFor(target, out SessionData saved))
                RunOperation(target, cancellation => target.Session.ApplyPersistedSessionAsync(saved, cancellation),
                    "Remembered token applied.", false, false);
        }
    }
}
