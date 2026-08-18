# Deucarian Viewer Authentication

`com.deucarian.viewer-authentication` provides the reusable authentication
boundary for Deucarian viewers. It composes Deucarian Session with the Session
API adapter, exposes sanitized authentication state, supplies generic
authentication command handlers, and adds a local-only Unity Editor workflow.

## Install

Install the package through the Deucarian Package Installer or its stable or
development Git URL.

## Runtime composition

```csharp
using Deucarian.ViewerAuthentication;

var authentication = new ViewerAuthenticationSession();
using IDisposable registration =
    ViewerAuthenticationTargetRegistry.Register(
        "viewer",
        "Viewer",
        authentication);

// API requests use the live token, including later replacements.
IApiAuthProvider apiAuthProvider = authentication.ApiAuthProvider;
```

`ViewerAuthenticationSession` (or its `CreateTransient` factory) uses `SessionService` with an
`InMemorySessionStore`. Supply an `ISessionRefreshService` to enable refresh
and automatic refresh-before-API behavior:

```csharp
var authentication = new ViewerAuthenticationSession(refreshService);
```

Without a refresh service, token replacement and clear remain available while
`CanRefresh` is false. Input may contain an optional `Bearer` prefix; the
session always stores the normalized token.

## Configurable endpoint reacquisition

Session API Integration 1.1.0 supplies a credential-free
`SessionTokenEndpointProfile`. It describes an endpoint, where transient input
values belong in the request, and the JSON paths that contain the returned
access token, optional refresh token, and expiry. The profile must never contain
credential values.

Create a profile with:

`Assets > Create > Deucarian > Session > Token Endpoint Profile`

Assign field keys, labels, masking, and request destinations in that asset. Put
the asset at this conventional Resources path when every viewer should discover
it without local composition code:

`Assets/Resources/Deucarian/ViewerAuthenticationTokenEndpointProfile.asset`

Then register the generic provider:

```csharp
ViewerAuthenticationEndpointProvider provider = null;
ViewerAuthenticationEndpointProviderFactory.TryCreateFromResources(
    out provider);

var authentication = ViewerAuthenticationSession.CreateTransient();
using IDisposable registration =
    ViewerAuthenticationTargetRegistry.Register(
        "viewer",
        "Viewer",
        authentication,
        provider);
```

An explicitly assigned asset can instead use
`ViewerAuthenticationEndpointProviderFactory.Create(profile, apiClient)`.
Neither factory stores credentials. Interactive values live only for the
operation and are cleared afterward.

## Commands

`ViewerAuthenticationCommandHandler<THost>` handles:

- `update_access_token`
- `updateaccesstoken` (legacy alias)
- `refresh_access_token`
- `clear_access_token`

The host implements `IViewerAuthenticationHost`. The handler reads its two
known payload fields explicitly, so its command DTO does not depend on
reflection or linker preservation. Command results and optional published
events contain only `ViewerAuthenticationStatusSnapshot`; they never contain
the token. Command Routing's normal redaction still protects incoming payload
history.

## Editor workflow

Open:

`Tools > Deucarian > Viewer > Authentication`

The window lists explicitly registered targets and offers:

- masked paste-and-replace input, cleared immediately after use;
- one `Refresh Token` action and a separate clear action;
- provider-defined masked or plain transient acquisition fields;
- sanitized Missing, Active, Expiring, Expired, or Expiry Unknown state;
- opt-in local remembering, one-click apply, and auto-apply.

Remembered tokens are stored only in the consuming project's ignored
`UserSettings` folder. This prevents source-control inclusion, but it is not an
OS credential vault. Do not enable remembering on a machine whose local Unity
settings are not appropriately protected.

When a target has an acquisition provider, `Refresh Token` reacquires a token
through that provider. This commonly means repeating a configured sign-in
exchange; it does not claim that the backend implements a formal refresh-token
protocol. Without an acquisition provider, the same button invokes the true
Session refresh service when one was supplied.

## Acquisition providers

Applications can inject `IViewerAuthenticationAcquisitionProvider` when
registering a target. The provider receives the target `ISessionService` and
performs a backend-specific login or token acquisition. This package never
assumes a login endpoint, account shape, or vendor.

Existing providers remain source-compatible. Providers that need shared
interactive fields additionally implement
`IInteractiveViewerAuthenticationAcquisitionProvider`. Their descriptors never
carry values; `ViewerAuthenticationInputValues` is a disposable, short-lived
value container.

## Editor-only legacy migration and export

`ViewerAuthenticationRememberedTokenFacade.TryMigrateLegacyToken` explicitly
moves a normalized legacy development token into the consuming project's
ignored UserSettings, enables local remembering and auto-apply, and reports
whether persistence succeeded. The legacy source must be cleared only after the
method returns true.

`ViewerAuthenticationRememberedTokenFacade.TryGet` retrieves that token only
for the exact stable target id. It exists for local Editor exports such as a
gitignored WebGL development context. Callers must clear their local reference
immediately and must never log or preview it. Ordinary `TryImport` remains
opt-in-only and never enables persistence silently.

## Security invariants

- Tokens are never logged, previewed, copied into status, command results, or
  outbound authentication events.
- The package contains no serialized token asset.
- Registry entries hold session/provider references only.
- Local remembering is explicit and can be cleared from the window.

## Validation

```powershell
python C:/Repositories/Package-Registry/Tools/deucarian_package_validator.py --registry-root C:/Repositories/Package-Registry --repository-root . --config deucarian-package.json
```

Run the package EditMode tests after code or assembly-definition changes and
run `git diff --check` before committing.
