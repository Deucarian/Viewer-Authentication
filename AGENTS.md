# Deucarian Viewer Authentication Agent Notes

Package ID: `com.deucarian.viewer-authentication`

Follow the canonical Deucarian architecture and capability rules in Package
Registry.

## Ownership

This package owns reusable viewer authentication composition: the viewer-facing
session/token facade, sanitized authentication status, explicit development
target registration, authentication command adapters, and the local-only
editor workflow used to replace, refresh, clear, or acquire a development
token. It also owns the vendor-neutral optional runtime connection provider
registry used to share one authoritative session/API composition with a
generic viewer.

It must not own HTTP transport, backend login DTOs or endpoints, browser
transport, product context, Report/Activity commands, runtime viewer chrome,
or a second session implementation.

## Dependencies

- Session owns authenticated state, persistence contracts, and lifecycle.
- Session API Integration adapts the live Session token to API.
- API owns bearer-header formatting and HTTP transport.
- Command Routing owns the transport-independent handler protocol and redaction.
- Editor owns all editor chrome and workflow controls.

## Policies

- Never log, preview, include in status, or publish an access token.
- Editor token persistence is opt-in and project-local under `UserSettings`.
- Runtime target registration is explicit and registration disposal is
  idempotent; do not add reflection or runtime assembly scanning.
- Runtime connection resolution falls back only when no provider exists;
  provider failures and multiple-provider ambiguity fail closed.
- Acquisition providers are injected. Never add Simultria or another backend's
  endpoints, credentials, or login semantics here.
- Command results and events contain sanitized status only.
- Do not add direct Unity `Debug` calls.
- Work on `extract-packages` for the initial package pass. Do not create or push
  a remote until the package and governance metadata are approved.

## Validation

Run the shared Package Registry validator, EditMode tests, and
`git diff --check` before committing.
