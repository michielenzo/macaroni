# Macaroni

Deterministic Windows application shortcuts on your physical number pad.

## Table of Contents

- [Development](#development)
- [Overview](#overview)
- [Features](#features)
- [Architecture](#architecture)

## Development

Requires Windows and the .NET 10 SDK. No third-party packages are used.

```powershell
./build.ps1
./artifacts/Macaroni/Macaroni.exe
```

The build runs the dependency-free policy tests and publishes a framework-dependent Windows application. The target laptop needs the .NET 10 Desktop Runtime. Keep the entire published directory together, in a stable location. First launch registers that executable for startup at Windows sign-in; launching a different copy later does not silently replace that registration. Toggle **Start with Windows** off/on to register the current location.

To run the tests alone:

```powershell
dotnet run --project tests/Macaroni.Tests -- config.example.json
```

An optional desktop API smoke check installs a non-intercepting hook, enumerates windows/displays, and reads Explorer automation without changing startup settings or moving windows:

```powershell
dotnet run --project tests/Macaroni.WindowsSmoke
```

To additionally test real activation of an already-running Windows Terminal window, append `-- --focus-terminal`. This intentionally focuses Terminal. Activation verifies the actual foreground handle. If the normal request fails and no modifier is held, it retries after a balanced Ctrl-masked Alt tap to unlock foreground activation without opening an application menu. Injected events are ignored by Macaroni's keyboard hook.

If a grouped taskbar preview blocks activation, Macaroni briefly activates its taskbar owner so Windows dismisses the preview, then focuses the requested window. Recognition requires a known preview class on the taskbar's own process and UI thread. Recovery attempts and outcomes are always written to `macaroni.log`. New shortcuts, dedicated-assignment shortcuts, and enable/disable changes supersede pending focus recovery and final workspace presentation from older requests.

`-- --focus-taskbar-test` waits up to 60 seconds for you to open a grouped taskbar preview, dismisses it using the production focus code, and verifies focus on a temporary test window. No reply or extra shortcut is needed. `-- --focus-safety-test` checks that ordinary dialogs remain open, receive no Escape, and superseded requests stop before recovery. These tests close only their own temporary windows.

`-- --explorer-restore` opens four temporary Explorer folders and checks minimized restoration, repeated placement, window stacking above the previous application, and dedicated focus. It closes only its test windows afterward. `-- --workspace-test` uses temporary test forms to check placement, focus, and dedicated assignment transfers.

`-- --startup-layout-test` verifies delayed maximize correction and cancellation for manual movement, minimization, and a newer shortcut. `-- --postman-startup` requires Postman to be closed; it launches the installed app and verifies that the bottom-left layout survives startup. This leaves Postman open afterward.

To uninstall, turn off **Start with Windows**, exit from the tray, and remove the published folder. User settings remain in `%LOCALAPPDATA%\Macaroni` until you remove them.

### Continuous integration

The [GitHub Actions pipeline](.github/workflows/pipeline.yml) builds the Windows application and runs the automated policy/configuration checks on every push to `master`, using a Windows runner with .NET 10. A failed build or check fails the workflow. Results appear in the repository's [Actions tab](https://github.com/michielenzo/macaroni/actions). It becomes active once the workflow file is committed and pushed to `master`; no repository secrets are needed.

CI invokes the test executable with `dotnet run`, because this project does not use a `dotnet test` test framework. Interactive desktop smoke checks remain manual: they require real application windows, monitors, and a logged-in desktop.

### Desktop acceptance checklist

- Num Lock on: digits type normally; dedicated navigation keys are unaffected.
- Num Lock off: 9 launches/focuses Terminal, 8 opens/reuses the home folder.
- Hold 9: only one action. Hold Ctrl + numpad 0: only one toggle. Plain numpad 0 passes through. Change Num Lock or release Ctrl while holding a key and verify no stray navigation event.
- Disable with Ctrl + numpad 0 or the tray: navigation works; Ctrl + numpad 0 re-enables with Num Lock off. Restart and confirm state is remembered. Verify `macaroni.log` identifies each toggle source and the startup state.
- Move/minimize a target, press its mapping, and confirm placement and focus.
- Press a maximize mapping again while its window is already maximized on the target display: only focus changes, with no restore/maximize animation. Move it to another display and confirm the shortcut still brings it back.
- Test title/recent/dedicated modes with several windows. Dedicated mode should adopt an existing window, stay with it despite focus changes, and adopt another when it closes; launch only when none remain.
- Disconnect monitors and test fallback, different DPI scales, and a laptop-only session.
- Edit config, save via an editor that replaces the file, and confirm reload. Introduce invalid JSON and confirm existing mappings still work.
- Test both command modes, missing executables, invalid paths, and application launch timeouts.
- Check folder matching with tabbed Explorer and focus behavior for elevated applications on the target Windows installation.
- Sign out/in and confirm startup; verify Exit removes the tray icon and hook.

## Overview

With Num Lock off, numpad 1–9 perform configurable actions. Ctrl + numpad 0 enables/disables Macaroni; plain numpad 0 passes through normally. With Num Lock on, Macaroni leaves number-pad input unchanged. Only one instance runs per Windows session.

## Features

Macaroni opens or focuses applications, reuses Explorer windows for folders, and runs commands either hidden or in a configurable terminal. Each shortcut chooses a monitor and window sizing behavior.

Each key contains an `actions` array, so one press can restore a workspace with several windows. Actions start in parallel by default; use `execution: sequential` on a key when order matters. The action with `focus: true` receives focus as soon as its window is ready, with a final correction if a later launch steals it. Invalid or failed actions do not prevent valid siblings from running. Use dedicated selection for separate windows of the same application; duplicate recent actions on one key are rejected. Assignment pickers show ownership and transfers within the key. Optional action names make workspace assignment easier to recognize.

Application mappings can use a Start menu name such as `"startMenuName": "Google Chrome"` or `"startMenuName": "ChatGPT"`. **List applications** in the tray shows available names. Installed Chrome web apps retain their app/profile identity so their windows are distinguished from the browser.

Every action must declare `action`. Application mappings require exactly one launch source: `startMenuName`, or `executable` with `process`. Folder mappings require `path`; command mappings require `executable`. Invalid actions are skipped independently; duplicate key definitions disable the affected key. The former `app` keyword is now `startMenuName`.

Use `layout` to maximize, preserve normal dimensions, fill the left/right/top/bottom half, or occupy a corner quarter. Layouts respect the taskbar and skip resizing when the window already matches. This replaces the old `size` keyword.

**See the [Configuration guide](CONFIGURATION.md) for every keyword, defaults, complete examples, window-selection strategies, monitor numbering, and troubleshooting.** The guide includes Windows Terminal, Chrome, ChatGPT, and tag-watch examples. The [example configuration](config.example.json) is the first-run template.

Right-click the tray icon and choose **Open configuration** to edit your active settings. Saved changes apply automatically; notifications identify skipped mappings. Invalid JSON or shared settings retain the last working configuration. Ctrl + numpad 0 (Num Lock off) or the tray toggles remapping, and Macaroni remembers that state across restarts. Every state change and startup restoration is logged with its reason in `macaroni.log`; **Open logs** in the tray opens its folder.

To switch a dedicated window, focus the desired application and press **Ctrl + its numpad key** with Num Lock off. A single matching action assigns directly; multiple matches open a styled command palette centered on the source window's monitor. You can also use **Dedicated windows → key → action → Choose window…** in the tray. Assignment leaves windows in place; the next normal shortcut applies placement.

The tray also provides manual configuration reload, monitor identity diagnostics, startup at sign-in, logs, and exit. Monitor numbers follow the current Windows display-path order automatically; optional physical-monitor overrides are documented in the configuration guide. A window already maximized on its configured monitor is simply focused without another maximize animation.

## Architecture

- `Program.cs`: tray, single-instance lifetime, configuration watching, state persistence, startup registration.
- `Configuration.cs`: configuration model, validation, and monitor fallback policy.
- `MonitorCatalog.cs` / `MonitorAssignments.cs`: physical display identities and explicit monitor-number assignments.
- `KeyboardHook.cs` / `KeyPolicy.cs`: physical numpad interception and stable press/release suppression.
- `WindowActions.cs`: process-scoped window matching, foreground history, launch coordination, Explorer COM lookup, placement, and focus.
- `Native.cs`: Win32 API boundary.

The WinForms STA message loop owns hooks and Explorer COM access. Keyboard callbacks enqueue work; asynchronous discovery waits keep the message loop responsive. Configuration is replaced as a complete validated snapshot so an in-progress action keeps its original settings.
