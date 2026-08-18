using System;
using Deucarian.Session;

namespace Deucarian.ViewerAuthentication
{
    /// <summary>
    /// Normalizes token input accepted from editor fields, browser commands,
    /// and other viewer integration boundaries.
    /// </summary>
    public static class ViewerAccessTokenInput
    {
        private const string BearerScheme = "Bearer";

        /// <summary>Stable failure code for invalid token input.</summary>
        public const string InvalidCode = "invalid_access_token";

        /// <summary>Token-free failure message for invalid token input.</summary>
        public const string InvalidMessage =
            "A valid access token is required.";

        /// <summary>
        /// Removes an optional Bearer prefix and validates the raw token.
        /// </summary>
        public static bool TryNormalize(
            string value,
            out string accessToken)
        {
            accessToken = value == null ? null : value.Trim();
            if (string.Equals(
                    accessToken,
                    BearerScheme,
                    StringComparison.OrdinalIgnoreCase))
            {
                accessToken = null;
            }
            else if (!string.IsNullOrEmpty(accessToken) &&
                     accessToken.Length > BearerScheme.Length &&
                     accessToken.StartsWith(
                         BearerScheme,
                         StringComparison.OrdinalIgnoreCase) &&
                     char.IsWhiteSpace(accessToken[BearerScheme.Length]))
            {
                accessToken =
                    accessToken.Substring(BearerScheme.Length).Trim();
            }

            if (SessionData.IsValidAccessToken(accessToken))
            {
                return true;
            }

            accessToken = null;
            return false;
        }
    }
}
