using System;
using System.Collections.Generic;

namespace Deucarian.ViewerAuthentication.Editor
{
    /// <summary>
    /// Window-local input buffer. It is never serialized and releases secret
    /// references before an authentication operation is dispatched.
    /// </summary>
    internal sealed class ViewerAuthenticationTransientInputState
    {
        private readonly Dictionary<string, string> values =
            new Dictionary<string, string>(StringComparer.Ordinal);

        internal string GetValue(string key)
        {
            return !string.IsNullOrWhiteSpace(key) &&
                   values.TryGetValue(key, out string value)
                ? value ?? string.Empty
                : string.Empty;
        }

        internal void SetValue(string key, string value)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                values[key] = value ?? string.Empty;
            }
        }

        internal bool HasRequiredValues(
            IReadOnlyList<ViewerAuthenticationInputDescriptor> descriptors)
        {
            if (descriptors == null)
            {
                return true;
            }

            for (int i = 0; i < descriptors.Count; i++)
            {
                ViewerAuthenticationInputDescriptor descriptor =
                    descriptors[i];
                if (descriptor != null &&
                    descriptor.IsRequired &&
                    string.IsNullOrWhiteSpace(GetValue(descriptor.Key)))
                {
                    return false;
                }
            }

            return true;
        }

        internal ViewerAuthenticationInputValues CreateValues(
            IReadOnlyList<ViewerAuthenticationInputDescriptor> descriptors)
        {
            var selected = new List<KeyValuePair<string, string>>();
            if (descriptors != null)
            {
                for (int i = 0; i < descriptors.Count; i++)
                {
                    ViewerAuthenticationInputDescriptor descriptor =
                        descriptors[i];
                    if (descriptor != null)
                    {
                        selected.Add(
                            new KeyValuePair<string, string>(
                                descriptor.Key,
                                GetValue(descriptor.Key)));
                    }
                }
            }

            return new ViewerAuthenticationInputValues(selected);
        }

        internal void ClearSecrets(
            IReadOnlyList<ViewerAuthenticationInputDescriptor> descriptors)
        {
            if (descriptors == null)
            {
                return;
            }

            for (int i = 0; i < descriptors.Count; i++)
            {
                ViewerAuthenticationInputDescriptor descriptor =
                    descriptors[i];
                if (descriptor != null && descriptor.IsSecret)
                {
                    values.Remove(descriptor.Key);
                }
            }
        }

        internal void ClearAll()
        {
            values.Clear();
        }
    }
}
