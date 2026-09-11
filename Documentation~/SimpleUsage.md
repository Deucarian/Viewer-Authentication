# Simple usage

Configure AuthenticationHost once with your IAuthenticationSession and IAuthenticationAcquisitionProvider. The acquisition provider owns the application's real sign-in flow and receives the same authoritative session through a scoped forwarding interface. SignIn/SignOut return SessionResult. Replacing sign-in, signing out, or destroying the host cancels the old acquisition; even a late provider that ignores its cancellation argument cannot start a new mutation through the old scope. The host owns no tokens or persistent store and exposes only the existing token-free Status snapshot. Never retain the supplied acquisition scope after its operation finishes.

Import the **Simple Usage** sample from Unity Package Manager. Its caller script is:

```csharp
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
```
