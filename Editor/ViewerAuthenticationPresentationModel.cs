using System;

namespace Deucarian.ViewerAuthentication.Editor
{
    internal enum ViewerAuthenticationPresentationTone
    {
        Disabled,
        Info,
        Success,
        Warning,
        Error
    }

    internal enum ViewerAuthenticationPrimaryActionKind
    {
        None,
        RevealCredentials,
        RevealManual,
        Acquire,
        CheckAgain
    }

    internal sealed class ViewerAuthenticationPresentationInput
    {
        internal ViewerAuthenticationStatusSnapshot Status { get; set; }

        internal ViewerAuthenticationAssessmentSnapshot Validation { get; set; }

        internal ViewerAuthenticationEndpointTargetSummary Endpoints { get; set; }

        internal bool IsChecking { get; set; }

        internal bool IsBusy { get; set; }

        internal bool HasValidationProvider { get; set; }

        internal bool HasAcquisitionProvider { get; set; }

        internal bool HasAnyProvider { get; set; }

        internal bool HasInteractiveInputs { get; set; }

        internal bool CredentialsExpanded { get; set; }

        internal bool ManualExpanded { get; set; }

        internal bool RequiredAcquisitionValuesPresent { get; set; }

        internal DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
    }

    internal sealed class ViewerAuthenticationPresentationModel
    {
        private ViewerAuthenticationPresentationModel(
            string statusLabel,
            string statusDetail,
            ViewerAuthenticationPresentationTone tone,
            string targetLabel,
            string targetBadgeLabel,
            string expiryLabel,
            ViewerAuthenticationPrimaryActionKind primaryAction,
            string primaryActionLabel,
            bool primaryActionEnabled,
            bool acquisitionActionEnabled)
        {
            StatusLabel = statusLabel;
            StatusDetail = statusDetail;
            Tone = tone;
            TargetLabel = targetLabel;
            TargetBadgeLabel = targetBadgeLabel;
            ExpiryLabel = expiryLabel;
            PrimaryAction = primaryAction;
            PrimaryActionLabel = primaryActionLabel;
            PrimaryActionEnabled = primaryActionEnabled;
            AcquisitionActionEnabled = acquisitionActionEnabled;
        }

        internal string StatusLabel { get; }

        internal string StatusDetail { get; }

        internal ViewerAuthenticationPresentationTone Tone { get; }

        internal string TargetLabel { get; }

        internal string TargetBadgeLabel { get; }

        internal string ExpiryLabel { get; }

        internal ViewerAuthenticationPrimaryActionKind PrimaryAction { get; }

        internal string PrimaryActionLabel { get; }

        internal bool PrimaryActionEnabled { get; }

        internal bool AcquisitionActionEnabled { get; }

        internal static ViewerAuthenticationPresentationModel Resolve(
            ViewerAuthenticationPresentationInput input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            ViewerAuthenticationStatusSnapshot status = input.Status ??
                new ViewerAuthenticationStatusSnapshot(
                    ViewerAuthenticationStatus.Missing,
                    false,
                    false,
                    null);
            ResolveStatus(
                status,
                input.Validation,
                input.IsChecking,
                input.HasValidationProvider,
                out string statusLabel,
                out string statusDetail,
                out ViewerAuthenticationPresentationTone tone);
            ResolveTarget(
                input.Endpoints,
                input.HasAnyProvider,
                out string targetLabel,
                out string targetBadgeLabel);

            bool invalidToken = !status.HasAccessToken ||
                                status.Status ==
                                ViewerAuthenticationStatus.Expired ||
                                input.Validation?.Result.Status ==
                                ViewerAuthenticationValidationStatus.Rejected;
            bool inconclusive = input.Validation?.Result.Status ==
                                ViewerAuthenticationValidationStatus
                                    .Inconclusive;
            ViewerAuthenticationPrimaryActionKind action =
                ViewerAuthenticationPrimaryActionKind.None;
            string actionLabel = string.Empty;
            if (invalidToken)
            {
                if (input.HasAcquisitionProvider)
                {
                    ResolveAcquisitionAction(
                        input,
                        "Sign in",
                        out action,
                        out actionLabel);
                }
                else if (!input.ManualExpanded)
                {
                    action = ViewerAuthenticationPrimaryActionKind
                        .RevealManual;
                    actionLabel = "Enter token";
                }
            }
            else if (inconclusive &&
                     status.HasAccessToken &&
                     input.HasValidationProvider)
            {
                action = ViewerAuthenticationPrimaryActionKind.CheckAgain;
                actionLabel = "Check again";
            }
            else if (input.HasAcquisitionProvider)
            {
                ResolveAcquisitionAction(
                    input,
                    "Get new token",
                    out action,
                    out actionLabel);
            }
            if (input.CredentialsExpanded || input.ManualExpanded)
            {
                action = ViewerAuthenticationPrimaryActionKind.None;
                actionLabel = string.Empty;
            }

            bool blocked = input.IsBusy || input.IsChecking;
            bool actionEnabled = action !=
                                 ViewerAuthenticationPrimaryActionKind.None &&
                                 !blocked;
            bool acquisitionEnabled =
                input.HasAcquisitionProvider &&
                input.RequiredAcquisitionValuesPresent &&
                !blocked;

            return new ViewerAuthenticationPresentationModel(
                statusLabel,
                statusDetail,
                tone,
                targetLabel,
                targetBadgeLabel,
                ResolveExpiryLabel(
                    status,
                    input.Validation,
                    input.UtcNow),
                action,
                actionLabel,
                actionEnabled,
                acquisitionEnabled);
        }

        private static void ResolveAcquisitionAction(
            ViewerAuthenticationPresentationInput input,
            string label,
            out ViewerAuthenticationPrimaryActionKind action,
            out string actionLabel)
        {
            actionLabel = label;
            if (input.HasInteractiveInputs)
            {
                action = input.CredentialsExpanded
                    ? ViewerAuthenticationPrimaryActionKind.None
                    : ViewerAuthenticationPrimaryActionKind
                        .RevealCredentials;
                return;
            }

            action = ViewerAuthenticationPrimaryActionKind.Acquire;
        }

        private static void ResolveStatus(
            ViewerAuthenticationStatusSnapshot status,
            ViewerAuthenticationAssessmentSnapshot validation,
            bool checking,
            bool hasValidationProvider,
            out string label,
            out string detail,
            out ViewerAuthenticationPresentationTone tone)
        {
            if (checking)
            {
                label = "Checking connection";
                detail = "Verifying the current token with the server.";
                tone = ViewerAuthenticationPresentationTone.Info;
                return;
            }

            if (!status.HasAccessToken)
            {
                label = "Not connected";
                detail = "Sign in to get an access token.";
                tone = ViewerAuthenticationPresentationTone.Disabled;
                return;
            }

            if (status.Status == ViewerAuthenticationStatus.Expired)
            {
                label = "Token expired";
                detail = "Sign in again to continue.";
                tone = ViewerAuthenticationPresentationTone.Error;
                return;
            }

            if (validation != null)
            {
                switch (validation.Result.Status)
                {
                    case ViewerAuthenticationValidationStatus.Verified:
                        label = "Connected";
                        detail = "The server accepted the current token.";
                        tone = ViewerAuthenticationPresentationTone.Success;
                        return;
                    case ViewerAuthenticationValidationStatus.Rejected:
                        label = "Token rejected";
                        detail = "Sign in again to continue.";
                        tone = ViewerAuthenticationPresentationTone.Error;
                        return;
                    default:
                        label = "Unable to verify";
                        detail = "The token was kept. Check the connection and try again.";
                        tone = ViewerAuthenticationPresentationTone.Warning;
                        return;
                }
            }

            switch (status.Status)
            {
                case ViewerAuthenticationStatus.Active:
                    label = "Token ready";
                    detail = hasValidationProvider
                        ? "Waiting for server verification."
                        : "The token is valid locally.";
                    tone = ViewerAuthenticationPresentationTone.Info;
                    return;
                case ViewerAuthenticationStatus.Expiring:
                    label = "Expires soon";
                    detail = "Get a new token before this one expires.";
                    tone = ViewerAuthenticationPresentationTone.Warning;
                    return;
                case ViewerAuthenticationStatus.Expired:
                    label = "Token expired";
                    detail = "Sign in again to continue.";
                    tone = ViewerAuthenticationPresentationTone.Error;
                    return;
                case ViewerAuthenticationStatus.ExpiryUnknown:
                    label = "Token present";
                    detail = hasValidationProvider
                        ? "Expiry is unknown; server verification is pending."
                        : "The expiry cannot be read locally.";
                    tone = ViewerAuthenticationPresentationTone.Warning;
                    return;
                default:
                    label = "Not connected";
                    detail = "Sign in to get an access token.";
                    tone = ViewerAuthenticationPresentationTone.Disabled;
                    return;
            }
        }

        private static void ResolveTarget(
            ViewerAuthenticationEndpointTargetSummary endpoints,
            bool hasAnyProvider,
            out string label,
            out string badge)
        {
            if (endpoints == null || !endpoints.HasAnyEndpoint)
            {
                label = hasAnyProvider
                    ? "Endpoint details unavailable"
                    : "No backend endpoint configured";
                badge = hasAnyProvider ? "CUSTOM" : "UNSET";
                return;
            }

            if (endpoints.HasDifferentOrigins)
            {
                label = "Multiple backend targets configured";
                badge = "MIXED";
                return;
            }

            if (Uri.TryCreate(
                    endpoints.SharedOrigin,
                    UriKind.Absolute,
                    out Uri origin))
            {
                label = origin.IsDefaultPort
                    ? origin.Host
                    : origin.Host + ":" + origin.Port;
                badge = "CURRENT";
                return;
            }

            label = "Backend resolved at request time";
            badge = "CURRENT";
        }

        private static string ResolveExpiryLabel(
            ViewerAuthenticationStatusSnapshot status,
            ViewerAuthenticationAssessmentSnapshot validation,
            DateTimeOffset utcNow)
        {
            if (!status.HasAccessToken)
            {
                return "No access token";
            }

            DateTimeOffset? expiry = status.ExpiresAtUtc ??
                                     validation?.Result.ExpiresAtUtc;
            if (!expiry.HasValue)
            {
                return "Expiry unknown";
            }

            TimeSpan remaining = expiry.Value.ToUniversalTime() -
                                 utcNow.ToUniversalTime();
            if (remaining <= TimeSpan.Zero)
            {
                return "Expired";
            }

            if (remaining < TimeSpan.FromMinutes(2))
            {
                return "Expires in under 2 minutes";
            }

            if (remaining < TimeSpan.FromHours(1))
            {
                return "Expires in " +
                       (int)Math.Ceiling(remaining.TotalMinutes) +
                       " minutes";
            }

            if (remaining < TimeSpan.FromHours(48))
            {
                int hours = (int)Math.Ceiling(remaining.TotalHours);
                return "Expires in " + hours +
                       (hours == 1 ? " hour" : " hours");
            }

            if (remaining < TimeSpan.FromDays(14))
            {
                return "Expires in " +
                       (int)Math.Ceiling(remaining.TotalDays) +
                       " days";
            }

            return "Expires " + expiry.Value.ToLocalTime().ToString("g");
        }
    }

    internal sealed class ViewerAuthenticationDisclosureState
    {
        internal bool ConnectionDetailsExpanded { get; set; }

        internal bool CredentialsExpanded { get; set; }

        internal bool ManualToolsExpanded { get; set; }

        internal bool LocalStorageExpanded { get; set; }

        internal void SetCredentialsExpanded(bool expanded)
        {
            CredentialsExpanded = expanded;
            if (expanded)
            {
                ManualToolsExpanded = false;
            }
        }

        internal void SetManualToolsExpanded(bool expanded)
        {
            ManualToolsExpanded = expanded;
            if (expanded)
            {
                CredentialsExpanded = false;
            }
        }

        internal void Reset()
        {
            ConnectionDetailsExpanded = false;
            CredentialsExpanded = false;
            ManualToolsExpanded = false;
            LocalStorageExpanded = false;
        }

        internal void CompleteAcquisition()
        {
            CredentialsExpanded = false;
        }
    }
}
