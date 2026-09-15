using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Deucarian.Session;

namespace Deucarian.Authentication.Tests
{
    public sealed class AuthenticationHostTests
    {
        [Test]
        public async Task SignOutPreventsALateAcquisitionFromRestoringAuthentication()
        {
            var go = new GameObject("authentication");
            try
            {
                var session = AuthenticationSession.CreateTransient();
                var provider = new DelayedProvider();
                var host = go.AddComponent<AuthenticationHost>();
                host.Configure(session, provider);
                var pending = host.SignInAsync();
                await host.SignOutAsync();
                provider.Resume.SetResult(true);
                try { await pending; Assert.Fail("The old acquisition must be cancelled."); }
                catch (System.OperationCanceledException) { }
                Assert.That(session.SessionService.IsAuthenticated, Is.False);
            }
            finally { Object.DestroyImmediate(go); }
        }
        private sealed class DelayedProvider : IAuthenticationAcquisitionProvider
        {
            public string DisplayName => "Test";
            public readonly TaskCompletionSource<bool> Resume = new TaskCompletionSource<bool>();
            public async Task<SessionResult> AcquireAsync(ISessionService sessionService, CancellationToken cancellationToken = default)
            {
                await Resume.Task;
                // Deliberately ignores the supplied token to exercise stale mutation protection.
                return await sessionService.ReplaceAccessTokenAsync("test-token");
            }
        }
    }
}
