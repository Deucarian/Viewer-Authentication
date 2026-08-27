# Deucarian Authentication

`com.deucarian.authentication` provides generic authentication sessions,
acquisition and validation contracts, stable target registration, secure
Editor persistence, and shared Editor UI. The generic core has no viewer or
Command Routing dependency.

## Install

Install the package through the Deucarian Package Installer or pin the required
feature commit while this breaking migration is under review.

## Runtime composition

```csharp
using Deucarian.Authentication;

var session = AuthenticationSession.CreateTransient();
var identity = new AuthenticationPersistenceIdentity(
    "service.api-v2",
    "service.development",
    "https://api.example.invalid",
    "unity-editor");

using IDisposable registration = AuthenticationTargetRegistry.Register(
    "service-authentication",
    "Service Development",
    session,
    acquisitionProvider,
    validationProvider,
    identity);
```

The persistence identity is service/environment/authority/client/account based.
It must not be derived from a transient window or viewer registration ID.

`AuthenticationSession` composes Deucarian Session and Session API Integration.
Transient sessions use `InMemorySessionStore`. Integrations that own another
secure store can supply it through the `ISessionStore` constructor. The same
session backs API authentication, status, refresh, validation, and clear.

Acquisition and validation profiles are always explicitly assigned:

```csharp
AuthenticationEndpointProvider acquisition =
    AuthenticationEndpointProviderFactory.Create(profile, apiClient);
```

There is no Resources convention, default profile loader, or implicit target.

## Secure Editor persistence

Open `Tools > Deucarian > Authentication`.

Opt-in remembering writes an encrypted session envelope below
`Library/Deucarian/Authentication/Sessions`. On Windows, encryption uses the
OS Current User data-protection facility with per-identity entropy. The envelope
supports access token, refresh token, and expiry metadata, and is replaced
atomically. Tokens are never written to `UserSettings`, `ProjectSettings`,
`EditorPrefs`, `PlayerPrefs`, ScriptableObjects, UI summaries, or logs.

The one-time migration recognizes the former ignored plaintext
`UserSettings/DeucarianViewerAuthenticationSettings.asset`. It retains that
source if secure storage or identity resolution is unavailable. Only after an
encrypted save/load round trip exactly matches does it remove the plaintext
source.

Restoration is fail-closed. Offline or transient validation failures preserve
the protected session. Explicit sign-out and confirmed credential rejection
clear it; closing a viewer or failing a viewer connection does not.

## Optional viewer integration assembly

`Deucarian.Authentication.ViewerIntegration` contains the Command Routing
handlers and the optional `ViewerRuntimeConnectionProviderRegistry` seam. It
depends one way on `Deucarian.Authentication`; the generic core assembly does
not reference Command Routing or viewer contracts. Command Routing is not a
required UPM dependency: Unity enables this adapter assembly through an asmdef
version define only when `com.deucarian.command-routing` 0.2.4 or newer is
already installed by the viewer composition.

## Validation

Run the Package Registry validator, Unity EditMode tests, and
`git diff --check`. Secure-store tests verify encrypted access/refresh/expiry
round trips, reopen behavior, and clear semantics without exposing token values.
