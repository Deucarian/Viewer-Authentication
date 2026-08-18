using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.CommandRouting;
using Deucarian.Session;
using Newtonsoft.Json.Linq;

namespace Deucarian.ViewerAuthentication
{
    /// <summary>
    /// Generic token lifecycle command handler for any explicitly composed
    /// viewer authentication host.
    /// </summary>
    public sealed class ViewerAuthenticationCommandHandler<THost> :
        ICommandHandler<THost>
        where THost : class, IViewerAuthenticationHost
    {
        private const string HostUnavailableCode =
            "authentication_host_unavailable";
        private const string EventPublishFailedCode =
            "authentication_event_publish_failed";

        private static readonly IReadOnlyList<string> SupportedNames =
            Array.AsReadOnly(
                new[]
                {
                    ViewerAuthenticationCommandNames.UpdateAccessToken,
                    ViewerAuthenticationCommandNames.UpdateAccessTokenLegacy,
                    ViewerAuthenticationCommandNames.RefreshAccessToken,
                    ViewerAuthenticationCommandNames.ClearAccessToken
                });

        private readonly IViewerAuthenticationEventPublisher eventPublisher;

        /// <summary>Creates the generic authentication command handler.</summary>
        public ViewerAuthenticationCommandHandler(
            IViewerAuthenticationEventPublisher eventPublisher = null)
        {
            this.eventPublisher = eventPublisher;
        }

        /// <inheritdoc />
        public IReadOnlyList<string> CommandNames
        {
            get { return SupportedNames; }
        }

        /// <inheritdoc />
        public async Task<CommandResult> HandleAsync(
            CommandExecutionContext<THost> context,
            CancellationToken cancellationToken)
        {
            if (context == null ||
                context.Application == null ||
                context.Application.AuthenticationSession == null)
            {
                return CommandResult.Failure(
                    HostUnavailableCode,
                    "No viewer authentication session is available.");
            }

            IViewerAuthenticationSession session =
                context.Application.AuthenticationSession;
            SessionResult sessionResult;
            string eventName;
            switch (context.NormalizedCommandName)
            {
                case ViewerAuthenticationCommandNames.UpdateAccessToken:
                case ViewerAuthenticationCommandNames.UpdateAccessTokenLegacy:
                    if (!TryReadOptionalString(
                            context.Command.Payload,
                            "access_token",
                            out string accessToken) ||
                        !TryReadOptionalString(
                            context.Command.Payload,
                            "expires_at_utc",
                            out string expiryInput))
                    {
                        return CommandResult.Failure(
                            "invalid_payload",
                            "access_token and expires_at_utc must be strings when provided.",
                            CreateStatusPayload(session.Status));
                    }

                    if (!TryParseExpiry(
                            expiryInput,
                            out DateTimeOffset? expiresAtUtc))
                    {
                        return CommandResult.Failure(
                            "invalid_payload",
                            "expires_at_utc must be an ISO-8601 UTC timestamp.",
                            CreateStatusPayload(session.Status));
                    }

                    sessionResult = await session.ReplaceAccessTokenAsync(
                        accessToken,
                        expiresAtUtc,
                        cancellationToken);
                    eventName = ViewerAuthenticationEventNames.AccessTokenUpdated;
                    break;

                case ViewerAuthenticationCommandNames.RefreshAccessToken:
                    sessionResult =
                        await session.RefreshAsync(cancellationToken);
                    eventName = ViewerAuthenticationEventNames.AccessTokenRefreshed;
                    break;

                case ViewerAuthenticationCommandNames.ClearAccessToken:
                    sessionResult =
                        await session.ClearAsync(cancellationToken);
                    eventName = ViewerAuthenticationEventNames.AccessTokenCleared;
                    break;

                default:
                    return CommandResult.Failure(
                        "unsupported_authentication_command",
                        "The authentication command is not supported.",
                        CreateStatusPayload(session.Status));
            }

            ViewerAuthenticationStatusSnapshot status = session.Status;
            JObject payload = CreateStatusPayload(status);
            if (sessionResult == null || sessionResult.IsFailure)
            {
                SessionError error = sessionResult == null
                    ? null
                    : sessionResult.Error;
                return CommandResult.Failure(
                    error == null
                        ? "authentication_operation_failed"
                        : error.Code,
                    error == null
                        ? "The authentication operation failed."
                        : error.Message,
                    payload);
            }

            if (eventPublisher != null)
            {
                try
                {
                    await eventPublisher.PublishAsync(
                        eventName,
                        status,
                        cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception)
                {
                    return CommandResult.Failure(
                        EventPublishFailedCode,
                        "The authentication outcome event could not be published.",
                        payload);
                }
            }

            return CommandResult.Success(
                payload,
                "The authentication operation completed.");
        }

        private static bool TryReadOptionalString(
            JObject payload,
            string propertyName,
            out string value)
        {
            value = null;
            JToken token = payload == null ? null : payload[propertyName];
            if (token == null || token.Type == JTokenType.Null)
            {
                return true;
            }

            if (token.Type != JTokenType.String)
            {
                return false;
            }

            value = token.Value<string>();
            return true;
        }

        private static JObject CreateStatusPayload(
            ViewerAuthenticationStatusSnapshot status)
        {
            var payload = new JObject
            {
                ["status"] = status.Status.ToString(),
                ["has_access_token"] = status.HasAccessToken,
                ["can_refresh"] = status.CanRefresh,
                ["expiry_known"] = status.ExpiresAtUtc.HasValue
            };
            if (status.ExpiresAtUtc.HasValue)
            {
                payload["expires_at_utc"] =
                    status.ExpiresAtUtc.Value.ToUniversalTime().ToString(
                        "O",
                        CultureInfo.InvariantCulture);
            }

            return payload;
        }

        private static bool TryParseExpiry(
            string value,
            out DateTimeOffset? expiresAtUtc)
        {
            expiresAtUtc = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            if (!DateTimeOffset.TryParse(
                    value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal |
                    DateTimeStyles.AdjustToUniversal,
                    out DateTimeOffset parsed))
            {
                return false;
            }

            expiresAtUtc = parsed.ToUniversalTime();
            return true;
        }
    }
}
