using UnityEngine;

namespace Deucarian.Authentication.Samples.SimpleUsage
{
    public sealed class SimpleUsageExample : MonoBehaviour
    {
        [SerializeField] private AuthenticationHost authentication;
        public System.Threading.Tasks.Task SignIn() => authentication.SignInAsync();
        public System.Threading.Tasks.Task SignOut() => authentication.SignOutAsync();
    }
}
