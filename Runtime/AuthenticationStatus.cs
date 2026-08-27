namespace Deucarian.Authentication
{
    /// <summary>Sanitized lifecycle state of authentication.</summary>
    public enum AuthenticationStatus
    {
        Missing = 0,
        Active = 1,
        Expiring = 2,
        Expired = 3,
        ExpiryUnknown = 4
    }
}
