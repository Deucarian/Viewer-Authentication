using System;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.Session;
using UnityEngine;

namespace Deucarian.Authentication
{
    /// <summary>Convenience access to an application's explicitly composed authentication and acquisition workflow.</summary>
    [DisallowMultipleComponent]
    public sealed class AuthenticationHost : MonoBehaviour
    {
        private IAuthenticationSession session;
        private IAuthenticationAcquisitionProvider acquisition;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private CancellationTokenSource signIn;
        private bool destroyed;

        public void Configure(IAuthenticationSession authentication, IAuthenticationAcquisitionProvider provider)
        {
            if (destroyed) throw new ObjectDisposedException(nameof(AuthenticationHost));
            if (session != null) throw new InvalidOperationException("This authentication host is already configured.");
            if (authentication == null) throw new ArgumentNullException(nameof(authentication));
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            session = authentication;
            acquisition = provider;
        }
        public AuthenticationStatusSnapshot Status => Session.Status;
        private IAuthenticationSession Session => !destroyed ? session ??
            throw new InvalidOperationException("AuthenticationHost '" + name + "' is not configured. Supply its session service and credential acquisition provider once during startup before signing in or out.") : throw new ObjectDisposedException(nameof(AuthenticationHost));

        public async Task<SessionResult> SignInAsync(CancellationToken cancellationToken = default)
        {
            var authoritative = Session.SessionService;
            signIn?.Cancel();
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, cancellationToken);
            signIn = cancellation;
            try
            {
                using (var scope = new AcquisitionSessionScope(authoritative, cancellation.Token))
                    return await acquisition.AcquireAsync(scope, cancellation.Token);
            }
            finally
            {
                if (ReferenceEquals(signIn, cancellation)) signIn = null;
                cancellation.Dispose();
            }
        }
        public async Task<SessionResult> SignOutAsync(CancellationToken cancellationToken = default)
        {
            var authentication = Session;
            signIn?.Cancel();
            using (var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, cancellationToken))
                return await authentication.ClearAsync(cancellation.Token);
        }
        private void OnDestroy() { destroyed = true; lifetime.Cancel(); lifetime.Dispose(); session = null; acquisition = null; }
    }
}
