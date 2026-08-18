using System.Threading;
using System.Threading.Tasks;

namespace Deucarian.ViewerAuthentication
{
    /// <summary>
    /// Optional token-free event sink for authentication command outcomes.
    /// </summary>
    public interface IViewerAuthenticationEventPublisher
    {
        /// <summary>Publishes an event with sanitized status only.</summary>
        Task PublishAsync(
            string eventName,
            ViewerAuthenticationStatusSnapshot status,
            CancellationToken cancellationToken = default(CancellationToken));
    }
}
