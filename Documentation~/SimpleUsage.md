# Simple usage

Configure AuthenticationHost once with your IAuthenticationSession and IAuthenticationAcquisitionProvider. The acquisition provider owns the application's real sign-in flow and receives the same authoritative session through a scoped forwarding interface. SignIn/SignOut return SessionResult. Replacing sign-in, signing out, or destroying the host cancels the old acquisition; even a late provider that ignores its cancellation argument cannot start a new mutation through the old scope. The host owns no tokens or persistent store and exposes only the existing token-free Status snapshot. Never retain the supplied acquisition scope after its operation finishes.

Import the **Simple Usage** sample from Unity Package Manager. Its caller script is:

Definition fields now use named, domain-specific keys. Select an existing definition from the Inspector dropdown or pass the same named key in code. Declare each project key once in a marked key set; ordinary caller methods do not accept raw IDs. Generated keys for asset-authored definitions require no asset reference in the caller. Owner-issued selection and row handles represent runtime instances.

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
