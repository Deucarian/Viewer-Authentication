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
    }
}
