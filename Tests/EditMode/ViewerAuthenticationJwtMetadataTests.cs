using System;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Deucarian.Session.APIIntegration;
using NUnit.Framework;

namespace Deucarian.ViewerAuthentication.Tests
{
    public sealed class ViewerAuthenticationExpiryIntegrationTests
    {
        [Test]
        public void FractionalNumericDateIsReadWithoutSignatureValidation()
        {
            const double unixSeconds = 1800000000.625d;
            string token = CreateJwt(unixSeconds);

            bool parsed = SessionAccessTokenExpiryResolver.TryResolveJwtExpiry(
                token,
                out DateTimeOffset expiry);

            Assert.That(parsed, Is.True);
            Assert.That(
                expiry,
                Is.EqualTo(
                    DateTimeOffset.FromUnixTimeMilliseconds(1800000000625)));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("opaque-token")]
        [TestCase("header.invalid-base64.signature")]
        [TestCase("header.e30.signature")]
        [TestCase("header.eyJleHAiOiJub3QtbnVtZXJpYyJ9.signature")]
        public void OpaqueOrMalformedTokenHasUnknownExpiry(string token)
        {
            Assert.That(
                SessionAccessTokenExpiryResolver.TryResolveJwtExpiry(
                    token,
                    out _),
                Is.False);
        }

        [Test]
        public async Task ManualReplacementAppliesReadableJwtExpiry()
        {
            const double unixSeconds = 1900000000.25d;
            ViewerAuthenticationSession session =
                ViewerAuthenticationSession.CreateTransient();

            await session.ReplaceAccessTokenAsync(CreateJwt(unixSeconds));

            Assert.That(
                session.Status.ExpiresAtUtc,
                Is.EqualTo(
                    DateTimeOffset.FromUnixTimeMilliseconds(1900000000250)));
        }

        internal static string CreateJwt(double unixSeconds)
        {
            string header = Base64Url("{\"alg\":\"none\"}");
            string payload = Base64Url(
                "{\"exp\":" +
                unixSeconds.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture) +
                "}");
            return header + "." + payload + ".signature";
        }

        private static string Base64Url(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }
    }
}
