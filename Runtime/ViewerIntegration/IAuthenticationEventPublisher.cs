using System.Threading;
using System.Threading.Tasks;

namespace Deucarian.Authentication
{
    /// <summary>
    /// Optional token-free event sink for authentication command outcomes.
    /// </summary>
    public interface IAuthenticationEventPublisher
    {
        /// <summary>Publishes an event with sanitized status only.</summary>
        Task PublishAsync(
            string eventName,
            AuthenticationStatusSnapshot status,
            CancellationToken cancellationToken = default(CancellationToken));
    }
}
