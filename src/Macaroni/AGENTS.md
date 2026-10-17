# Macaroni application

The tray owns the WinForms message loop and application lifetime. Keyboard callbacks only decide whether to suppress input and queue actions; never perform I/O or wait in a callback. KeyPolicy maintains a press/release decision across state changes.

WindowActions runs on the UI STA thread because Explorer automation uses COM. Await window creation without blocking the message loop. Keep matching scoped to the configured process. Configuration replacements must be validated before becoming active.

Use Native for Win32 interop. Document API-specific behavior and keep pure decision logic independently testable.

Placement and activation are separate: bring every workspace window above ordinary application windows with SWP_NOACTIVATE, including unchanged layouts and already-maximized windows. Only the focus action requests keyboard focus. Desktop regression tests must cover both minimized windows and correctly placed windows obscured by another application.

AppCatalog reads desktop shortcuts and AppsFolder on a separate STA worker so discovery cannot stall keyboard hooks. Resolve named mappings before publishing a configuration snapshot, and reject outdated reload results. Launch desktop shortcuts through the shell to preserve Chrome profile/app arguments. WindowAppIdentity reads window-level IDs first and packaged process identity as fallback. Never replace strict app identity matching with a broad chrome.exe match.

Configuration.Load isolates mapping validation errors and records diagnostics while keeping other keys. Configuration.Parse is strict validation for individual mappings and tooling. ResolveMappings isolates Start menu failures per key too. Duplicate key definitions disable all entries for that key; never accept the last value. Invalid JSON and invalid shared settings retain the previously active configuration.

Mappings contain KeyBinding.Actions and optional Execution (parallel by default, sequential when requested). Isolate failures per action while preserving original Position for identity and labels. WorkspaceRunner overlaps asynchronous actions on the caller's STA. WindowClaims is a live exclusion view: claim windows during discovery, before awaiting or placing, so concurrent launchers cannot reuse a sibling's candidate. Focus the designated window immediately, correcting lost focus after startup only when necessary. Reject duplicate recent application targets per key, including aliases after catalog resolution. Dedicated identity includes the key and original action position; ownership and transfers are scoped to a key, never global. Assignment routing filters by window identity before showing the modeless palette. Show current ownership and explain transfer behavior in both pickers. Preserve source HWND/process/start-time snapshots and reject stale configuration selections. Keep palette painting lightweight and free of COM/catalog discovery.
