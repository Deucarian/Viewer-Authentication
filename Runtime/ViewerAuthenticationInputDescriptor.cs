using System;

namespace Deucarian.ViewerAuthentication
{
    /// <summary>
    /// Describes one transient value requested by an interactive authentication
    /// provider. The descriptor contains presentation metadata only and never a
    /// credential value.
    /// </summary>
    public sealed class ViewerAuthenticationInputDescriptor
    {
        /// <summary>Creates an interactive authentication input descriptor.</summary>
        public ViewerAuthenticationInputDescriptor(
            string key,
            string displayName = null,
            bool isSecret = false,
            bool isRequired = true,
            string description = null)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException(
                    "A non-empty input key is required.",
                    nameof(key));
            }

            Key = key.Trim();
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? Key
                : displayName.Trim();
            IsSecret = isSecret;
            IsRequired = isRequired;
            Description = string.IsNullOrWhiteSpace(description)
                ? null
                : description.Trim();
        }

        /// <summary>Gets the provider-defined stable input key.</summary>
        public string Key { get; }

        /// <summary>Gets the human-readable field label.</summary>
        public string DisplayName { get; }

        /// <summary>Gets whether the Editor must mask this value.</summary>
        public bool IsSecret { get; }

        /// <summary>Gets whether a non-empty value is required.</summary>
        public bool IsRequired { get; }

        /// <summary>Gets optional token-free guidance for the field.</summary>
        public string Description { get; }
    }
}
