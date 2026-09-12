using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.Authentication.Editor;
using Deucarian.Editor;
using Deucarian.Session;
using Deucarian.Session.APIIntegration;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Deucarian.Authentication.Tests
{
    public sealed class AuthenticationFeedbackTests
    {
        [UnityTest]
        public IEnumerator RememberFollowsBothInputsAndFeedbackReflectsOperationOutcome()
        {
            const string id = "feedback-fixture";
            string selected = AuthenticationLocalSettings.instance.SelectedTargetId;
            var session = AuthenticationSession.CreateTransient();
            using (AuthenticationTargetRegistry.Register(id, "Feedback fixture", session, new Provider(), null,
                new AuthenticationPersistenceIdentity(id, "test", "fixture.invalid", "test")))
            {
                AuthenticationWindow window = null;
                FeedbackLayoutWindow host = null;
                try
                {
                    AuthenticationLocalSettings.instance.SetSelectedTarget(id);
                    window = ScriptableObject.CreateInstance<AuthenticationWindow>();
                    window.ExpandCredentials(true);
                    var root = new VisualElement();
                    root.style.flexGrow = 1;
                    host = ScriptableObject.CreateInstance<FeedbackLayoutWindow>();
                    host.titleContent = new GUIContent("Authentication feedback test");
                    host.position = new Rect(40, 40, 1100, 940);
                    host.Show(); host.rootVisualElement.Add(root);
                    using (var page = new AuthenticationPage(root, window))
                    {
                        for (int i = 0; i < 5; i++) yield return null;
                        var elements = root.Query<VisualElement>().ToList();
                        var username = root.Q<TextField>("authentication-input-username");
                        var password = root.Q<TextField>("authentication-input-password");
                        var remember = root.Q<Toggle>("authentication-remember-username");
                        Assert.NotNull(username); Assert.NotNull(password); Assert.NotNull(remember);
                        Assert.Less(elements.IndexOf(username), elements.IndexOf(password));
                        Assert.Less(elements.IndexOf(password), elements.IndexOf(remember));
                        Assert.IsTrue(password.isPasswordField);
                        Assert.GreaterOrEqual(remember.worldBound.yMin, password.worldBound.yMax);

                        Run(window, SessionTokenEndpointFailures.FromHttpStatus(404)); page.Update(true);
                        for (int i = 0; i < 5; i++) yield return null;
                        var feedback = root.Q<Label>("authentication-feedback");
                        Assert.That(feedback.text, Does.Contain("No authentication endpoint"));
                        Assert.IsTrue(feedback.ClassListContains("dw-card-status--error"));
                        Assert.IsFalse(feedback.ClassListContains("dw-card-status--success"));
                        Assert.IsTrue(root.Q("authentication-credentials").Contains(feedback));
                        Assert.IsFalse(feedback.enableRichText);
                        Assert.That(feedback.resolvedStyle.color, Is.EqualTo(DeucarianEditorSurfacePalette.Error));

                        Run(window, SessionResult.Failed("unknown", "secret-response-sentinel")); page.Update(true);
                        Assert.That(root.Q<Label>("authentication-feedback").text, Does.Not.Contain("secret-response-sentinel"));
                        Run(window, SessionResult.Success()); page.Update(true);
                        for (int i = 0; i < 5; i++) yield return null;
                        feedback = root.Q<Label>("authentication-feedback");
                        Assert.That(feedback.text, Is.EqualTo("Signed in successfully."));
                        Assert.IsTrue(feedback.ClassListContains("dw-card-status--success"));
                        Assert.IsFalse(feedback.ClassListContains("dw-card-status--error"));
                        Assert.That(feedback.resolvedStyle.color, Is.EqualTo(DeucarianEditorSurfacePalette.Success));
                    }
                }
                finally
                {
                    if (window != null) UnityEngine.Object.DestroyImmediate(window);
                    if (host != null) host.Close();
                    AuthenticationLocalSettings.instance.SetSelectedTarget(selected);
                }
            }
        }

        private static void Run(AuthenticationWindow window, SessionResult result)
        {
            Func<CancellationToken, Task<SessionResult>> operation = _ => Task.FromResult(result);
            typeof(AuthenticationWindow).GetMethod("RunOperation", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(window, new object[] { window.CapturePage().Target, operation, "Signed in successfully.", false, false, false, null });
        }

        private sealed class Provider : IInteractiveAuthenticationAcquisitionProvider
        {
            public string DisplayName => "Fixture sign in";
            public IReadOnlyList<AuthenticationInputDescriptor> InputDescriptors { get; } = new[] {
                new AuthenticationInputDescriptor("username", "Username"),
                new AuthenticationInputDescriptor("password", "Password", isSecret: true) };
            public Task<SessionResult> AcquireAsync(ISessionService service, CancellationToken token = default) =>
                throw new InvalidOperationException("The fixture never contacts a backend.");
            public Task<SessionResult> AcquireAsync(ISessionService service, AuthenticationInputValues values, CancellationToken token = default) =>
                throw new InvalidOperationException("The fixture never contacts a backend.");
        }

        private sealed class FeedbackLayoutWindow : EditorWindow { }
    }
}
