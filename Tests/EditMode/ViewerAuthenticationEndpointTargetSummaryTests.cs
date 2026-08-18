using Deucarian.ViewerAuthentication.Editor;
using NUnit.Framework;

namespace Deucarian.ViewerAuthentication.Tests
{
    public sealed class ViewerAuthenticationEndpointTargetSummaryTests
    {
        [Test]
        public void SameOriginIsShownOnceWithExactEndpointValues()
        {
            ViewerAuthenticationEndpointTargetSummary summary =
                ViewerAuthenticationEndpointTargetSummary.Create(
                    "POST",
                    "https://api.example.com:8443/api/v2/login",
                    "GET",
                    "https://api.example.com:8443/api/v2/auth/validate");

            Assert.That(summary.HasAnyEndpoint, Is.True);
            Assert.That(summary.HasDifferentOrigins, Is.False);
            Assert.That(
                summary.SharedOrigin,
                Is.EqualTo("https://api.example.com:8443"));
            Assert.That(
                summary.SignIn.DisplayValue,
                Is.EqualTo(
                    "POST  https://api.example.com:8443/api/v2/login"));
            Assert.That(
                summary.TokenCheck.DisplayValue,
                Is.EqualTo(
                    "GET  https://api.example.com:8443/api/v2/auth/validate"));
        }

        [Test]
        public void DifferentOriginsRemainExplicitAndRaiseMismatch()
        {
            ViewerAuthenticationEndpointTargetSummary summary =
                ViewerAuthenticationEndpointTargetSummary.Create(
                    "POST",
                    "https://login.example.com/token",
                    "GET",
                    "https://validate.example.com/token");

            Assert.That(summary.HasDifferentOrigins, Is.True);
            Assert.That(summary.SharedOrigin, Is.Empty);
            Assert.That(
                summary.SignIn.Origin,
                Is.EqualTo("https://login.example.com"));
            Assert.That(
                summary.TokenCheck.Origin,
                Is.EqualTo("https://validate.example.com"));
        }

        [Test]
        public void EquivalentDefaultPortAndHostCaseShareOneOrigin()
        {
            ViewerAuthenticationEndpointTargetSummary summary =
                ViewerAuthenticationEndpointTargetSummary.Create(
                    "POST",
                    "https://API.Example.com:443/login",
                    "GET",
                    "https://api.example.com/validate");

            Assert.That(summary.HasDifferentOrigins, Is.False);
            Assert.That(
                summary.SharedOrigin,
                Is.EqualTo("https://api.example.com"));
        }

        [Test]
        public void NonAbsoluteTemplateStillShowsTheExactConfiguredValue()
        {
            ViewerAuthenticationEndpointTargetSummary summary =
                ViewerAuthenticationEndpointTargetSummary.Create(
                    "POST",
                    "{backend}/api/v2/login",
                    null,
                    null);

            Assert.That(summary.HasAnyEndpoint, Is.True);
            Assert.That(summary.SharedOrigin, Is.Empty);
            Assert.That(summary.SignIn.HasOrigin, Is.False);
            Assert.That(
                summary.SignIn.DisplayValue,
                Is.EqualTo("POST  {backend}/api/v2/login"));
            Assert.That(summary.TokenCheck, Is.Null);
        }

        [Test]
        public void MixedResolvedAndRelativeTemplatesDoNotImplySharedHost()
        {
            ViewerAuthenticationEndpointTargetSummary summary =
                ViewerAuthenticationEndpointTargetSummary.Create(
                    "POST",
                    "https://api.example.com/login",
                    "GET",
                    "{backend}/validate");

            Assert.That(summary.HasDifferentOrigins, Is.False);
            Assert.That(summary.SharedOrigin, Is.Empty);
        }

        [Test]
        public void NonHttpAndMultilineTemplatesRemainSafeToDisplay()
        {
            ViewerAuthenticationEndpointTargetSummary summary =
                ViewerAuthenticationEndpointTargetSummary.Create(
                    "GET",
                    "custom://backend/token\r\nnext",
                    null,
                    null);

            Assert.That(summary.SignIn.HasOrigin, Is.False);
            Assert.That(
                summary.SignIn.DisplayValue,
                Is.EqualTo("GET  custom://backend/token  next"));
        }

        [Test]
        public void UserInfoQueryAndFragmentValuesAreNeverDisplayed()
        {
            ViewerAuthenticationEndpointTargetSummary summary =
                ViewerAuthenticationEndpointTargetSummary.Create(
                    "GET",
                    "https://user:password@example.com/token" +
                    "?access_token=secret#also-secret",
                    null,
                    null);

            Assert.That(
                summary.SignIn.Origin,
                Is.EqualTo("https://example.com"));
            Assert.That(
                summary.SignIn.DisplayValue,
                Is.EqualTo(
                    "GET  https://example.com/token" +
                    "?[configured values hidden]#[fragment hidden]"));
            Assert.That(
                summary.SignIn.DisplayValue,
                Does.Not.Contain("user"));
            Assert.That(
                summary.SignIn.DisplayValue,
                Does.Not.Contain("password"));
            Assert.That(
                summary.SignIn.DisplayValue,
                Does.Not.Contain("secret"));
        }
    }
}
