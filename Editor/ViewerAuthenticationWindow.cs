using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.Editor;
using Deucarian.Session;
using UnityEditor;
using UnityEngine;

namespace Deucarian.ViewerAuthentication.Editor
{
    /// <summary>
    /// Local-only viewer authentication workflow. Token input is always masked
    /// and cleared immediately after an operation starts.
    /// </summary>
    public sealed class ViewerAuthenticationWindow : EditorWindow
    {
        private const string MenuPath =
            "Tools/Deucarian/Viewer/Authentication";

        private Vector2 scrollPosition;
        private string replacementToken = string.Empty;
        private string operationMessage = string.Empty;
        private bool operationFailed;
        private bool operationInProgress;
        private CancellationTokenSource operationCancellation;

        [MenuItem(MenuPath)]
        public static void Open()
        {
            ViewerAuthenticationWindow window =
                GetWindow<ViewerAuthenticationWindow>(
                    "Viewer Authentication");
            window.minSize = new Vector2(460f, 520f);
            window.Focus();
        }

        private void OnEnable()
        {
            ViewerAuthenticationTargetRegistry.TargetsChanged += Repaint;
        }

        private void OnDisable()
        {
            ViewerAuthenticationTargetRegistry.TargetsChanged -= Repaint;
            replacementToken = string.Empty;
            if (operationCancellation != null)
            {
                operationCancellation.Cancel();
                operationCancellation.Dispose();
                operationCancellation = null;
            }
        }

        private void OnGUI()
        {
            DeucarianEditorWindowChrome.DrawImGuiWindowBackground(position);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            GUILayout.Space(8f);

            DeucarianEditorChrome.DrawPackageHeader(
                "Viewer Authentication",
                "Local, token-safe session controls for registered Deucarian viewers.",
                DeucarianEditorIcons.GetPackageIcon("viewer-authentication"));

            IReadOnlyList<ViewerAuthenticationTarget> targets =
                ViewerAuthenticationTargetRegistry.Targets;
            if (targets.Count == 0)
            {
                DeucarianEditorCards.DrawCard(
                    "Active target",
                    () => EditorGUILayout.HelpBox(
                        "No viewer authentication target is registered. Enter Play Mode or initialize a viewer composition that calls ViewerAuthenticationTargetRegistry.Register.",
                        MessageType.Info));
                DrawLocalStorageCard(null);
                DrawOperationMessage();
                DrawFooter();
                EditorGUILayout.EndScrollView();
                return;
            }

            int selectedIndex = ResolveSelectedIndex(targets);
            string[] displayNames = new string[targets.Count];
            for (int i = 0; i < targets.Count; i++)
            {
                displayNames[i] = targets[i].DisplayName;
            }

            DeucarianEditorCards.DrawCard(
                "Active target",
                () =>
                {
                    int nextIndex = EditorGUILayout.Popup(
                        "Viewer",
                        selectedIndex,
                        displayNames);
                    if (nextIndex != selectedIndex)
                    {
                        selectedIndex = nextIndex;
                        ViewerAuthenticationLocalSettings.instance
                            .SetSelectedTarget(targets[selectedIndex].Id);
                        operationMessage = string.Empty;
                    }
                });

            ViewerAuthenticationTarget target = targets[selectedIndex];
            DrawStatusCard(target);
            DrawReplacementCard(target);
            DrawActionCard(target);
            DrawLocalStorageCard(target);
            DrawOperationMessage();
            DrawFooter();
            EditorGUILayout.EndScrollView();
        }

        private void DrawStatusCard(ViewerAuthenticationTarget target)
        {
            ViewerAuthenticationStatusSnapshot status = target.Session.Status;
            DeucarianEditorCards.DrawCard(
                "Session status",
                () =>
                {
                    DeucarianEditorStatusBadge.Draw(
                        GetStatusLabel(status.Status),
                        GetEditorStatus(status.Status),
                        GUILayout.Width(150f));
                    EditorGUILayout.LabelField(
                        "Expiry",
                        status.ExpiresAtUtc.HasValue
                            ? status.ExpiresAtUtc.Value.ToLocalTime().ToString("u")
                            : "Unknown");
                    EditorGUILayout.LabelField(
                        "Refresh configured",
                        status.CanRefresh ? "Yes" : "No");
                },
                "This snapshot never contains the access token.");
        }

        private void DrawReplacementCard(ViewerAuthenticationTarget target)
        {
            DeucarianEditorCards.DrawCard(
                "Replace access token",
                () =>
                {
                    EditorGUILayout.HelpBox(
                        "Paste a raw token or a value prefixed with Bearer. The field is masked and cleared immediately after submission.",
                        MessageType.Info);
                    replacementToken = EditorGUILayout.PasswordField(
                        "Access token",
                        replacementToken);
                    bool canSubmit =
                        !operationInProgress &&
                        !string.IsNullOrWhiteSpace(replacementToken);
                    if (DeucarianEditorButtons.Primary(
                            "Replace Access Token",
                            canSubmit,
                            GUILayout.ExpandWidth(true)))
                    {
                        string token = replacementToken;
                        replacementToken = string.Empty;
                        RunOperation(
                            target,
                            cancellationToken =>
                                target.Session.ReplaceAccessTokenAsync(
                                    token,
                                    null,
                                    cancellationToken),
                            "Access token replaced.",
                            rememberOnSuccess: true,
                            clearRememberedOnSuccess: false);
                        token = null;
                    }
                });
        }

        private void DrawActionCard(ViewerAuthenticationTarget target)
        {
            DeucarianEditorCards.DrawCard(
                "Actions",
                () =>
                {
                    ViewerAuthenticationStatusSnapshot status =
                        target.Session.Status;
                    bool hasToken = status.HasAccessToken;
                    bool canRefresh =
                        !operationInProgress &&
                        hasToken &&
                        target.Session.CanRefresh;
                    if (DeucarianEditorButtons.Secondary(
                            "Refresh Access Token",
                            canRefresh,
                            GUILayout.ExpandWidth(true)))
                    {
                        RunOperation(
                            target,
                            target.Session.RefreshAsync,
                            "Access token refreshed.",
                            rememberOnSuccess: true,
                            clearRememberedOnSuccess: false);
                    }

                    if (DeucarianEditorButtons.Secondary(
                            "Clear Session",
                            !operationInProgress && hasToken,
                            GUILayout.ExpandWidth(true)))
                    {
                        RunOperation(
                            target,
                            target.Session.ClearAsync,
                            "Authentication session cleared.",
                            rememberOnSuccess: false,
                            clearRememberedOnSuccess: true);
                    }

                    IViewerAuthenticationAcquisitionProvider provider =
                        target.AcquisitionProvider;
                    if (provider != null &&
                        DeucarianEditorButtons.Primary(
                            string.IsNullOrWhiteSpace(provider.DisplayName)
                                ? "Get Access Token"
                                : provider.DisplayName,
                            !operationInProgress,
                            GUILayout.ExpandWidth(true)))
                    {
                        RunOperation(
                            target,
                            cancellationToken => provider.AcquireAsync(
                                target.Session.SessionService,
                                cancellationToken),
                            "Authentication acquired.",
                            rememberOnSuccess: true,
                            clearRememberedOnSuccess: false);
                    }

                    if (provider == null)
                    {
                        EditorGUILayout.HelpBox(
                            "Get Token becomes available when the viewer registers a backend-specific acquisition provider.",
                            MessageType.None);
                    }
                });
        }

        private void DrawLocalStorageCard(ViewerAuthenticationTarget target)
        {
            ViewerAuthenticationLocalSettings settings =
                ViewerAuthenticationLocalSettings.instance;
            DeucarianEditorCards.DrawCard(
                "Local development convenience",
                () =>
                {
                    EditorGUILayout.HelpBox(
                        "Remembering is opt-in and writes only to this project's ignored UserSettings folder. It is not an OS credential vault.",
                        MessageType.Warning);
                    bool remember = EditorGUILayout.Toggle(
                        "Remember locally",
                        settings.RememberAccessToken);
                    if (remember != settings.RememberAccessToken)
                    {
                        settings.SetRememberAccessToken(remember);
                    }

                    using (new EditorGUI.DisabledScope(!remember))
                    {
                        bool autoApply = EditorGUILayout.Toggle(
                            "Auto-apply when missing",
                            settings.AutoApply);
                        if (autoApply != settings.AutoApply)
                        {
                            settings.SetAutoApply(autoApply);
                        }
                    }

                    EditorGUILayout.LabelField(
                        "Stored token",
                        settings.HasRememberedAccessToken
                            ? "Present (hidden)"
                            : "None");

                    bool targetNeedsToken = false;
                    if (target != null)
                    {
                        ViewerAuthenticationStatus targetStatus =
                            target.Session.Status.Status;
                        targetNeedsToken =
                            targetStatus == ViewerAuthenticationStatus.Missing ||
                            targetStatus == ViewerAuthenticationStatus.Expired;
                    }

                    if (DeucarianEditorButtons.Primary(
                            "Apply Remembered Token",
                            target != null &&
                            targetNeedsToken &&
                            settings.HasRememberedAccessToken &&
                            !operationInProgress,
                            GUILayout.ExpandWidth(true)))
                    {
                        string rememberedToken =
                            settings.RememberedAccessToken;
                        RunOperation(
                            target,
                            cancellationToken =>
                                target.Session.ReplaceAccessTokenAsync(
                                    rememberedToken,
                                    null,
                                    cancellationToken),
                            "Remembered token applied.",
                            rememberOnSuccess: false,
                            clearRememberedOnSuccess: false);
                        rememberedToken = null;
                    }

                    if (DeucarianEditorButtons.Secondary(
                            "Forget Local Token",
                            settings.HasRememberedAccessToken &&
                            !operationInProgress,
                            GUILayout.ExpandWidth(true)))
                    {
                        settings.ClearRememberedToken();
                        operationMessage = "Local remembered token cleared.";
                        operationFailed = false;
                    }

                    if (target != null &&
                        string.IsNullOrWhiteSpace(settings.SelectedTargetId))
                    {
                        settings.SetSelectedTarget(target.Id);
                    }
                });
        }

        private void DrawOperationMessage()
        {
            if (operationInProgress)
            {
                EditorGUILayout.HelpBox(
                    "Authentication operation in progress...",
                    MessageType.Info);
            }
            else if (!string.IsNullOrWhiteSpace(operationMessage))
            {
                EditorGUILayout.HelpBox(
                    operationMessage,
                    operationFailed ? MessageType.Warning : MessageType.Info);
            }
        }

        private void DrawFooter()
        {
            GUILayout.Space(4f);
            DeucarianEditorChrome.DrawFooterVersion(
                "com.deucarian.viewer-authentication",
                "0.1.0");
            GUILayout.Space(8f);
        }

        private int ResolveSelectedIndex(
            IReadOnlyList<ViewerAuthenticationTarget> targets)
        {
            string selectedId =
                ViewerAuthenticationLocalSettings.instance.SelectedTargetId;
            for (int i = 0; i < targets.Count; i++)
            {
                if (string.Equals(
                        targets[i].Id,
                        selectedId,
                        StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return 0;
        }

        private async void RunOperation(
            ViewerAuthenticationTarget target,
            Func<CancellationToken, Task<SessionResult>> operation,
            string successMessage,
            bool rememberOnSuccess,
            bool clearRememberedOnSuccess)
        {
            if (operationInProgress)
            {
                return;
            }

            operationInProgress = true;
            operationMessage = string.Empty;
            operationFailed = false;
            operationCancellation = new CancellationTokenSource();
            Repaint();

            try
            {
                SessionResult result = await operation(
                    operationCancellation.Token);
                if (result == null || result.IsFailure)
                {
                    operationFailed = true;
                    operationMessage = result == null || result.Error == null
                        ? "The authentication operation failed."
                        : result.Error.Message;
                    return;
                }

                ViewerAuthenticationLocalSettings settings =
                    ViewerAuthenticationLocalSettings.instance;
                if (clearRememberedOnSuccess)
                {
                    settings.ClearRememberedToken();
                }
                else if (rememberOnSuccess &&
                         settings.RememberAccessToken)
                {
                    settings.RememberToken(
                        target.Id,
                        target.Session.AccessToken);
                }

                operationMessage = successMessage;
            }
            catch (OperationCanceledException)
            {
                operationFailed = true;
                operationMessage = "The authentication operation was cancelled.";
            }
            catch (Exception)
            {
                operationFailed = true;
                operationMessage =
                    "The authentication operation failed unexpectedly.";
            }
            finally
            {
                operationInProgress = false;
                if (operationCancellation != null)
                {
                    operationCancellation.Dispose();
                    operationCancellation = null;
                }

                Repaint();
            }
        }

        private static string GetStatusLabel(
            ViewerAuthenticationStatus status)
        {
            switch (status)
            {
                case ViewerAuthenticationStatus.Active:
                    return "Active";
                case ViewerAuthenticationStatus.Expiring:
                    return "Expiring";
                case ViewerAuthenticationStatus.Expired:
                    return "Expired";
                case ViewerAuthenticationStatus.ExpiryUnknown:
                    return "Expiry unknown";
                default:
                    return "Missing";
            }
        }

        private static DeucarianEditorStatus GetEditorStatus(
            ViewerAuthenticationStatus status)
        {
            switch (status)
            {
                case ViewerAuthenticationStatus.Active:
                    return DeucarianEditorStatus.Success;
                case ViewerAuthenticationStatus.Expiring:
                case ViewerAuthenticationStatus.ExpiryUnknown:
                    return DeucarianEditorStatus.Warning;
                case ViewerAuthenticationStatus.Expired:
                    return DeucarianEditorStatus.Error;
                default:
                    return DeucarianEditorStatus.Disabled;
            }
        }
    }
}
