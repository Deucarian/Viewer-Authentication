using System.Collections.Generic;
using Deucarian.CommandRouting;

namespace Deucarian.ViewerAuthentication
{
    /// <summary>Factory for explicitly composed authentication handlers.</summary>
    public static class ViewerAuthenticationCommandHandlers
    {
        /// <summary>Creates the generic viewer authentication handler set.</summary>
        public static IReadOnlyList<ICommandHandler<THost>> Create<THost>(
            IViewerAuthenticationEventPublisher eventPublisher = null)
            where THost : class, IViewerAuthenticationHost
        {
            return new ICommandHandler<THost>[]
            {
                new ViewerAuthenticationCommandHandler<THost>(eventPublisher)
            };
        }
    }
}
