using System.Threading;
using System.Threading.Tasks;
using Deucarian.Session;

namespace Deucarian.Authentication
{
    /// <summary>
    /// Optional server-side probe for the current token. The contract is
    /// backend-neutral and returns only a sanitized outcome.
    /// </summary>
    public interface IAuthenticationValidationProvider
    {
        /// <summary>Gets a token-free label for editor presentation.</summary>
        string DisplayName { get; }

        /// <summary>
        /// Validates the current session without clearing or replacing it on
        /// rejection, transport failure, or an inconclusive response.
        /// </summary>
        Task<AuthenticationValidationResult> ValidateAsync(
            ISessionService sessionService,
            CancellationToken cancellationToken = default(CancellationToken));
    }
}
