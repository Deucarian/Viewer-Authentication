using System;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.Session;

namespace Deucarian.Authentication
{
    /// <summary>Prevents a replaced acquisition workflow from starting new session mutations.</summary>
    internal sealed class AcquisitionSessionScope : ISessionService, IDisposable
    {
        private readonly ISessionService session;
        private readonly CancellationToken scope;
        private EventHandler<SessionChangedEventArgs> subscriptions;
        private bool disposed;
        public AcquisitionSessionScope(ISessionService session, CancellationToken scope)
        { this.session = session; this.scope = scope; }
        public event EventHandler<SessionChangedEventArgs> SessionChanged
        {
            add { CheckActive(); session.SessionChanged += value; subscriptions += value; }
            remove { session.SessionChanged -= value; subscriptions -= value; }
        }
        public SessionData CurrentSession => session.CurrentSession;
        public SessionState State => session.State;
        public bool IsAuthenticated => session.IsAuthenticated;
        public bool IsAccessTokenExpired => session.IsAccessTokenExpired;
        public bool IsAccessTokenExpiringSoon => session.IsAccessTokenExpiringSoon;
        public TimeSpan ExpiryLeeway
        { get => session.ExpiryLeeway; set { CheckActive(); session.ExpiryLeeway = value; } }
        public SessionRefreshFailurePolicy RefreshFailurePolicy
        { get => session.RefreshFailurePolicy; set { CheckActive(); session.RefreshFailurePolicy = value; } }
        public Task<SessionResult> RestoreAsync(CancellationToken cancellationToken = default) => Run(session.RestoreAsync, cancellationToken);
        public Task<SessionResult> RefreshAsync(CancellationToken cancellationToken = default) => Run(session.RefreshAsync, cancellationToken);
        public Task<SessionResult> LogoutAsync(CancellationToken cancellationToken = default) => Run(session.LogoutAsync, cancellationToken);
        public Task<SessionResult> ReplaceAccessTokenAsync(string accessToken, DateTimeOffset? expiresAtUtc = null,
            CancellationToken cancellationToken = default) => Run(token => session.ReplaceAccessTokenAsync(accessToken, expiresAtUtc, token), cancellationToken);
        public Task<SessionResult> LoginAsync<T>(T request, ISessionLoginService<T> loginService,
            CancellationToken cancellationToken = default) => Run(token => session.LoginAsync(request, loginService, token), cancellationToken);
        private async Task<SessionResult> Run(Func<CancellationToken, Task<SessionResult>> operation, CancellationToken token)
        {
            CheckActive();
            using (var cancellation = CancellationTokenSource.CreateLinkedTokenSource(scope, token))
                return await operation(cancellation.Token);
        }
        private void CheckActive()
        {
            scope.ThrowIfCancellationRequested();
            if (disposed) throw new ObjectDisposedException(nameof(AcquisitionSessionScope));
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (subscriptions != null)
                foreach (EventHandler<SessionChangedEventArgs> handler in subscriptions.GetInvocationList()) session.SessionChanged -= handler;
            subscriptions = null;
        }
    }
}
