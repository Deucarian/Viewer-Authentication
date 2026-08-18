using System;
using System.Collections.Generic;

namespace Deucarian.ViewerAuthentication
{
    /// <summary>
    /// Short-lived authentication input values. Dispose the instance as soon
    /// as the provider operation completes so retained credential references
    /// are released on success, failure, and cancellation.
    /// </summary>
    public sealed class ViewerAuthenticationInputValues : IDisposable
    {
        private readonly Dictionary<string, string> values;
        private bool isCleared;

        /// <summary>Creates a transient copy of the supplied input values.</summary>
        public ViewerAuthenticationInputValues(
            IEnumerable<KeyValuePair<string, string>> inputValues)
        {
            values = new Dictionary<string, string>(StringComparer.Ordinal);
            if (inputValues == null)
            {
                return;
            }

            foreach (KeyValuePair<string, string> pair in inputValues)
            {
                if (!string.IsNullOrWhiteSpace(pair.Key))
                {
                    values[pair.Key.Trim()] = pair.Value;
                }
            }
        }

        /// <summary>Gets whether all retained value references were cleared.</summary>
        public bool IsCleared
        {
            get { return isCleared; }
        }

        /// <summary>Attempts to resolve a value by provider-defined key.</summary>
        public bool TryGetValue(string key, out string value)
        {
            if (isCleared || string.IsNullOrWhiteSpace(key))
            {
                value = null;
                return false;
            }

            return values.TryGetValue(key, out value);
        }

        /// <summary>
        /// Gets a value by key, or null when it is absent or this container was
        /// cleared.
        /// </summary>
        public string GetValueOrDefault(string key)
        {
            return TryGetValue(key, out string value) ? value : null;
        }

        /// <summary>Clears every retained input-value reference.</summary>
        public void Clear()
        {
            values.Clear();
            isCleared = true;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Clear();
        }
    }
}
