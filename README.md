# Deucarian Viewer Authentication

`com.deucarian.viewer-authentication` provides the reusable authentication
boundary for Deucarian viewers. It composes Deucarian Session with the Session
API adapter, exposes sanitized authentication state, supplies generic
authentication command handlers, and adds a local-only Unity Editor workflow.

## Install

The repository is intentionally local-only during its initial extraction pass.
After release channels are approved, install the package through the Deucarian
Package Installer or its stable/development Git URL.

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
- refresh and clear actions;
- an optional provider-supplied Get Token action;
- sanitized Missing, Active, Expiring, Expired, or Expiry Unknown state;
- opt-in local remembering, one-click apply, and auto-apply.

Remembered tokens are stored only in the consuming project's ignored
`UserSettings` folder. This prevents source-control inclusion, but it is not an
OS credential vault. Do not enable remembering on a machine whose local Unity
settings are not appropriately protected.

## Acquisition providers

Applications can inject `IViewerAuthenticationAcquisitionProvider` when
registering a target. The provider receives the target `ISessionService` and
performs a backend-specific login or token acquisition. This package never
assumes a login endpoint, account shape, or vendor.

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
