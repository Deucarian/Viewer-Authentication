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
    }
}
