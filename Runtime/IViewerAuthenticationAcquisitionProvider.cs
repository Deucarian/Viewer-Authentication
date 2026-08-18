using System.Threading;
using System.Threading.Tasks;
using Deucarian.Session;

namespace Deucarian.ViewerAuthentication
{
    /// <summary>
    /// Injected application-specific development token acquisition workflow.
    /// Existing non-interactive providers implement only this contract. A
    /// provider that needs transient user input can additionally implement
    /// <see cref="IInteractiveViewerAuthenticationAcquisitionProvider"/>.
    /// </summary>
    public interface IViewerAuthenticationAcquisitionProvider
    {
        /// <summary>Gets the action label shown by editor tooling.</summary>
        string DisplayName { get; }

        /// <summary>
        /// Acquires authentication and mutates the supplied authoritative
        /// session through its public lifecycle API.
        /// </summary>
        Task<SessionResult> AcquireAsync(
            ISessionService sessionService,
            CancellationToken cancellationToken = default(CancellationToken));
    }
}
