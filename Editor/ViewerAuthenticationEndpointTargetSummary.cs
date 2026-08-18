using System;

namespace Deucarian.ViewerAuthentication.Editor
{
    internal sealed class ViewerAuthenticationEndpointTargetSummary
    {
        private ViewerAuthenticationEndpointTargetSummary(
            ViewerAuthenticationEndpointLocation signIn,
            ViewerAuthenticationEndpointLocation tokenCheck)
        {
            SignIn = signIn;
            TokenCheck = tokenCheck;
        }

        internal ViewerAuthenticationEndpointLocation SignIn { get; }

        internal ViewerAuthenticationEndpointLocation TokenCheck { get; }

        internal bool HasAnyEndpoint
        {
            get { return SignIn != null || TokenCheck != null; }
        }

        internal bool HasDifferentOrigins
        {
            get
            {
                return SignIn != null &&
                       TokenCheck != null &&
                       SignIn.HasOrigin &&
                       TokenCheck.HasOrigin &&
                       !string.Equals(
                           SignIn.Origin,
                           TokenCheck.Origin,
                           StringComparison.OrdinalIgnoreCase);
            }
        }

        internal string SharedOrigin
        {
            get
            {
                if (HasDifferentOrigins ||
                    (SignIn != null && !SignIn.HasOrigin) ||
                    (TokenCheck != null && !TokenCheck.HasOrigin))
                {
                    return string.Empty;
                }

                if (SignIn != null && SignIn.HasOrigin)
                {
                    return SignIn.Origin;
                }

                return TokenCheck != null && TokenCheck.HasOrigin
                    ? TokenCheck.Origin
                    : string.Empty;
            }
        }

        internal static ViewerAuthenticationEndpointTargetSummary Create(
            string signInMethod,
            string signInEndpoint,
            string tokenCheckMethod,
            string tokenCheckEndpoint)
        {
            return new ViewerAuthenticationEndpointTargetSummary(
                ViewerAuthenticationEndpointLocation.Create(
                    signInMethod,
                    signInEndpoint),
                ViewerAuthenticationEndpointLocation.Create(
                    tokenCheckMethod,
                    tokenCheckEndpoint));
        }
    }

    internal sealed class ViewerAuthenticationEndpointLocation
    {
        private ViewerAuthenticationEndpointLocation(
            string origin,
            string displayValue)
        {
            Origin = origin ?? string.Empty;
            DisplayValue = displayValue ?? string.Empty;
        }

        internal string Origin { get; }

        internal string DisplayValue { get; }

        internal bool HasOrigin
        {
            get { return !string.IsNullOrWhiteSpace(Origin); }
        }

        internal static ViewerAuthenticationEndpointLocation Create(
            string method,
            string endpointTemplate)
        {
            if (string.IsNullOrWhiteSpace(endpointTemplate))
            {
                return null;
            }

            string endpoint = endpointTemplate
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Trim();
            string normalizedMethod = string.IsNullOrWhiteSpace(method)
                ? string.Empty
                : method.Trim();
            string origin = string.Empty;
            string safeEndpoint = SanitizeRelativeEndpoint(endpoint);
            if (Uri.TryCreate(endpoint, UriKind.Absolute, out Uri uri) &&
                (string.Equals(
                     uri.Scheme,
                     Uri.UriSchemeHttp,
                     StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(
                     uri.Scheme,
                     Uri.UriSchemeHttps,
                     StringComparison.OrdinalIgnoreCase)) &&
                !string.IsNullOrWhiteSpace(uri.Host))
            {
                origin = uri.GetComponents(
                    UriComponents.SchemeAndServer,
                    UriFormat.UriEscaped);
                safeEndpoint = CreateSafeAbsoluteEndpoint(uri, origin);
            }

            string displayValue = string.IsNullOrWhiteSpace(normalizedMethod)
                ? safeEndpoint
                : normalizedMethod + "  " + safeEndpoint;
            return new ViewerAuthenticationEndpointLocation(
                origin,
                displayValue);
        }

        private static string CreateSafeAbsoluteEndpoint(
            Uri uri,
            string origin)
        {
            string path = string.IsNullOrWhiteSpace(uri.AbsolutePath)
                ? "/"
                : uri.AbsolutePath;
            return origin +
                   path +
                   CreateHiddenSuffix(uri.Query, "configured values") +
                   CreateHiddenSuffix(uri.Fragment, "fragment");
        }

        private static string SanitizeRelativeEndpoint(string endpoint)
        {
            string sanitized = RemoveAuthorityUserInfo(endpoint);
            int queryIndex = sanitized.IndexOf('?');
            int fragmentIndex = sanitized.IndexOf('#');
            int endIndex = sanitized.Length;
            if (queryIndex >= 0)
            {
                endIndex = Math.Min(endIndex, queryIndex);
            }

            if (fragmentIndex >= 0)
            {
                endIndex = Math.Min(endIndex, fragmentIndex);
            }

            string result = sanitized.Substring(0, endIndex);
            if (queryIndex >= 0)
            {
                result += "?[configured values hidden]";
            }

            if (fragmentIndex >= 0)
            {
                result += "#[fragment hidden]";
            }

            return result;
        }

        private static string RemoveAuthorityUserInfo(string endpoint)
        {
            int schemeSeparator = endpoint.IndexOf(
                "://",
                StringComparison.Ordinal);
            if (schemeSeparator < 0)
            {
                return endpoint;
            }

            int authorityStart = schemeSeparator + 3;
            int authorityEnd = endpoint.IndexOf('/', authorityStart);
            if (authorityEnd < 0)
            {
                authorityEnd = endpoint.Length;
            }

            int userInfoEnd = endpoint.IndexOf('@', authorityStart);
            if (userInfoEnd < 0 || userInfoEnd >= authorityEnd)
            {
                return endpoint;
            }

            return endpoint.Remove(
                authorityStart,
                userInfoEnd - authorityStart + 1);
        }

        private static string CreateHiddenSuffix(
            string configuredValue,
            string description)
        {
            if (string.IsNullOrEmpty(configuredValue))
            {
                return string.Empty;
            }

            return configuredValue[0] + "[" + description + " hidden]";
        }
    }
}
