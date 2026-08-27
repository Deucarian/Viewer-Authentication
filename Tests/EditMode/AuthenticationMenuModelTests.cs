using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.Session;
using Deucarian.Authentication.Editor;
using NUnit.Framework;

namespace Deucarian.Authentication.Tests
{
    public sealed class AuthenticationMenuModelTests
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
                AuthenticationMenuModel
                    .ShouldShowConfigurationSelector(configurationCount),
                Is.EqualTo(expected));
        }

        [Test]
        public void OnlyVerifiedSessionIsEligibleForLocalRemembering()
        {
            DateTimeOffset checkedAt = DateTimeOffset.UtcNow;
            var verified = new AuthenticationAssessmentSnapshot(
                AuthenticationValidationResult.Verified(),
                checkedAt);
            var inconclusive = new AuthenticationAssessmentSnapshot(
                AuthenticationValidationResult.Inconclusive(),
                checkedAt);

            Assert.That(
                AuthenticationMenuModel
                    .ShouldRememberVerifiedSession(
                        true,
                        true,
                        verified),
                Is.True);
            Assert.That(
                AuthenticationMenuModel
                    .ShouldRememberVerifiedSession(
                        false,
                        true,
                        verified),
                Is.False);
            Assert.That(
                AuthenticationMenuModel
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
                AuthenticationTargetRegistry.Targets.Count;
            var profiles = AuthenticationProjectProfiles.CreateForTests(
                null,
                null);

            using (var workspace =
                   new AuthenticationEditModeWorkspace(
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
                    AuthenticationTargetRegistry.Targets.Count,
                    Is.EqualTo(registryCountBefore));
            }
        }

        [Test]
        public async Task AutomaticAssessmentIsThrottledAcrossOpenAndFocus()
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            var controller = new AuthenticationAssessmentController(
                () => now);
            AuthenticationTarget target = CreateTarget("automatic");
            await target.Session.ReplaceAccessTokenAsync("opaque-test-token");
            var provider = new RecordingValidationProvider(
                AuthenticationValidationResult.Verified());

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
                Is.EqualTo(AuthenticationValidationStatus.Verified));

            now += AuthenticationAssessmentController
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
        public async Task ConfirmedRejectionClearsTheLiveSession()
        {
            var controller = new AuthenticationAssessmentController();
            AuthenticationTarget target = CreateTarget("rejected");
            await target.Session.ReplaceAccessTokenAsync("rejected-token");

            await controller.AssessAsync(
                target,
                new RecordingValidationProvider(
                    AuthenticationValidationResult.Rejected()),
                true,
                CancellationToken.None);

            Assert.That(target.Session.Status.HasAccessToken, Is.False);
            Assert.That(
                controller.TryGetSnapshot(target.Id, out var snapshot),
                Is.True);
            Assert.That(
                snapshot.Result.Status,
                Is.EqualTo(AuthenticationValidationStatus.Rejected));
        }

        [Test]
        public async Task InconclusiveValidationPreservesTheLiveSession()
        {
            var controller = new AuthenticationAssessmentController();
            AuthenticationTarget target = CreateTarget("offline");
            await target.Session.ReplaceAccessTokenAsync("preserved-token");

            await controller.AssessAsync(
                target,
                new RecordingValidationProvider(
                    AuthenticationValidationResult.Inconclusive()),
                true,
                CancellationToken.None);

            Assert.That(target.Session.AccessToken,
                Is.EqualTo("preserved-token"));
        }

        [Test]
        public async Task CancelledAssessmentLeavesNoStaleResultOrBusyState()
        {
            var controller = new AuthenticationAssessmentController();
            AuthenticationTarget target = CreateTarget("cancelled");
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

        [Test]
        public async Task CancellationAfterProviderReturnCannotPublishAResult()
        {
            var controller = new AuthenticationAssessmentController();
            AuthenticationTarget target = CreateTarget("late-cancel");
            await target.Session.ReplaceAccessTokenAsync("opaque-test-token");
            var cancellation = new CancellationTokenSource();
            var provider = new DeferredValidationProvider();

            Task assessmentTask = controller.AssessAsync(
                target,
                provider,
                true,
                cancellation.Token);
            Assert.That(controller.IsInProgress(target.Id), Is.True);

            cancellation.Cancel();
            provider.Complete(
                AuthenticationValidationResult.Verified());

            Assert.CatchAsync<OperationCanceledException>(async () =>
                await assessmentTask);
            Assert.That(controller.IsInProgress(target.Id), Is.False);
            Assert.That(
                controller.TryGetSnapshot(target.Id, out _),
                Is.False);
        }

        private static AuthenticationTarget CreateTarget(string id)
        {
            return new AuthenticationTarget(
                id,
                id,
                AuthenticationSession.CreateTransient(),
                null,
                null,
                null);
        }

        private sealed class RecordingValidationProvider :
            IAuthenticationValidationProvider
        {
            private readonly AuthenticationValidationResult result;

            internal RecordingValidationProvider(
                AuthenticationValidationResult validationResult)
            {
                result = validationResult;
            }

            public int CallCount { get; private set; }

            public string DisplayName
            {
                get { return "Test validation"; }
            }

            public Task<AuthenticationValidationResult> ValidateAsync(
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
            IAuthenticationValidationProvider
        {
            public string DisplayName
            {
                get { return "Cancelling validation"; }
            }

            public Task<AuthenticationValidationResult> ValidateAsync(
                ISessionService sessionService,
                CancellationToken cancellationToken =
                    default(CancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(
                    AuthenticationValidationResult.Inconclusive());
            }
        }

        private sealed class DeferredValidationProvider :
            IAuthenticationValidationProvider
        {
            private readonly TaskCompletionSource<
                AuthenticationValidationResult> completion =
                    new TaskCompletionSource<
                        AuthenticationValidationResult>();

            public string DisplayName
            {
                get { return "Deferred validation"; }
            }

            public Task<AuthenticationValidationResult> ValidateAsync(
                ISessionService sessionService,
                CancellationToken cancellationToken =
                    default(CancellationToken))
            {
                return completion.Task;
            }

            internal void Complete(
                AuthenticationValidationResult result)
            {
                completion.SetResult(result);
            }
        }
    }
}
