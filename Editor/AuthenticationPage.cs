using System;
using System.Collections.Generic;
using Deucarian.Editor;
using UnityEngine;
using UnityEngine.UIElements;
using Ui = Deucarian.Editor.DeucarianEditorWorkspaceControls;

namespace Deucarian.Authentication.Editor
{
    internal sealed class AuthenticationPage : IDisposable
    {
        private readonly AuthenticationWindow controller;
        private readonly DeucarianEditorWorkspace workspace;
        private readonly List<DeucarianEditorWorkspaceForm> forms = new List<DeucarianEditorWorkspaceForm>();
        private readonly List<TextField> secrets = new List<TextField>();
        private AuthenticationPageState state;
        private AuthenticationTarget renderedTarget;
        private string structure;
        private DeucarianEditorStatusSummary summary;
        private Button primary, acquire, replace, clear;
        private Label feedback;
        private double nextUpdate;

        internal AuthenticationPage(VisualElement root, AuthenticationWindow controller)
        {
            this.controller = controller;
            workspace = new DeucarianEditorWorkspace(root, Application.productName);
            workspace.Title.text = "Authentication";
            workspace.Subtitle.text = "Manage your development session.";
            DeucarianEditorWorkspaceNavigation.Populate(workspace, DeucarianToolIds.Authentication);
            Update(true);
        }

        internal void Update(bool force = false)
        {
            if (!force && UnityEditor.EditorApplication.timeSinceStartup < nextUpdate) return;
            nextUpdate = UnityEditor.EditorApplication.timeSinceStartup + .25;
            state = controller.CapturePage();
            string signature = state.Targets.Count + "|" + state.Credentials + "|" + state.Manual + "|" +
                ((state.Credentials || state.Manual) ? state.Generation : 0);
            if (force || signature != structure || !ReferenceEquals(renderedTarget, state.Target))
            { structure = signature; renderedTarget = state.Target; Build(); }
            var presentation = state.Presentation;
            summary.Set(presentation?.StatusLabel ?? "No authentication target",
                presentation?.StatusDetail ?? "Configure a service integration to make its session available.",
                Tone(presentation?.Tone ?? AuthenticationPresentationTone.Info), DeucarianEditorIconIds.User);
            primary.text = presentation?.PrimaryActionLabel ?? "Sign in";
            primary.SetEnabled(presentation?.PrimaryActionEnabled == true);
            Ui.Show(primary, presentation != null && presentation.PrimaryAction != AuthenticationPrimaryActionKind.None);
            acquire?.SetEnabled(presentation?.AcquisitionActionEnabled == true);
            replace?.SetEnabled(!state.Busy && !state.Checking && !string.IsNullOrWhiteSpace(controller.Replacement));
            clear?.SetEnabled(!state.Busy && !state.Checking && state.Target?.Session.Status.HasAccessToken == true);
            feedback.text = state.Message ?? string.Empty;
            Ui.Show(feedback, !string.IsNullOrEmpty(feedback.text));
            foreach (var form in forms) form.Refresh();
        }

        internal void Activate() { nextUpdate = 0; Update(); }

        private void Build()
        {
            ClearSensitiveFields(); forms.Clear(); secrets.Clear();
            primary = acquire = replace = clear = null;
            workspace.Content.Clear(); workspace.Scope.Clear();
            var scroll = Ui.Scroll("authentication-content"); workspace.Content.Add(scroll);
            if (state.Targets.Count > 1)
            {
                var targets = new List<AuthenticationTarget>(state.Targets);
                var names = targets.ConvertAll(value => value.DisplayName);
                var selector = new PopupField<string>(names, Math.Max(0, targets.IndexOf(state.Target)));
                selector.RegisterValueChangedCallback(_ => { controller.SelectPageTarget(targets[selector.index]); Update(true); });
                workspace.Scope.Add(Ui.Field("Target", selector));
            }
            Ui.Show(workspace.Scope, state.Targets.Count > 1);
            Ui.Show(workspace.Tabs, false);
            var panel = Ui.Panel("authentication-session");
            panel.AddToClassList("dw-session-panel"); scroll.Add(panel);
            summary = new DeucarianEditorStatusSummary("authentication-status"); panel.Add(summary.Root);
            summary.Root.AddToClassList("dw-session-hero");
            primary = Ui.Button("Sign in", () => { controller.InvokePagePrimary(state); Update(true); }, true);
            primary.name = "authentication-primary";
            summary.Actions.Add(primary);
            feedback = Ui.Label(string.Empty, "dw-muted"); panel.Add(feedback);
            panel.Add(Ui.Divider());
            var overview = Form(panel);
            overview.ReadOnly("authentication-session-state", "Session", () => state.Target?.Session.Status.Status.ToString() ?? "Not active");
            overview.ReadOnly("authentication-storage-mode", "Storage", () => AuthenticationLocalSettings.instance.RememberAccessToken
                ? "Remembered locally" : "Session only");
            if (state.Target != null)
            {
                if (state.Credentials) BuildCredentials(panel);
                if (state.Manual) BuildManual(panel);
                BuildSession(Foldout(panel, "Session details"));
            }
            BuildStorage(panel);
        }

        private void BuildSession(VisualElement parent)
        {
            var form = Form(parent);
            form.ReadOnly("authentication-target", "Target", () => state.Target.DisplayName);
            form.ReadOnly("authentication-expiry", "Expires", () => state.Presentation.ExpiryLabel);
            form.ReadOnly("authentication-verification", "Verification", () => state.Verification);
            var details = Foldout(parent, "Connection details");
            var connection = Form(details);
            connection.ReadOnly(null, "Server", () => state.Endpoints.SharedOrigin ?? "Resolved by the configured provider");
            connection.ReadOnly(null, "Sign in", () => state.Endpoints.SignIn?.DisplayValue ?? "Not exposed by provider");
            connection.ReadOnly(null, "Token check", () => state.Endpoints.TokenCheck?.DisplayValue ?? "Not configured");
            connection.ReadOnly(null, "Last checked", () => state.Validation?.CheckedAtUtc.ToLocalTime().ToString("u") ?? "Not checked");
            connection.Note(() => state.Endpoints.HasDifferentOrigins ? "Sign-in and token-check routes use different servers. Verify that this is intentional."
                : "This project uses fixed endpoint profiles. Changing the displayed target does not switch server environments.");
            var actions = Ui.Actions(Ui.Button("Get a new token", () => { controller.ExpandCredentials(true); Update(true); }),
                Ui.Button("Replace token manually", () => { controller.ExpandManual(true); Update(true); }));
            details.Add(actions);
            actions[0].SetEnabled(state.HasProvider);
            clear = Ui.Button("Clear session", () => controller.ClearSession(state.Target), DeucarianEditorButtonRole.Destructive);
            clear.name = "authentication-clear";
            details.Add(clear);
        }

        private void BuildCredentials(VisualElement parent)
        {
            var content = Ui.Panel("authentication-credentials", "Sign in"); parent.Add(content);
            if (!state.HasProvider) { content.Add(Ui.Label("No acquisition provider is configured. You can enter a token manually.", "dw-muted")); return; }
            var form = Form(content);
            if (state.Inputs != null)
                foreach (var descriptor in state.Inputs)
                {
                    if (descriptor == null) continue;
                    var field = form.Text("authentication-input-" + descriptor.Key, descriptor.DisplayName,
                        () => controller.ReadInput(descriptor.Key), value => { controller.WriteInput(descriptor.Key, value); Update(); });
                    field.isPasswordField = descriptor.IsSecret;
                    field.tooltip = descriptor.Description;
                    if (descriptor.IsSecret) secrets.Add(field);
                }
            acquire = Ui.Button(state.Target.Session.Status.HasAccessToken ? "Get new token" : "Sign in", () =>
            { controller.SignIn(state); ClearSensitiveFields(); Update(); }, true);
            acquire.name = "authentication-acquire";
            content.Add(Ui.EndActions(Ui.Button("Cancel", () => { controller.ExpandCredentials(false); Update(true); }), acquire));
        }

        private void BuildManual(VisualElement parent)
        {
            var content = Ui.Panel("authentication-manual", "Replace token manually"); parent.Add(content);
            var field = Form(content).Text("authentication-token", "Access token", () => controller.Replacement,
                value => { controller.Replacement = value; Update(); });
            field.isPasswordField = true;
            field.tooltip = "Hidden, window-local input. Cleared immediately after submission.";
            secrets.Add(field);
            replace = Ui.Button("Replace token", () => { controller.Replace(state.Target); ClearSensitiveFields(); Update(); }, true);
            replace.name = "authentication-replace";
            content.Add(Ui.EndActions(Ui.Button("Cancel", () => { controller.ExpandManual(false); Update(true); }), replace));
        }

        private void BuildStorage(VisualElement parent)
        {
            var storage = Foldout(parent, "Local storage");
            var settings = AuthenticationLocalSettings.instance;
            var form = Form(storage);
            form.Note(() => "Opt-in, encrypted storage for this OS account and Unity project.");
            form.Toggle("authentication-remember", "Remember session", () => settings.RememberAccessToken, settings.SetRememberAccessToken);
            var apply = form.Toggle("authentication-auto-apply", "Auto-apply when missing", () => settings.AutoApply,
                value => { if (settings.RememberAccessToken) settings.SetAutoApply(value); });
            form.ReadOnly("authentication-stored", "Stored session", () => !settings.HasRememberedAccessToken ? "None"
                : state.Target == null || settings.HasRememberedAccessTokenFor(state.Target) ? "Present (hidden)" : "Saved for another target (hidden)");
            form.Action("authentication-apply-saved", "Apply saved session", () => controller.ApplySaved(state.Target),
                () => state.Target != null && !state.Busy && !state.Checking && settings.HasRememberedAccessTokenFor(state.Target)
                    && (state.Target.Session.Status.Status == AuthenticationStatus.Missing || state.Target.Session.Status.Status == AuthenticationStatus.Expired));
            form.Action("authentication-forget", "Forget saved session", () => { settings.ClearRememberedToken(); Update(); },
                () => settings.HasRememberedAccessToken && !state.Busy);
            storage.schedule.Execute(() => apply.SetEnabled(settings.RememberAccessToken)).Every(250);
        }

        private static Foldout Foldout(VisualElement parent, string title)
        { var foldout = new Foldout { text = title, value = false }; foldout.AddToClassList("dw-foldout"); parent.Add(foldout); return foldout; }
        private DeucarianEditorWorkspaceForm Form(VisualElement root)
        { var form = new DeucarianEditorWorkspaceForm(root); forms.Add(form); return form; }
        internal void ClearSensitiveFields() { foreach (var field in secrets) field.SetValueWithoutNotify(string.Empty); }
        private static DeucarianEditorStatus Tone(AuthenticationPresentationTone tone) => tone == AuthenticationPresentationTone.Success ? DeucarianEditorStatus.Success
            : tone == AuthenticationPresentationTone.Warning ? DeucarianEditorStatus.Warning : tone == AuthenticationPresentationTone.Error ? DeucarianEditorStatus.Error : DeucarianEditorStatus.Info;
        public void Dispose() { ClearSensitiveFields(); secrets.Clear(); forms.Clear(); workspace.Dispose(); }
    }
}
