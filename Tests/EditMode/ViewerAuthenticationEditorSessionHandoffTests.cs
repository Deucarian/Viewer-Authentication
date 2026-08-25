using System.Threading.Tasks;
using Deucarian.ViewerAuthentication.Editor;
using NUnit.Framework;

namespace Deucarian.ViewerAuthentication.Tests
{
    public sealed class ViewerAuthenticationEditorSessionHandoffTests
    {
        [SetUp]
        public void SetUp()
        {
            ViewerAuthenticationEditorSessionHandoff.ClearAllForTests();
        }

        [TearDown]
        public void TearDown()
        {
            ViewerAuthenticationEditorSessionHandoff.ClearAllForTests();
        }

        [Test]
        public async Task MatchingBindingRestoresAuthenticatedSession()
        {
            ViewerAuthenticationSession source =
                ViewerAuthenticationSession.CreateTransient();
            await source.ReplaceAccessTokenAsync("editor-session-token");

            ViewerAuthenticationEditorSessionHandoff.Capture(
                "profile-a|development",
                source);

            Assert.That(
                ViewerAuthenticationEditorSessionHandoff.TryCreateSession(
                    "profile-a|development",
                    out ViewerAuthenticationSession restored),
                Is.True);
            Assert.That(restored.AccessToken, Is.EqualTo("editor-session-token"));
            Assert.That(restored.Status.HasAccessToken, Is.True);
        }

        [Test]
        public async Task DifferentBindingCannotRestoreToken()
        {
            ViewerAuthenticationSession source =
                ViewerAuthenticationSession.CreateTransient();
            ViewerAuthenticationSession other =
                ViewerAuthenticationSession.CreateTransient();
            await source.ReplaceAccessTokenAsync("editor-session-token");
            ViewerAuthenticationEditorSessionHandoff.Capture(
                "profile-a|development",
                source);

            Assert.That(
                ViewerAuthenticationEditorSessionHandoff.TryApply(
                    "profile-a|testing",
                    other),
                Is.False);
            Assert.That(other.Status.HasAccessToken, Is.False);
        }

        [Test]
        public async Task ClearedSessionClearsMatchingHandoff()
        {
            ViewerAuthenticationSession source =
                ViewerAuthenticationSession.CreateTransient();
            await source.ReplaceAccessTokenAsync("editor-session-token");
            ViewerAuthenticationEditorSessionHandoff.Capture(
                "profile-a|development",
                source);

            await source.ClearAsync();
            ViewerAuthenticationEditorSessionHandoff.Capture(
                "profile-a|development",
                source);

            Assert.That(
                ViewerAuthenticationEditorSessionHandoff.TryCreateSession(
                    "profile-a|development",
                    out ViewerAuthenticationSession restored),
                Is.False);
            Assert.That(restored.Status.HasAccessToken, Is.False);
        }
    }
}
