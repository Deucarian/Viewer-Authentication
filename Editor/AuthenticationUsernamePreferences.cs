using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Deucarian.Authentication.Editor
{
    [Serializable]
    internal sealed class AuthenticationRememberedUsernames
    {
        [Serializable] private sealed class Entry { public string scope, input, username; }
        [SerializeField] private List<Entry> entries = new List<Entry>();

        internal static bool IsUsername(AuthenticationInputDescriptor descriptor)
        {
            if (descriptor == null || descriptor.IsSecret) return false;
            switch (descriptor.Key.ToLowerInvariant())
            {
                case "identity": case "username": case "user_name": case "email": case "emailaddress": case "email_address": return true;
                default: return false;
            }
        }

        private static string Scope(AuthenticationPersistenceIdentity identity)
        {
            if (identity == null) return null;
            using (var hash = SHA256.Create())
                return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(identity.StableKey)));
        }

        private Entry Find(AuthenticationPersistenceIdentity identity, AuthenticationInputDescriptor descriptor)
        {
            if (identity == null || !IsUsername(descriptor)) return null;
            string scope = Scope(identity);
            return entries.Find(value => value.scope == scope && value.input == descriptor.Key);
        }

        internal bool Enabled(AuthenticationPersistenceIdentity identity, AuthenticationInputDescriptor descriptor) => Find(identity, descriptor) != null;
        internal string Read(AuthenticationPersistenceIdentity identity, AuthenticationInputDescriptor descriptor) => Find(identity, descriptor)?.username ?? string.Empty;
        internal void SetEnabled(AuthenticationPersistenceIdentity identity, AuthenticationInputDescriptor descriptor, bool enabled)
        {
            var entry = Find(identity, descriptor);
            if (!enabled) { if (entry != null) entries.Remove(entry); return; }
            if (entry != null || identity == null || !IsUsername(descriptor)) return;
            entries.Add(new Entry { scope = Scope(identity), input = descriptor.Key, username = string.Empty });
        }
        internal void Remember(AuthenticationPersistenceIdentity identity, AuthenticationInputDescriptor descriptor, string value)
        {
            var entry = Find(identity, descriptor);
            if (entry != null) entry.username = value != null && value.Length <= 320 ? value : string.Empty;
        }
    }

    [FilePath("UserSettings/DeucarianAuthenticationUsernames.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class AuthenticationUsernamePreferences : ScriptableSingleton<AuthenticationUsernamePreferences>
    {
        [SerializeField] private AuthenticationRememberedUsernames values = new AuthenticationRememberedUsernames();
        internal bool Enabled(AuthenticationTarget target, AuthenticationInputDescriptor input) => values.Enabled(target?.PersistenceIdentity, input);
        internal string Read(AuthenticationTarget target, AuthenticationInputDescriptor input) => values.Read(target?.PersistenceIdentity, input);
        internal void SetEnabled(AuthenticationTarget target, AuthenticationInputDescriptor input, bool enabled, string current)
        {
            values.SetEnabled(target?.PersistenceIdentity, input, enabled);
            values.Remember(target?.PersistenceIdentity, input, current);
            Save(true);
        }
        internal void Remember(AuthenticationTarget target, IReadOnlyList<AuthenticationInputDescriptor> descriptors, AuthenticationTransientInputState inputs)
        {
            if (descriptors == null) return;
            bool changed = false;
            foreach (var descriptor in descriptors)
                if (values.Enabled(target?.PersistenceIdentity, descriptor))
                { values.Remember(target.PersistenceIdentity, descriptor, inputs.GetValue(descriptor.Key)); changed = true; }
            if (changed) Save(true);
        }
    }
}
