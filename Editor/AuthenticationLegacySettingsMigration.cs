using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Deucarian.Authentication.Editor
{
    /// <summary>
    /// Migrates the former plaintext remembered token only after an encrypted
    /// round trip succeeds, then removes the plaintext source.
    /// </summary>
    [InitializeOnLoad]
    internal static class AuthenticationLegacySettingsMigration
    {
        private const string LegacyRelativePath =
            "UserSettings/DeucarianViewerAuthenticationSettings.asset";

        static AuthenticationLegacySettingsMigration()
        {
            AuthenticationTargetRegistry.RegistrationsChanged += Schedule;
            Schedule();
        }

        private static void Schedule()
        {
            EditorApplication.delayCall -= RunMigration;
            EditorApplication.delayCall += RunMigration;
        }

        private static void RunMigration()
        {
            TryMigrate();
        }

        internal static bool TryMigrate()
        {
            string path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                LegacyRelativePath));
            if (!File.Exists(path))
            {
                return true;
            }

            string content;
            try
            {
                content = File.ReadAllText(path);
            }
            catch
            {
                return false;
            }

            string secret = ReadScalar(
                content,
                "remembered" + "Access" + "Token");
            if (!AccessTokenInput.TryNormalize(secret, out string normalized))
            {
                secret = null;
                normalized = null;
                return false;
            }

            string owner = ReadScalar(content, "rememberedTargetId");
            if (string.IsNullOrWhiteSpace(owner))
            {
                owner = ReadScalar(content, "selectedTargetId");
            }

            string targetId = ResolveTargetId(owner);
            bool migrated = !string.IsNullOrWhiteSpace(targetId) &&
                AuthenticationLocalSettings.instance.TryMigrateLegacyToken(
                    targetId,
                    normalized);
            secret = null;
            normalized = null;
            content = null;
            if (!migrated)
            {
                return false;
            }

            try
            {
                File.Delete(path);
                return !File.Exists(path);
            }
            catch
            {
                return false;
            }
        }

        private static string ResolveTargetId(string requested)
        {
            if (!string.IsNullOrWhiteSpace(requested) &&
                AuthenticationTargetRegistry.TryGet(
                    requested.Trim(),
                    out AuthenticationTarget exact) &&
                exact.PersistenceIdentity != null)
            {
                return exact.Id;
            }

            AuthenticationTarget candidate = null;
            foreach (AuthenticationTarget target in
                AuthenticationTargetRegistry.Targets)
            {
                if (target.PersistenceIdentity == null)
                {
                    continue;
                }

                if (candidate != null)
                {
                    return null;
                }

                candidate = target;
            }

            return candidate?.Id;
        }

        private static string ReadScalar(string yaml, string field)
        {
            if (string.IsNullOrEmpty(yaml) || string.IsNullOrEmpty(field))
            {
                return null;
            }

            string prefix = "  " + field + ":";
            using (var reader = new StringReader(yaml))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (!line.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string value = line.Substring(prefix.Length).Trim();
                    if (value.Length >= 2 && value[0] == '"' &&
                        value[value.Length - 1] == '"')
                    {
                        value = value.Substring(1, value.Length - 2)
                            .Replace("\\\"", "\"")
                            .Replace("\\\\", "\\");
                    }

                    return value;
                }
            }

            return null;
        }
    }
}
