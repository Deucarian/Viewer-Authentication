using System.Threading;
using System.Threading.Tasks;
using Deucarian.Session;
using UnityEngine;
namespace Deucarian.Authentication.Samples.DefinitionWorkflow
{
    [DefaultExecutionOrder(-2000)]
    public sealed class SampleAuthenticationSetup : MonoBehaviour
    {
        [SerializeField] private AuthenticationHost host;
        private void Awake() => host.Configure(AuthenticationSession.CreateTransient(), new LocalAcquisition());
        private sealed class LocalAcquisition : IAuthenticationAcquisitionProvider
        {
            public string DisplayName => "Local sample";
            public Task<SessionResult> AcquireAsync(ISessionService service, CancellationToken cancellationToken = default) =>
                service.ReplaceAccessTokenAsync("non-secret-local-sample-value", cancellationToken: cancellationToken);
        }
    }
}
