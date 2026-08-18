using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.Session;
using Deucarian.ViewerAuthentication.Editor;
using NUnit.Framework;

namespace Deucarian.ViewerAuthentication.Tests
{
    public sealed class ViewerAuthenticationMenuModelTests
    {
        [TestCase(0, false)]
        [TestCase(1, false)]
        [TestCase(2, true)]
        [TestCase(4, true)]
        public void SelectorAppearsOnlyForMultipleConfigurations(
            int configurationCount,
            bool expected)
        {
            Assert.That(
                ViewerAuthenticationMenuModel
                    .ShouldShowConfigurationSelector(configurationCount),
                Is.EqualTo(expected));
        }

        [Test]
        public void OnlyVerifiedSessionIsEligibleForLocalRemembering()
        {
            DateTimeOffset checkedAt = DateTimeOffset.UtcNow;
            var verified = new ViewerAuthenticationAssessmentSnapshot(
                ViewerAuthenticationValidationResult.Verified(),
                checkedAt);
            var inconclusive = new ViewerAuthenticationAssessmentSnapshot(
                ViewerAuthenticationValidationResult.Inconclusive(),
                checkedAt);

            Assert.That(
                ViewerAuthenticationMenuModel
                    .ShouldRememberVerifiedSession(
                        true,
                        true,
                        verified),
                Is.True);
            Assert.That(
                ViewerAuthenticationMenuModel
                    .ShouldRememberVerifiedSession(
                        false,
                        true,
                        verified),
                Is.False);
            Assert.That(
                ViewerAuthenticationMenuModel
                    .ShouldRememberVerifiedSession(
                        true,
                        true,
                        inconclusive),
                Is.False);
        }

        [Test]
        public void EditModeWorkspaceIsEphemeralAndNotGloballyRegistered()
        {
            int registryCountBefore =
                ViewerAuthenticationTargetRegistry.Targets.Count;
            var profiles = ViewerAuthenticationProjectProfiles.CreateForTests(
                null,
                null);

            using (var workspace =
                   new ViewerAuthenticationEditModeWorkspace(
                       "web-viewer-10422",
                       profiles,
                       "Activity Viewer"))
            {
                Assert.That(
                    workspace.Target.Id,
                    Is.EqualTo("web-viewer-10422"));
                Assert.That(
                    workspace.Target.DisplayName,
                    Is.EqualTo("Activity Viewer"));
                Assert.That(
                    ViewerAuthenticationTargetRegistry.Targets.Count,
                    Is.EqualTo(registryCountBefore));
            }
        }

        [Test]
        public async Task AutomaticAssessmentIsThrottledAcrossOpenAndFocus()
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            var controller = new ViewerAuthenticationAssessmentController(
                () => now);
            ViewerAuthenticationTarget target = CreateTarget("automatic");
            await target.Session.ReplaceAccessTokenAsync("opaque-test-token");
            var provider = new RecordingValidationProvider(
                ViewerAuthenticationValidationResult.Verified());

            await controller.AssessAsync(
                target,
                provider,
                false,
                CancellationToken.None);
            await controller.AssessAsync(
                target,
                provider,
                false,
                CancellationToken.None);

            Assert.That(provider.CallCount, Is.EqualTo(1));
            Assert.That(
                controller.TryGetSnapshot(target.Id, out var snapshot),
                Is.True);
            Assert.That(
                snapshot.Result.Status,
                Is.EqualTo(ViewerAuthenticationValidationStatus.Verified));

            now += ViewerAuthenticationAssessmentController
                .AutomaticProbeCooldown + TimeSpan.FromMilliseconds(1);
            await controller.AssessAsync(
                target,
                provider,
                false,
                CancellationToken.None);

            Assert.That(provider.CallCount, Is.EqualTo(2));

            await target.Session.ReplaceAccessTokenAsync(
                "replacement-opaque-token");
            Assert.That(
                controller.TryGetSnapshot(target, out _),
                Is.False,
                "A result for the previous token must not be presented as current.");
        }

        [Test]
        public async Task CancelledAssessmentLeavesNoStaleResultOrBusyState()
        {
            var controller = new ViewerAuthenticationAssessmentController();
            ViewerAuthenticationTarget target = CreateTarget("cancelled");
            await target.Session.ReplaceAccessTokenAsync("opaque-test-token");
            var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var provider = new CancellingValidationProvider();

            Assert.CatchAsync<OperationCanceledException>(async () =>
                await controller.AssessAsync(
                    target,
                    provider,
                    true,
                    cancellation.Token));

            Assert.That(controller.IsInProgress(target.Id), Is.False);
            Assert.That(
                controller.TryGetSnapshot(target.Id, out _),
                Is.False);
        }

        private static ViewerAuthenticationTarget CreateTarget(string id)
        {
            return new ViewerAuthenticationTarget(
                id,
                id,
                ViewerAuthenticationSession.CreateTransient(),
                null,
                null);
        }

        private sealed class RecordingValidationProvider :
            IViewerAuthenticationValidationProvider
        {
            private readonly ViewerAuthenticationValidationResult result;

            internal RecordingValidationProvider(
                ViewerAuthenticationValidationResult validationResult)
            {
                result = validationResult;
            }

            public int CallCount { get; private set; }

            public string DisplayName
            {
                get { return "Test validation"; }
            }

            public Task<ViewerAuthenticationValidationResult> ValidateAsync(
                ISessionService sessionService,
                CancellationToken cancellationToken =
                    default(CancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                CallCount++;
                return Task.FromResult(result);
            }
        }

        private sealed class CancellingValidationProvider :
            IViewerAuthenticationValidationProvider
        {
            public string DisplayName
            {
                get { return "Cancelling validation"; }
            }

            public Task<ViewerAuthenticationValidationResult> ValidateAsync(
                ISessionService sessionService,
                CancellationToken cancellationToken =
                    default(CancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(
                    ViewerAuthenticationValidationResult.Inconclusive());
            }
        }
    }
}
