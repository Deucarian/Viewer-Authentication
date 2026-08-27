namespace Deucarian.Authentication
{
    /// <summary>
    /// Application context consumed by generic authentication commands.
    /// </summary>
    public interface IAuthenticationHost
    {
        /// <summary>Gets the authentication session to mutate.</summary>
        IAuthenticationSession AuthenticationSession { get; }
    }
}
