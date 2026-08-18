namespace Deucarian.ViewerAuthentication
{
    /// <summary>Sanitized outcome of a server-side token validation probe.</summary>
    public enum ViewerAuthenticationValidationStatus
    {
        /// <summary>The probe could not establish acceptance or rejection.</summary>
        Inconclusive = 0,

        /// <summary>The server accepted the current token.</summary>
        Verified = 1,

        /// <summary>The server explicitly rejected the current token.</summary>
        Rejected = 2
    }
}
