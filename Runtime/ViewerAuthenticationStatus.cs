namespace Deucarian.ViewerAuthentication
{
    /// <summary>Sanitized lifecycle state of viewer authentication.</summary>
    public enum ViewerAuthenticationStatus
    {
        Missing = 0,
        Active = 1,
        Expiring = 2,
        Expired = 3,
        ExpiryUnknown = 4
    }
}
