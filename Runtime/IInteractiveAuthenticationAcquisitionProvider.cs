using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.Session;

namespace Deucarian.Authentication
{
    /// <summary>
    /// Optional additive contract for acquisition providers that need values
    /// entered interactively by local development tooling.
    /// </summary>
    public interface IInteractiveAuthenticationAcquisitionProvider :
        IAuthenticationAcquisitionProvider
    {
        /// <summary>
        /// Gets the token-free field descriptors rendered by shared tooling.
        /// </summary>
        IReadOnlyList<AuthenticationInputDescriptor> InputDescriptors
        {
            get;
        }

        /// <summary>
        /// Reacquires authentication with short-lived interactive values and
        /// mutates the supplied authoritative session.
        /// </summary>
        Task<SessionResult> AcquireAsync(
            ISessionService sessionService,
            AuthenticationInputValues inputValues,
            CancellationToken cancellationToken = default(CancellationToken));
    }
}
