using System;
using UnityEngine;
using Deucarian.Session;
namespace Deucarian.Authentication.Samples.DefinitionWorkflow
{
    /// <summary>Small caller example. The configured scene hosts own services and resource lifetimes.</summary>
    public sealed class ViewerAuthenticationWorkflow : MonoBehaviour
    {
        [SerializeField] private AuthenticationHost host;
        [SerializeField] private AuthenticationTrigger trigger;
        private string status = "Ready. Choose an action below.";
        public string Status => status;
        public async void SignIn() { var result = await host.SignInAsync(); status = result.Succeeded ? "Mock authentication succeeded." : "Authentication failed."; }
        public async void SignOut() { var result = await host.SignOutAsync(); status = result.Succeeded ? "Signed out." : "Sign out failed."; }
        public void SignInComponent() { trigger.SignIn(); status = "Local mock sign-in requested through the component."; }
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(24, 24, Math.Min(540, Screen.width - 48), Screen.height - 48), GUI.skin.box);
            GUILayout.Label("Viewer-Authentication — definition workflow");
            GUILayout.Label("A mock acquisition provider exercises the real authentication host and session. No backend or real credentials are used or displayed.");
            GUILayout.Space(12);
            if (GUILayout.Button("Sign in with local mock", GUILayout.Height(32))) { try { SignIn(); } catch (Exception error) { status = error.Message; } }
            if (GUILayout.Button("Sign out", GUILayout.Height(32))) { try { SignOut(); } catch (Exception error) { status = error.Message; } }
            if (GUILayout.Button("Sign in from component", GUILayout.Height(32))) { try { SignInComponent(); } catch (Exception error) { status = error.Message; } }
            GUILayout.Space(12);
            GUILayout.Label(status);
            GUILayout.EndArea();
        }
    }
}
