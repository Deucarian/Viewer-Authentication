# Changelog

## Asset workflow — Unreleased

- Place Remember username after all credential inputs; show red failure and green success feedback next to the active form, with sanitized actionable token-endpoint explanations.
- Require Session API Integration 1.3.0 for shared HTTP failure classification and document local storage Git exclusions.

- Add optional project-local, connection-scoped remembered usernames while excluding passwords, tokens and non-identity inputs from this preference store.

## [1.1.0] - 2026-09-11

- Add typed reusable definition authoring and/or scoped Inspector components that share the existing C# service behavior.
- Include a playable Definition Workflow sample with configured hosts, short callers and usage documentation.
- Align declared package dependencies with the definition-authoring development wave.


## [1.0.4] - 2026-09-11

- Use a native authentication page with retained non-sensitive state while preserving credential clearing, cancellation and existing authentication operations.
- Require Editor 1.10.6 for the shared native controls, typography, responsive layouts and accessible interaction states.

## [1.0.3] - 2026-09-09

- Register package tooling and navigation actions as shared Control Center pages. Preserve the domain workflow while using Editor-owned submenus, in-window navigation, and UI scaling.

## 1.0.2 - 2026-09-01

- Bound remembered Editor sessions to the exact current persistence identity
  before automatic, manual, inspection, facade-based restoration, or target-ID
  owner rebinding.
- Added a source-compatible full-composition fingerprint identity overload so
  host, endpoint-catalog, secondary-client, and policy changes fail closed.
- Preserved the legacy identity constructor and stable storage key for existing
  integrations that do not yet supply a composition fingerprint.
- Existing Viewer remembered sessions predate the composition fingerprint and
  intentionally do not auto-restore after this upgrade; sign in once to bind a
  new protected session to the complete current composition.

## 1.0.1 - 2026-08-31

- Registered the package workflow and a bounded, sanitized local-state card with Deucarian Control Center.
- Removed normal `Tools/Deucarian` menu exposure while preserving the standalone open API.
- Updated the shared Editor dependency to 1.2.0.
- Aligned API to 2.0.1 and the optional Command Routing version define to 0.2.5.

## 1.0.0 - 2026-08-26

- Renamed the package, core assembly, namespace, and public contracts from
  Viewer Authentication to generic Authentication responsibility names.
- Isolated Command Routing and runtime-viewer seams in the optional
  `Deucarian.Authentication.ViewerIntegration` assembly; the generic core has
  no viewer or Command Routing dependency. The UPM package no longer requires
  Command Routing; the adapter is enabled only when that package is installed.
- Replaced plaintext remembered-token `UserSettings` persistence with an
  atomic platform-protected session store supporting access tokens, refresh
  tokens, and expiry metadata.
- Added verified one-time migration of the former plaintext local settings;
  the old source is removed only after a protected round trip succeeds.
- Updated the optional viewer adapter to Command Routing 0.2.4 and Session API
  Integration 1.2.0 for the coordinated API 2.0 release.

## 0.5.1 - 2026-08-25

- Added a vendor-neutral Editor-session handoff that preserves one authenticated
  transient session across domain reloads and Edit/Play transitions.
- Handoffs require an exact caller-owned binding and are never written to
  project assets or persistent UserSettings.
## 0.5.0 - 2026-08-19

- Added an Editor-only remembered-token owner rebind operation for safe
  one-time migration between stable target IDs. The caller must match the
  current owner before it can assign the replacement owner.
- Owner rebinds never expose, copy, replace, log, or preview the token value and
  do not enable local remembering when it was disabled.
- Added an explicit, vendor-neutral runtime connection provider registry that
  supplies one stable authentication session, its composed API client, API
  base URL, and exact authenticated origins to optional viewer integrations.
- Zero providers preserve consumer fallback behavior; provider failure and
  multiple-provider ambiguity fail closed.
- Hardened provider failure cleanup, exact-origin validation, and target
  registry notifications so third-party failures cannot leak a connection or
  leave an orphaned stable target registration.

## 0.4.0 - 2026-08-19

- Reframed the Editor workflow as one minimalist connection workspace with a
  compact status, current backend host, human-readable expiry, and one
  contextual primary action.
- Moved exact routes, sign-in fields, manual token entry, and local storage
  behind collapsed disclosures, kept credential and manual forms mutually
  exclusive, and clear transient values on target or play-mode changes.
- Limited explicit server rechecks to inconclusive validation states and kept
  environment presentation neutral until projects supply explicit environment
  metadata in a future release.
- Added a pure, token-free presentation contract with deterministic coverage
  for status priority, action selection, disclosure defaults, and relative
  expiry labels.
- Distinguishes inspectable endpoint profiles from opaque custom providers and
  refreshes time-sensitive status copy while the window remains open.
- Binds each remembered token to its exact viewer independently of the visible
  viewer selection, and invalidates in-flight work when that context changes.

## 0.3.1 - 2026-08-19

- Added a prominent backend-target card that shows the active server origin
  and exact sign-in and token-check endpoints without clipping long URLs.
- Warned when acquisition and validation profiles target different backend
  origins, while keeping endpoint and environment semantics project-owned.
- Made the menu explicit that current endpoint profiles are fixed and that
  environment switching is not enabled yet.

## 0.3.0 - 2026-08-18

- Added an ephemeral window-owned Edit Mode authentication workspace that
  discovers the conventional acquisition profile without registering a fake
  live viewer target.
- Added an optional, backend-neutral validation provider contract and a second
  conventional bearer-authenticated validation endpoint profile.
- Added automatic validation on window open and focus, with a short duplicate
  request guard and honest server-verified, rejected, inconclusive, local-only,
  and unknown states.
- Reused Session API Integration's shared fractional JWT NumericDate resolver
  for pasted, remembered, and endpoint-acquired tokens.
- Hid the configuration selector for the normal single-viewer case and rebuilt
  the window around compact Deucarian status, acquisition, advanced, and local
  storage cards.
- Renamed the primary action to `Get New Token` because acquisition signs in
  again and does not imply that a refresh-token route exists.

## 0.2.0 - 2026-08-18

- Added backward-compatible interactive acquisition providers with
  credential-free input descriptors and short-lived input values.
- Added a generic endpoint acquisition provider backed by Session API
  Integration token-endpoint profiles and a conventional Resources resolver.
- Replaced separate acquisition and refresh actions with one `Refresh Token`
  action that reacquires through a provider when present or otherwise invokes
  the configured Session refresh service.
- Added masked endpoint input fields that release secret references on
  dispatch, completion, failure, cancellation, and window shutdown.
- Added an explicit Editor-only facade for importing legacy development tokens
  into ignored UserSettings and retrieving them by stable target id for local
  export workflows.

## 0.1.0 - 2026-08-18

- Added generic authentication session composition backed by Deucarian
  Session and Session API Integration.
- Added sanitized status snapshots, explicit target registration, optional
  acquisition providers, and token lifecycle command handlers.
- Added a Deucarian-styled local-only Editor authentication workflow.
- Made single-viewer projects recover their remembered target automatically
  when Unity assigns a new runtime identity between Play sessions.
