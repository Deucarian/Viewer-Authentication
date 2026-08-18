namespace Deucarian.ViewerAuthentication
{
    /// <summary>Stable token-free authentication outcome event names.</summary>
    public static class ViewerAuthenticationEventNames
    {
        public const string AccessTokenUpdated = "access_token_updated";
        public const string AccessTokenRefreshed = "access_token_refreshed";
        public const string AccessTokenCleared = "access_token_cleared";
    }
}
