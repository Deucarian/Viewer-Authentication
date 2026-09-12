using System.Collections.Generic;

namespace Deucarian.Authentication.Editor
{
    internal sealed class AuthenticationPageState
    {
        internal IReadOnlyList<AuthenticationTarget> Targets;
        internal AuthenticationTarget Target;
        internal AuthenticationPresentationModel Presentation;
        internal AuthenticationEndpointTargetSummary Endpoints;
        internal AuthenticationAssessmentSnapshot Validation;
        internal IReadOnlyList<AuthenticationInputDescriptor> Inputs;
        internal bool Busy, Checking, Credentials, Manual, HasProvider, Failed;
        internal int Generation;
        internal string Message, Verification;
    }
}
