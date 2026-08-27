using System;
using Deucarian.Authentication.Editor;
using NUnit.Framework;

namespace Deucarian.Authentication.Tests
{
    public sealed class AuthenticationPresentationModelTests
    {
        private static readonly DateTimeOffset Now =
            new DateTimeOffset(2026, 8, 19, 9, 0, 0, TimeSpan.Zero);

        [Test]
        public void CheckingOverridesTheCurrentLifecycleAndDisablesActions()
        {
            AuthenticationPresentationModel model = Resolve(
                AuthenticationStatus.Active,
                true,
                validation: AuthenticationValidationResult.Verified(),
                checking: true,
                acquisition: true);

            Assert.That(model.StatusLabel, Is.EqualTo("Checking connection"));
            Assert.That(model.Tone,
                Is.EqualTo(AuthenticationPresentationTone.Info));
            Assert.That(model.PrimaryActionEnabled, Is.False);
        }

        [TestCase(
            AuthenticationValidationStatus.Verified,
            "Connected",
            "Success")]
        [TestCase(
            AuthenticationValidationStatus.Rejected,
            "Token rejected",
            "Error")]
        [TestCase(
            AuthenticationValidationStatus.Inconclusive,
            "Unable to verify",
            "Warning")]
        public void ServerResultTakesPriorityOverLocalLifecycle(
            AuthenticationValidationStatus validationStatus,
            string expectedLabel,
            string expectedTone)
        {
            AuthenticationValidationResult validation =
                validationStatus ==
                AuthenticationValidationStatus.Verified
                    ? AuthenticationValidationResult.Verified()
                    : validationStatus ==
                      AuthenticationValidationStatus.Rejected
                        ? AuthenticationValidationResult.Rejected()
                        : AuthenticationValidationResult.Inconclusive();

            AuthenticationPresentationModel model = Resolve(
                AuthenticationStatus.Active,
                true,
                validation: validation,
                acquisition: true);

            Assert.That(model.StatusLabel, Is.EqualTo(expectedLabel));
            Assert.That(model.Tone.ToString(), Is.EqualTo(expectedTone));
        }

        [TestCase(
            AuthenticationStatus.Missing,
            false,
            "Not connected",
            "Disabled")]
        [TestCase(
            AuthenticationStatus.Expired,
            true,
            "Token expired",
            "Error")]
        public void CurrentLocalInvalidityOverridesCachedVerification(
            AuthenticationStatus status,
            bool hasToken,
            string expectedLabel,
            string expectedTone)
        {
            AuthenticationPresentationModel model = Resolve(
                status,
                hasToken,
                validation: AuthenticationValidationResult.Verified(),
                acquisition: true);

            Assert.That(model.StatusLabel, Is.EqualTo(expectedLabel));
            Assert.That(model.Tone.ToString(), Is.EqualTo(expectedTone));
        }

        [TestCase(
            AuthenticationStatus.Missing,
            false,
            "Not connected",
            "Disabled")]
        [TestCase(
            AuthenticationStatus.Active,
            true,
            "Token ready",
            "Info")]
        [TestCase(
            AuthenticationStatus.Expiring,
            true,
            "Expires soon",
            "Warning")]
        [TestCase(
            AuthenticationStatus.Expired,
            true,
            "Token expired",
            "Error")]
        [TestCase(
            AuthenticationStatus.ExpiryUnknown,
            true,
            "Token present",
            "Warning")]
        public void LocalLifecycleHasAnHonestFallbackPresentation(
            AuthenticationStatus status,
            bool hasToken,
            string expectedLabel,
            string expectedTone)
        {
            AuthenticationPresentationModel model = Resolve(
                status,
                hasToken);

            Assert.That(model.StatusLabel, Is.EqualTo(expectedLabel));
            Assert.That(model.Tone.ToString(), Is.EqualTo(expectedTone));
        }

        [Test]
        public void InconclusiveValidationOffersOnlyCheckAgain()
        {
            AuthenticationPresentationModel model = Resolve(
                AuthenticationStatus.Active,
                true,
                validation: AuthenticationValidationResult
                    .Inconclusive(),
                acquisition: true);

            Assert.That(
                model.PrimaryAction,
                Is.EqualTo(
                    AuthenticationPrimaryActionKind.CheckAgain));
            Assert.That(model.PrimaryActionLabel, Is.EqualTo("Check again"));
        }

        [Test]
        public void InvalidInteractiveSessionRevealsCredentialsBeforeDispatch()
        {
            AuthenticationPresentationModel collapsed = Resolve(
                AuthenticationStatus.Missing,
                false,
                acquisition: true,
                interactive: true);
            AuthenticationPresentationModel expanded = Resolve(
                AuthenticationStatus.Missing,
                false,
                acquisition: true,
                interactive: true,
                credentialsExpanded: true,
                requiredValuesPresent: false);

            Assert.That(
                collapsed.PrimaryAction,
                Is.EqualTo(
                    AuthenticationPrimaryActionKind
                        .RevealCredentials));
            Assert.That(collapsed.PrimaryActionLabel, Is.EqualTo("Sign in"));
            Assert.That(
                expanded.PrimaryAction,
                Is.EqualTo(AuthenticationPrimaryActionKind.None));
            Assert.That(expanded.AcquisitionActionEnabled, Is.False);
        }

        [Test]
        public void ExpandedCredentialsEnableSignInOnlyWhenRequiredValuesExist()
        {
            AuthenticationPresentationModel model = Resolve(
                AuthenticationStatus.Missing,
                false,
                acquisition: true,
                interactive: true,
                credentialsExpanded: true,
                requiredValuesPresent: true);

            Assert.That(model.AcquisitionActionEnabled, Is.True);
        }

        [Test]
        public void BusyOperationDisablesExpandedAcquisitionAction()
        {
            AuthenticationPresentationModel model = Resolve(
                AuthenticationStatus.Missing,
                false,
                acquisition: true,
                interactive: true,
                credentialsExpanded: true,
                requiredValuesPresent: true,
                busy: true);

            Assert.That(model.AcquisitionActionEnabled, Is.False);
        }

        [Test]
        public void ActiveTokenWithoutValidatorUsesLocalOnlyCopy()
        {
            AuthenticationPresentationModel model = Resolve(
                AuthenticationStatus.Active,
                true,
                validationProvider: false);

            Assert.That(model.StatusLabel, Is.EqualTo("Token ready"));
            Assert.That(
                model.StatusDetail,
                Is.EqualTo("The token is valid locally."));
        }

        [Test]
        public void ProviderWithoutInteractiveInputsRemainsOneClick()
        {
            AuthenticationPresentationModel model = Resolve(
                AuthenticationStatus.Active,
                true,
                acquisition: true);

            Assert.That(
                model.PrimaryAction,
                Is.EqualTo(AuthenticationPrimaryActionKind.Acquire));
            Assert.That(model.PrimaryActionLabel,
                Is.EqualTo("Get new token"));
        }

        [Test]
        public void MissingProviderOffersManualEntryWithoutCompetingWhenOpen()
        {
            AuthenticationPresentationModel collapsed = Resolve(
                AuthenticationStatus.Missing,
                false);
            AuthenticationPresentationModel expanded = Resolve(
                AuthenticationStatus.Missing,
                false,
                manualExpanded: true);

            Assert.That(
                collapsed.PrimaryAction,
                Is.EqualTo(
                    AuthenticationPrimaryActionKind.RevealManual));
            Assert.That(collapsed.PrimaryActionLabel,
                Is.EqualTo("Enter token"));
            Assert.That(
                expanded.PrimaryAction,
                Is.EqualTo(AuthenticationPrimaryActionKind.None));
        }

        [Test]
        public void InvalidLifecyclePrefersManualRecoveryOverRechecking()
        {
            AuthenticationPresentationModel model = Resolve(
                AuthenticationStatus.Expired,
                true,
                validation: AuthenticationValidationResult
                    .Inconclusive(),
                validationProvider: true);

            Assert.That(
                model.PrimaryAction,
                Is.EqualTo(
                    AuthenticationPrimaryActionKind.RevealManual));
            Assert.That(model.PrimaryActionLabel, Is.EqualTo("Enter token"));
        }

        [Test]
        public void SummaryUsesOnlyTheSanitizedHostAndNeutralTargetBadge()
        {
            AuthenticationEndpointTargetSummary endpoints =
                AuthenticationEndpointTargetSummary.Create(
                    "POST",
                    "https://user:secret@api.example.com/login" +
                    "?token=hidden",
                    "GET",
                    "https://api.example.com/validate");
            AuthenticationPresentationModel model = Resolve(
                AuthenticationStatus.Active,
                true,
                endpoints: endpoints);

            Assert.That(model.TargetLabel, Is.EqualTo("api.example.com"));
            Assert.That(model.TargetBadgeLabel, Is.EqualTo("CURRENT"));
            Assert.That(model.TargetLabel, Does.Not.Contain("secret"));
            Assert.That(model.TargetLabel, Does.Not.Contain("login"));
            Assert.That(model.TargetLabel, Does.Not.Contain("token"));
        }

        [Test]
        public void OpaqueCustomProviderIsNotPresentedAsMissingConfiguration()
        {
            AuthenticationPresentationModel model = Resolve(
                AuthenticationStatus.Active,
                true,
                acquisition: true);

            Assert.That(
                model.TargetLabel,
                Is.EqualTo("Endpoint details unavailable"));
            Assert.That(model.TargetBadgeLabel, Is.EqualTo("CUSTOM"));
        }

        [Test]
        public void MissingProviderAndEndpointArePresentedAsUnset()
        {
            AuthenticationPresentationModel model = Resolve(
                AuthenticationStatus.Missing,
                false,
                validationProvider: false);

            Assert.That(
                model.TargetLabel,
                Is.EqualTo("No backend endpoint configured"));
            Assert.That(model.TargetBadgeLabel, Is.EqualTo("UNSET"));
        }

        [Test]
        public void ExpiryIsHumanizedForTheCompactSummary()
        {
            AuthenticationPresentationModel model = Resolve(
                AuthenticationStatus.Active,
                true,
                expiry: Now.AddMinutes(43));

            Assert.That(model.ExpiryLabel, Is.EqualTo("Expires in 43 minutes"));
        }

        [Test]
        public void ExactOneHourExpiryUsesSingularCopy()
        {
            AuthenticationPresentationModel model = Resolve(
                AuthenticationStatus.Active,
                true,
                expiry: Now.AddHours(1));

            Assert.That(model.ExpiryLabel, Is.EqualTo("Expires in 1 hour"));
        }

        [Test]
        public void DisclosureStateStartsMinimalKeepsFormsExclusiveAndResets()
        {
            var state = new AuthenticationDisclosureState();

            Assert.That(state.ConnectionDetailsExpanded, Is.False);
            Assert.That(state.CredentialsExpanded, Is.False);
            Assert.That(state.ManualToolsExpanded, Is.False);
            Assert.That(state.LocalStorageExpanded, Is.False);

            state.ConnectionDetailsExpanded = true;
            state.SetCredentialsExpanded(true);
            Assert.That(state.CredentialsExpanded, Is.True);
            Assert.That(state.ManualToolsExpanded, Is.False);

            state.SetManualToolsExpanded(true);
            Assert.That(state.CredentialsExpanded, Is.False);
            Assert.That(state.ManualToolsExpanded, Is.True);

            state.SetCredentialsExpanded(true);
            state.LocalStorageExpanded = true;
            state.CompleteAcquisition();

            Assert.That(state.ConnectionDetailsExpanded, Is.True);
            Assert.That(state.CredentialsExpanded, Is.False);
            Assert.That(state.ManualToolsExpanded, Is.False);
            Assert.That(state.LocalStorageExpanded, Is.True);

            state.Reset();
            Assert.That(state.ConnectionDetailsExpanded, Is.False);
            Assert.That(state.CredentialsExpanded, Is.False);
            Assert.That(state.ManualToolsExpanded, Is.False);
            Assert.That(state.LocalStorageExpanded, Is.False);
        }

        private static AuthenticationPresentationModel Resolve(
            AuthenticationStatus status,
            bool hasToken,
            AuthenticationValidationResult validation = null,
            bool checking = false,
            bool acquisition = false,
            bool interactive = false,
            bool credentialsExpanded = false,
            bool manualExpanded = false,
            bool requiredValuesPresent = true,
            DateTimeOffset? expiry = null,
            AuthenticationEndpointTargetSummary endpoints = null,
            bool validationProvider = true,
            bool busy = false)
        {
            return AuthenticationPresentationModel.Resolve(
                new AuthenticationPresentationInput
                {
                    Status = new AuthenticationStatusSnapshot(
                        status,
                        hasToken,
                        false,
                        expiry),
                    Validation = validation == null
                        ? null
                        : new AuthenticationAssessmentSnapshot(
                            validation,
                            Now),
                    Endpoints = endpoints,
                    IsChecking = checking,
                    IsBusy = busy,
                    HasValidationProvider = validationProvider,
                    HasAcquisitionProvider = acquisition,
                    HasAnyProvider = acquisition || validationProvider,
                    HasInteractiveInputs = interactive,
                    CredentialsExpanded = credentialsExpanded,
                    ManualExpanded = manualExpanded,
                    RequiredAcquisitionValuesPresent = requiredValuesPresent,
                    UtcNow = Now
                });
        }
    }
}
