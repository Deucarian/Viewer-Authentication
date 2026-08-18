namespace Deucarian.ViewerAuthentication
{
    /// <summary>
    /// Application context consumed by generic authentication commands.
    /// </summary>
    public interface IViewerAuthenticationHost
    {
        /// <summary>Gets the viewer authentication session to mutate.</summary>
        IViewerAuthenticationSession AuthenticationSession { get; }
    }
}
