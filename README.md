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

To uninstall, turn off **Start with Windows**, exit from the tray, and remove the published folder. User settings remain in `%LOCALAPPDATA%\Macaroni` until you remove them.

### Desktop acceptance checklist

- Num Lock on: digits type normally; dedicated navigation keys are unaffected.
- Num Lock off: 9 launches/focuses Terminal, 8 opens/reuses the home folder.
- Hold 9: only one action. Hold 0: only one toggle. Change Num Lock while holding a key and verify no stray navigation event.
- Disable with 0 or the tray: navigation works; 0 re-enables. Restart and confirm state is remembered.
- Move/minimize a target, press its mapping, and confirm placement and focus.
- Press a maximize mapping again while its window is already maximized on the target display: only focus changes, with no restore/maximize animation. Move it to another display and confirm the shortcut still brings it back.
- Test title/recent/dedicated modes with several windows. Test dedicated launch arguments that create a genuinely separate window.
- Disconnect monitors and test fallback, different DPI scales, and a laptop-only session.
- Edit config, save via an editor that replaces the file, and confirm reload. Introduce invalid JSON and confirm existing mappings still work.
- Test both command modes, missing executables, invalid paths, and application launch timeouts.
- Check folder matching with tabbed Explorer and focus behavior for elevated applications on the target Windows installation.
- Sign out/in and confirm startup; verify Exit removes the tray icon and hook.

## Overview

With Num Lock off, numpad 1–9 perform configurable actions. Numpad 0 enables/disables Macaroni. With Num Lock on, Macaroni leaves number-pad input unchanged. Only one instance runs per Windows session.

## Features

Open **configuration** from the tray to edit `%LOCALAPPDATA%\Macaroni\config.json`. It accepts JSON comments and trailing commas. Changes are watched and applied after a short debounce; invalid changes retain the last working configuration and show a notification. On first launch with invalid configuration, no action mappings are enabled until corrected.

See `config.example.json` for ready-to-use examples. Keys 1–9 are optional; unmapped keys retain normal behavior. Key 0 is always reserved while Num Lock is off. Modifier keys do not opt out of remapping. Physical numpad scan codes are used; injected input is ignored.

### Mapping fields

| Field | Meaning |
| --- | --- |
| `action` | `application`, `folder`, or `command` |
| `executable` | Executable path or name; required for applications and commands |
| `arguments` | Array of separate arguments, without shell escaping |
| `process` | Actual window-owning process name, e.g. `WindowsTerminal`; required for applications |
| `selection` | `recent` (default), `title`, or `dedicated` |
| `title` | Case-sensitive full window title for `title` selection |
| `path` | Existing filesystem directory for folder actions |
| `workingDirectory` | Optional launch working directory |
| `monitor` | Windows display number, default 1 |
| `size` | `maximize` (default) or `preserve` |
| `mode` | Command mode: `hidden` (default) or `terminal` |

Environment variables such as `%USERPROFILE%` expand in paths, executables, and arguments. Commands are executed directly; for shell syntax, explicitly configure `powershell.exe` or `cmd.exe` and their arguments. Hidden mode suppresses console creation; a graphical executable may still open its own UI. Commands execute once per press, not once per key-repeat.

Application actions reuse a suitable window or launch the configured executable. Exact-title matching is scoped to the process. Multiple matches use the most recently focused window since Macaroni started, with window enumeration order as the initial fallback.

Dedicated mode remembers a separate window for each mapping during this Macaroni session. Configure arguments that create a new window, e.g. `wt.exe` with `["-w", "new"]`. It intentionally fails with a timeout if an application only reuses an existing window. Restarting Macaroni or changing that mapping starts a new dedicated association; existing application windows are never closed.

Folder actions compare normalized filesystem paths against Explorer's Shell automation windows. Existing matching windows are reused. Hidden/inactive Explorer tabs vary by Windows version and are not guaranteed to be exposed or selected through this interface; use separate Explorer windows for reliable folder shortcuts.

Every window action restores a minimized window, centers it on the configured display, applies sizing, and requests focus. `preserve` uses the restored window dimensions, clamped to fit the destination work area. Display numbers come from Windows display device names, not array indexes. A missing target falls back to the closest lower number, or the lowest available number if none is lower.

For terminal commands, configure top-level `terminal`, `terminalProcess`, and `terminalArguments`. Defaults use Windows Terminal. Arguments must create a new window and accept an executable followed by arguments. Another terminal with different CLI conventions needs suitable arguments; its actual window process must match `terminalProcess`. Hidden commands have no placement step.

The tray supports enable/disable, configuration reload, startup registration, logs, and exit. Enabled state is stored separately from configuration. Launch failures, timeouts, invalid configuration, and focus errors appear as tray notifications and in `macaroni.log`.

Windows may deny focus or placement across privilege boundaries. Macaroni reports the failure; it does not elevate itself. Slow applications have a 15-second window-discovery timeout. Repeated presses while the same mapping is still launching are ignored.

## Architecture

- `Program.cs`: tray, single-instance lifetime, configuration watching, state persistence, startup registration.
- `Configuration.cs`: configuration model, validation, and monitor fallback policy.
- `KeyboardHook.cs` / `KeyPolicy.cs`: physical numpad interception and stable press/release suppression.
- `WindowActions.cs`: process-scoped window matching, foreground history, launch coordination, Explorer COM lookup, placement, and focus.
- `Native.cs`: Win32 API boundary.

The WinForms STA message loop owns hooks and Explorer COM access. Keyboard callbacks enqueue work; asynchronous discovery waits keep the message loop responsive. Configuration is replaced as a complete validated snapshot so an in-progress action keeps its original settings.
