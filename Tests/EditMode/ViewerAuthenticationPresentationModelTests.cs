using System;
using Deucarian.ViewerAuthentication.Editor;
using NUnit.Framework;

namespace Deucarian.ViewerAuthentication.Tests
{
    public sealed class ViewerAuthenticationPresentationModelTests
    {
        private static readonly DateTimeOffset Now =
            new DateTimeOffset(2026, 8, 19, 9, 0, 0, TimeSpan.Zero);

        [Test]
        public void CheckingOverridesTheCurrentLifecycleAndDisablesActions()
        {
            ViewerAuthenticationPresentationModel model = Resolve(
                ViewerAuthenticationStatus.Active,
                true,
                validation: ViewerAuthenticationValidationResult.Verified(),
                checking: true,
                acquisition: true);

            Assert.That(model.StatusLabel, Is.EqualTo("Checking connection"));
            Assert.That(model.Tone,
                Is.EqualTo(ViewerAuthenticationPresentationTone.Info));
            Assert.That(model.PrimaryActionEnabled, Is.False);
        }

        [TestCase(
            ViewerAuthenticationValidationStatus.Verified,
            "Connected",
            "Success")]
        [TestCase(
            ViewerAuthenticationValidationStatus.Rejected,
            "Token rejected",
            "Error")]
        [TestCase(
            ViewerAuthenticationValidationStatus.Inconclusive,
            "Unable to verify",
            "Warning")]
        public void ServerResultTakesPriorityOverLocalLifecycle(
            ViewerAuthenticationValidationStatus validationStatus,
            string expectedLabel,
            string expectedTone)
        {
            ViewerAuthenticationValidationResult validation =
                validationStatus ==
                ViewerAuthenticationValidationStatus.Verified
                    ? ViewerAuthenticationValidationResult.Verified()
                    : validationStatus ==
                      ViewerAuthenticationValidationStatus.Rejected
                        ? ViewerAuthenticationValidationResult.Rejected()
                        : ViewerAuthenticationValidationResult.Inconclusive();

            ViewerAuthenticationPresentationModel model = Resolve(
                ViewerAuthenticationStatus.Active,
                true,
                validation: validation,
                acquisition: true);

            Assert.That(model.StatusLabel, Is.EqualTo(expectedLabel));
            Assert.That(model.Tone.ToString(), Is.EqualTo(expectedTone));
        }

        [TestCase(
            ViewerAuthenticationStatus.Missing,
            false,
            "Not connected",
            "Disabled")]
        [TestCase(
            ViewerAuthenticationStatus.Expired,
            true,
            "Token expired",
            "Error")]
        public void CurrentLocalInvalidityOverridesCachedVerification(
            ViewerAuthenticationStatus status,
            bool hasToken,
            string expectedLabel,
            string expectedTone)
        {
            ViewerAuthenticationPresentationModel model = Resolve(
                status,
                hasToken,
                validation: ViewerAuthenticationValidationResult.Verified(),
                acquisition: true);

            Assert.That(model.StatusLabel, Is.EqualTo(expectedLabel));
            Assert.That(model.Tone.ToString(), Is.EqualTo(expectedTone));
        }

        [TestCase(
            ViewerAuthenticationStatus.Missing,
            false,
            "Not connected",
            "Disabled")]
        [TestCase(
            ViewerAuthenticationStatus.Active,
            true,
            "Token ready",
            "Info")]
        [TestCase(
            ViewerAuthenticationStatus.Expiring,
            true,
            "Expires soon",
            "Warning")]
        [TestCase(
            ViewerAuthenticationStatus.Expired,
            true,
            "Token expired",
            "Error")]
        [TestCase(
            ViewerAuthenticationStatus.ExpiryUnknown,
            true,
            "Token present",
            "Warning")]
        public void LocalLifecycleHasAnHonestFallbackPresentation(
            ViewerAuthenticationStatus status,
            bool hasToken,
            string expectedLabel,
            string expectedTone)
        {
            ViewerAuthenticationPresentationModel model = Resolve(
                status,
                hasToken);

            Assert.That(model.StatusLabel, Is.EqualTo(expectedLabel));
            Assert.That(model.Tone.ToString(), Is.EqualTo(expectedTone));
        }

        [Test]
        public void InconclusiveValidationOffersOnlyCheckAgain()
        {
            ViewerAuthenticationPresentationModel model = Resolve(
                ViewerAuthenticationStatus.Active,
                true,
                validation: ViewerAuthenticationValidationResult
                    .Inconclusive(),
                acquisition: true);

            Assert.That(
                model.PrimaryAction,
                Is.EqualTo(
                    ViewerAuthenticationPrimaryActionKind.CheckAgain));
            Assert.That(model.PrimaryActionLabel, Is.EqualTo("Check again"));
        }

        [Test]
        public void InvalidInteractiveSessionRevealsCredentialsBeforeDispatch()
        {
            ViewerAuthenticationPresentationModel collapsed = Resolve(
                ViewerAuthenticationStatus.Missing,
                false,
                acquisition: true,
                interactive: true);
            ViewerAuthenticationPresentationModel expanded = Resolve(
                ViewerAuthenticationStatus.Missing,
                false,
                acquisition: true,
                interactive: true,
                credentialsExpanded: true,
                requiredValuesPresent: false);

            Assert.That(
                collapsed.PrimaryAction,
                Is.EqualTo(
                    ViewerAuthenticationPrimaryActionKind
                        .RevealCredentials));
            Assert.That(collapsed.PrimaryActionLabel, Is.EqualTo("Sign in"));
            Assert.That(
                expanded.PrimaryAction,
                Is.EqualTo(ViewerAuthenticationPrimaryActionKind.None));
            Assert.That(expanded.AcquisitionActionEnabled, Is.False);
        }

        [Test]
        public void ExpandedCredentialsEnableSignInOnlyWhenRequiredValuesExist()
        {
            ViewerAuthenticationPresentationModel model = Resolve(
                ViewerAuthenticationStatus.Missing,
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
            ViewerAuthenticationPresentationModel model = Resolve(
                ViewerAuthenticationStatus.Missing,
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
            ViewerAuthenticationPresentationModel model = Resolve(
                ViewerAuthenticationStatus.Active,
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
            ViewerAuthenticationPresentationModel model = Resolve(
                ViewerAuthenticationStatus.Active,
                true,
                acquisition: true);

            Assert.That(
                model.PrimaryAction,
                Is.EqualTo(ViewerAuthenticationPrimaryActionKind.Acquire));
            Assert.That(model.PrimaryActionLabel,
                Is.EqualTo("Get new token"));
        }

        [Test]
        public void MissingProviderOffersManualEntryWithoutCompetingWhenOpen()
        {
            ViewerAuthenticationPresentationModel collapsed = Resolve(
                ViewerAuthenticationStatus.Missing,
                false);
            ViewerAuthenticationPresentationModel expanded = Resolve(
                ViewerAuthenticationStatus.Missing,
                false,
                manualExpanded: true);

            Assert.That(
                collapsed.PrimaryAction,
                Is.EqualTo(
                    ViewerAuthenticationPrimaryActionKind.RevealManual));
            Assert.That(collapsed.PrimaryActionLabel,
                Is.EqualTo("Enter token"));
            Assert.That(
                expanded.PrimaryAction,
                Is.EqualTo(ViewerAuthenticationPrimaryActionKind.None));
        }

        [Test]
        public void InvalidLifecyclePrefersManualRecoveryOverRechecking()
        {
            ViewerAuthenticationPresentationModel model = Resolve(
                ViewerAuthenticationStatus.Expired,
                true,
                validation: ViewerAuthenticationValidationResult
                    .Inconclusive(),
                validationProvider: true);

            Assert.That(
                model.PrimaryAction,
                Is.EqualTo(
                    ViewerAuthenticationPrimaryActionKind.RevealManual));
            Assert.That(model.PrimaryActionLabel, Is.EqualTo("Enter token"));
        }

        [Test]
        public void SummaryUsesOnlyTheSanitizedHostAndNeutralTargetBadge()
        {
            ViewerAuthenticationEndpointTargetSummary endpoints =
                ViewerAuthenticationEndpointTargetSummary.Create(
                    "POST",
                    "https://user:secret@api.example.com/login" +
                    "?token=hidden",
                    "GET",
                    "https://api.example.com/validate");
            ViewerAuthenticationPresentationModel model = Resolve(
                ViewerAuthenticationStatus.Active,
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
            ViewerAuthenticationPresentationModel model = Resolve(
                ViewerAuthenticationStatus.Active,
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
            ViewerAuthenticationPresentationModel model = Resolve(
                ViewerAuthenticationStatus.Missing,
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
            ViewerAuthenticationPresentationModel model = Resolve(
                ViewerAuthenticationStatus.Active,
                true,
                expiry: Now.AddMinutes(43));

            Assert.That(model.ExpiryLabel, Is.EqualTo("Expires in 43 minutes"));
        }

        [Test]
        public void ExactOneHourExpiryUsesSingularCopy()
        {
            ViewerAuthenticationPresentationModel model = Resolve(
                ViewerAuthenticationStatus.Active,
                true,
                expiry: Now.AddHours(1));

            Assert.That(model.ExpiryLabel, Is.EqualTo("Expires in 1 hour"));
        }

        [Test]
        public void DisclosureStateStartsMinimalKeepsFormsExclusiveAndResets()
        {
            var state = new ViewerAuthenticationDisclosureState();

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

        private static ViewerAuthenticationPresentationModel Resolve(
            ViewerAuthenticationStatus status,
            bool hasToken,
            ViewerAuthenticationValidationResult validation = null,
            bool checking = false,
            bool acquisition = false,
            bool interactive = false,
            bool credentialsExpanded = false,
            bool manualExpanded = false,
            bool requiredValuesPresent = true,
            DateTimeOffset? expiry = null,
            ViewerAuthenticationEndpointTargetSummary endpoints = null,
            bool validationProvider = true,
            bool busy = false)
        {
            return ViewerAuthenticationPresentationModel.Resolve(
                new ViewerAuthenticationPresentationInput
                {
                    Status = new ViewerAuthenticationStatusSnapshot(
                        status,
                        hasToken,
                        false,
                        expiry),
                    Validation = validation == null
                        ? null
                        : new ViewerAuthenticationAssessmentSnapshot(
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
