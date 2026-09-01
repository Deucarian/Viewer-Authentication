using Deucarian.Authentication.Editor;
using NUnit.Framework;

namespace Deucarian.Authentication.Tests
{
    public sealed class AuthenticationRememberedTokenBindingTests
    {
        [Test]
        public void ExplicitOwnerDoesNotFollowAViewerSelectionChange()
        {
            string owner = AuthenticationRememberedTokenBinding
                .ResolveOwner(
                    "viewer-a",
                    "viewer-b",
                    hasRememberedToken: true);

            Assert.That(owner, Is.EqualTo("viewer-a"));
            Assert.That(
                AuthenticationRememberedTokenBinding.Matches(
                    owner,
                    "viewer-b"),
                Is.False);
        }

        [Test]
        public void LegacyOwnerMigratesFromThePreviouslySelectedViewer()
        {
            string owner = AuthenticationRememberedTokenBinding
                .ResolveOwner(
                    string.Empty,
                    "viewer-a",
                    hasRememberedToken: true);

            Assert.That(owner, Is.EqualTo("viewer-a"));
            Assert.That(
                AuthenticationRememberedTokenBinding.Matches(
                    owner,
                    "viewer-a"),
                Is.True);
        }

        [Test]
        public void NoRememberedTokenHasNoOwner()
        {
            Assert.That(
                AuthenticationRememberedTokenBinding.ResolveOwner(
                    "viewer-a",
                    "viewer-b",
                    hasRememberedToken: false),
                Is.Empty);
        }

        [Test]
        public void OwnerRebindChangesOnlyTheTokenFreeOwnerIdentity()
        {
            bool rebound = AuthenticationRememberedTokenBinding
                .TryRebindOwner(
                    " report-viewer ",
                    "report-viewer",
                    " simultria-viewer ",
                    hasRememberedToken: true,
                    out string owner);

            Assert.That(rebound, Is.True);
            Assert.That(owner, Is.EqualTo("simultria-viewer"));
        }

        [TestCase(null, "report-viewer", "simultria-viewer", true)]
        [TestCase("report-viewer", null, "simultria-viewer", true)]
        [TestCase("report-viewer", "report-viewer", null, true)]
        [TestCase("report-viewer", "activity-viewer", "simultria-viewer", true)]
        [TestCase("report-viewer", "report-viewer", "simultria-viewer", false)]
        public void OwnerRebindRejectsMissingOwnershipContext(
            string currentOwner,
            string expectedCurrentOwner,
            string targetOwner,
            bool hasRememberedToken)
        {
            bool rebound = AuthenticationRememberedTokenBinding
                .TryRebindOwner(
                    currentOwner,
                    expectedCurrentOwner,
                    targetOwner,
                    hasRememberedToken,
                    out _);

            Assert.That(rebound, Is.False);
        }

        [Test]
        public void ExactIdentityMatchesARecreatedTargetAfterDomainReload()
        {
            AuthenticationPersistenceIdentity persistedIdentity =
                CreateIdentity(CompositionFingerprintA);
            AuthenticationTarget recreatedTarget = CreateTarget(
                "simultria-viewer",
                CreateIdentity(CompositionFingerprintA));

            Assert.That(
                AuthenticationRememberedTokenBinding.Matches(
                    "simultria-viewer",
                    persistedIdentity,
                    recreatedTarget),
                Is.True);
        }

        [TestCase(CompositionFingerprintCatalogChanged)]
        [TestCase(CompositionFingerprintSecondaryClientChanged)]
        [TestCase(CompositionFingerprintPolicyChanged)]
        public void SameTargetCannotRestoreAfterFullCompositionChanges(
            string currentCompositionFingerprint)
        {
            AuthenticationPersistenceIdentity persistedIdentity =
                CreateIdentity(CompositionFingerprintA);
            AuthenticationTarget currentTarget = CreateTarget(
                "simultria-viewer",
                CreateIdentity(currentCompositionFingerprint));

            Assert.That(
                AuthenticationRememberedTokenBinding.Matches(
                    "simultria-viewer",
                    persistedIdentity,
                    currentTarget),
                Is.False);
        }

        [Test]
        public void ExactIdentityStillRequiresTheRememberedTargetOwner()
        {
            AuthenticationPersistenceIdentity identity =
                CreateIdentity(CompositionFingerprintA);

            Assert.That(
                AuthenticationRememberedTokenBinding.Matches(
                    "another-viewer",
                    identity,
                    CreateTarget("simultria-viewer", identity)),
                Is.False);
        }

        [Test]
        public void OwnerRebindCannotCrossACompositionIdentityChange()
        {
            Assert.That(
                AuthenticationRememberedTokenBinding.IdentityMatches(
                    CreateIdentity(CompositionFingerprintA),
                    CreateIdentity(CompositionFingerprintCatalogChanged)),
                Is.False);
        }

        private const string CompositionFingerprintA =
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string CompositionFingerprintCatalogChanged =
            "baaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string CompositionFingerprintSecondaryClientChanged =
            "caaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string CompositionFingerprintPolicyChanged =
            "daaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        private static AuthenticationPersistenceIdentity CreateIdentity(
            string configurationFingerprint)
        {
            return new AuthenticationPersistenceIdentity(
                "simultria.api-v2",
                "simultria.development",
                "https://api.example.invalid",
                "primary",
                null,
                configurationFingerprint);
        }

        private static AuthenticationTarget CreateTarget(
            string targetId,
            AuthenticationPersistenceIdentity identity)
        {
            return new AuthenticationTarget(
                targetId,
                "Test Viewer",
                AuthenticationSession.CreateTransient(),
                null,
                null,
                identity);
        }
    }
}
