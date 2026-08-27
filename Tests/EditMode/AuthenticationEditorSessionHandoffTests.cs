using System.Threading.Tasks;
using Deucarian.Authentication.Editor;
using NUnit.Framework;

namespace Deucarian.Authentication.Tests
{
    public sealed class AuthenticationEditorSessionHandoffTests
    {
        [SetUp]
        public void SetUp()
        {
            AuthenticationEditorSessionHandoff.ClearAllForTests();
        }

        [TearDown]
        public void TearDown()
        {
            AuthenticationEditorSessionHandoff.ClearAllForTests();
        }

        [Test]
        public async Task MatchingBindingRestoresAuthenticatedSession()
        {
            AuthenticationSession source =
                AuthenticationSession.CreateTransient();
            await source.ReplaceAccessTokenAsync("editor-session-token");

            AuthenticationEditorSessionHandoff.Capture(
                "profile-a|development",
                source);

            Assert.That(
                AuthenticationEditorSessionHandoff.TryCreateSession(
                    "profile-a|development",
                    out AuthenticationSession restored),
                Is.True);
            Assert.That(restored.AccessToken, Is.EqualTo("editor-session-token"));
            Assert.That(restored.Status.HasAccessToken, Is.True);
        }

        [Test]
        public async Task DifferentBindingCannotRestoreToken()
        {
            AuthenticationSession source =
                AuthenticationSession.CreateTransient();
            AuthenticationSession other =
                AuthenticationSession.CreateTransient();
            await source.ReplaceAccessTokenAsync("editor-session-token");
            AuthenticationEditorSessionHandoff.Capture(
                "profile-a|development",
                source);

            Assert.That(
                AuthenticationEditorSessionHandoff.TryApply(
                    "profile-a|testing",
                    other),
                Is.False);
            Assert.That(other.Status.HasAccessToken, Is.False);
        }

        [Test]
        public async Task ClearedSessionClearsMatchingHandoff()
        {
            AuthenticationSession source =
                AuthenticationSession.CreateTransient();
            await source.ReplaceAccessTokenAsync("editor-session-token");
            AuthenticationEditorSessionHandoff.Capture(
                "profile-a|development",
                source);

            await source.ClearAsync();
            AuthenticationEditorSessionHandoff.Capture(
                "profile-a|development",
                source);

            Assert.That(
                AuthenticationEditorSessionHandoff.TryCreateSession(
                    "profile-a|development",
                    out AuthenticationSession restored),
                Is.False);
            Assert.That(restored.Status.HasAccessToken, Is.False);
        }
    }
}
