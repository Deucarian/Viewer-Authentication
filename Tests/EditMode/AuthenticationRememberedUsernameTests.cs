using NUnit.Framework;
using UnityEngine;
using Deucarian.Authentication.Editor;

namespace Deucarian.Authentication.Tests
{
    public sealed class AuthenticationRememberedUsernameTests
    {
        [Test]
        public void RememberingIsOptInScopedAndCanBeForgotten()
        {
            var store = new AuthenticationRememberedUsernames();
            var input = new AuthenticationInputDescriptor("identity", "Email / Username");
            var development = new AuthenticationPersistenceIdentity("service", "development", "authority", "client");
            var production = new AuthenticationPersistenceIdentity("service", "production", "authority", "client");
            store.Remember(development, input, "example-user");
            Assert.That(store.Read(development, input), Is.Empty);
            store.SetEnabled(development, input, true);
            store.Remember(development, input, "example-user");
            Assert.That(store.Read(development, input), Is.EqualTo("example-user"));
            Assert.That(store.Read(production, input), Is.Empty);
            var restored = JsonUtility.FromJson<AuthenticationRememberedUsernames>(JsonUtility.ToJson(store));
            Assert.That(restored.Read(development, input), Is.EqualTo("example-user"));
            store.SetEnabled(development, input, false);
            Assert.That(store.Read(development, input), Is.Empty);
        }

        [TestCase("password", true)]
        [TestCase("identity", true)]
        [TestCase("access_token", false)]
        [TestCase("arbitrary", false)]
        public void SecretsAndNonIdentityInputsCannotBeStored(string key, bool secret)
        {
            var store = new AuthenticationRememberedUsernames();
            var identity = new AuthenticationPersistenceIdentity("service", "development", "authority", "client");
            var descriptor = new AuthenticationInputDescriptor(key, isSecret: secret);
            store.SetEnabled(identity, descriptor, true);
            store.Remember(identity, descriptor, "must-not-be-stored");
            Assert.That(store.Enabled(identity, descriptor), Is.False);
            Assert.That(JsonUtility.ToJson(store), Does.Not.Contain("must-not-be-stored"));
        }
    }
}
