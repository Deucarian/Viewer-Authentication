using Deucarian.ViewerAuthentication.Editor;
using NUnit.Framework;

namespace Deucarian.ViewerAuthentication.Tests
{
    public sealed class ViewerAuthenticationRememberedTokenBindingTests
    {
        [Test]
        public void ExplicitOwnerDoesNotFollowAViewerSelectionChange()
        {
            string owner = ViewerAuthenticationRememberedTokenBinding
                .ResolveOwner(
                    "viewer-a",
                    "viewer-b",
                    hasRememberedToken: true);

            Assert.That(owner, Is.EqualTo("viewer-a"));
            Assert.That(
                ViewerAuthenticationRememberedTokenBinding.Matches(
                    owner,
                    "viewer-b"),
                Is.False);
        }

        [Test]
        public void LegacyOwnerMigratesFromThePreviouslySelectedViewer()
        {
            string owner = ViewerAuthenticationRememberedTokenBinding
                .ResolveOwner(
                    string.Empty,
                    "viewer-a",
                    hasRememberedToken: true);

            Assert.That(owner, Is.EqualTo("viewer-a"));
            Assert.That(
                ViewerAuthenticationRememberedTokenBinding.Matches(
                    owner,
                    "viewer-a"),
                Is.True);
        }

        [Test]
        public void NoRememberedTokenHasNoOwner()
        {
            Assert.That(
                ViewerAuthenticationRememberedTokenBinding.ResolveOwner(
                    "viewer-a",
                    "viewer-b",
                    hasRememberedToken: false),
                Is.Empty);
        }

        [Test]
        public void OwnerRebindChangesOnlyTheTokenFreeOwnerIdentity()
        {
            bool rebound = ViewerAuthenticationRememberedTokenBinding
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
            bool rebound = ViewerAuthenticationRememberedTokenBinding
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
