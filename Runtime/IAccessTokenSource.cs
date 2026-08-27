namespace Deucarian.Authentication
{
    /// <summary>
    /// Exposes the current access token to code that cannot consume the
    /// full authentication session.
    /// </summary>
    public interface IAccessTokenSource
    {
        /// <summary>
        /// Gets the current normalized access token, or null when no session is
        /// active. Callers must never log or display this value.
        /// </summary>
        string AccessToken { get; }
    }
}
