# Changelog

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

- Added generic viewer authentication session composition backed by Deucarian
  Session and Session API Integration.
- Added sanitized status snapshots, explicit target registration, optional
  acquisition providers, and token lifecycle command handlers.
- Added a Deucarian-styled local-only Editor authentication workflow.
- Made single-viewer projects recover their remembered target automatically
  when Unity assigns a new runtime identity between Play sessions.
