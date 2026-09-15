using System;
using Deucarian.Session;
using UnityEngine;
using UnityEngine.Events;
namespace Deucarian.Authentication
{
    /// <summary>Connects Unity events to the configured authentication host without storing credentials.</summary>
    public sealed class AuthenticationTrigger : MonoBehaviour
    {
        [SerializeField] private AuthenticationHost host;
        [SerializeField] private UnityEvent succeeded = new UnityEvent();
        [SerializeField] private UnityEvent failed = new UnityEvent();
        public bool LastSucceeded { get; private set; }
        private AuthenticationHost Host => host != null ? host : throw new InvalidOperationException("Assign a configured AuthenticationHost to this AuthenticationTrigger.");
        public async void SignIn() => Complete(await Host.SignInAsync());
        public async void SignOut() => Complete(await Host.SignOutAsync());
        private void Complete(SessionResult result)
        {
            if (this == null) return;
            LastSucceeded = result.Succeeded;
            if (LastSucceeded) succeeded.Invoke(); else failed.Invoke();
        }
    }
}
