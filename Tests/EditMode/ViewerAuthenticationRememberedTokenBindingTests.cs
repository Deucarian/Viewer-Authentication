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
    }
}
