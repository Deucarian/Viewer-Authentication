using System.Collections.Generic;
using Deucarian.CommandRouting;

namespace Deucarian.Authentication
{
    /// <summary>Factory for explicitly composed authentication handlers.</summary>
    public static class AuthenticationCommandHandlers
    {
        /// <summary>Creates the generic authentication handler set.</summary>
        public static IReadOnlyList<ICommandHandler<THost>> Create<THost>(
            IAuthenticationEventPublisher eventPublisher = null)
            where THost : class, IAuthenticationHost
        {
            return new ICommandHandler<THost>[]
            {
                new AuthenticationCommandHandler<THost>(eventPublisher)
            };
        }
    }
}
