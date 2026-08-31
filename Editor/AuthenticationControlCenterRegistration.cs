using System;
using System.Collections.Generic;
using Deucarian.Editor;
using Deucarian.Session;
using UnityEditor;

namespace Deucarian.Authentication.Editor
{
    [InitializeOnLoad]
    internal static class AuthenticationControlCenterRegistration
    {
        private const string PackageId = "com.deucarian.authentication";
        private static readonly IDisposable ToolRegistration;
        private static readonly IDisposable CardRegistration;

        static AuthenticationControlCenterRegistration()
        {
            ToolRegistration = DeucarianToolRegistry.Register(
                new DeucarianToolDescriptor(
                    DeucarianToolIds.Authentication,
                    "Authentication",
                    "Inspect and manage the active development authentication session.",
                    DeucarianControlCenterArea.Connections,
                    AuthenticationEditorWindow.Open,
                    PackageId,
                    searchTerms: new[] { "authentication", "session", "sign in" },
                    order: 100));

            CardRegistration = DeucarianControlCenterRegistry.RegisterCardProvider(
                new AuthenticationCardProvider());
        }

        private sealed class AuthenticationCardProvider :
            IDeucarianControlCenterCardProvider
        {
            public string Id => PackageId + ".control-center";

            public IEnumerable<DeucarianControlCenterCard> Capture(
                DeucarianControlCenterContext context)
            {
                IReadOnlyList<AuthenticationTarget> targets =
                    AuthenticationTargetRegistry.Targets;
                DeucarianControlCenterStatus status =
                    DeucarianControlCenterStatus.Warning;
                string statusText = "No active target";

                if (targets.Count == 1)
                {
                    AuthenticationStatus lifecycle = targets[0].Session.Status.Status;
                    statusText = lifecycle.ToString();
                    status = ConvertStatus(lifecycle);
                }
                else if (targets.Count > 1)
                {
                    status = DeucarianControlCenterStatus.Info;
                    statusText = targets.Count + " active targets";
                }

                return new[]
                {
                    new DeucarianControlCenterCard(
                        PackageId + ".status",
                        DeucarianControlCenterArea.Connections,
                        "Authentication",
                        "Sanitized local authentication lifecycle state.",
                        PackageId,
                        status,
                        statusText,
                        order: 100,
                        details: new[]
                        {
                            "Registered targets: " + targets.Count
                        },
                        actions: new[]
                        {
                            new DeucarianControlCenterAction(
                                PackageId + ".open",
                                "Open Authentication",
                                AuthenticationEditorWindow.Open)
                        },
                        searchTerms: new[] { "authentication", "session", "connection" })
                };
            }

            private static DeucarianControlCenterStatus ConvertStatus(
                AuthenticationStatus status)
            {
                switch (status)
                {
                    case AuthenticationStatus.Active:
                    case AuthenticationStatus.ExpiryUnknown:
                        return DeucarianControlCenterStatus.Success;
                    case AuthenticationStatus.Expiring:
                    case AuthenticationStatus.Missing:
                        return DeucarianControlCenterStatus.Warning;
                    case AuthenticationStatus.Expired:
                        return DeucarianControlCenterStatus.Error;
                    default:
                        return DeucarianControlCenterStatus.Info;
                }
            }
        }
    }
}
