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
- Test title/recent/dedicated modes with several windows. Dedicated mode should adopt an existing window, stay with it despite focus changes, and adopt another when it closes; launch only when none remain.
- Disconnect monitors and test fallback, different DPI scales, and a laptop-only session.
- Edit config, save via an editor that replaces the file, and confirm reload. Introduce invalid JSON and confirm existing mappings still work.
- Test both command modes, missing executables, invalid paths, and application launch timeouts.
- Check folder matching with tabbed Explorer and focus behavior for elevated applications on the target Windows installation.
- Sign out/in and confirm startup; verify Exit removes the tray icon and hook.

## Overview

With Num Lock off, numpad 1–9 perform configurable actions. Numpad 0 enables/disables Macaroni. With Num Lock on, Macaroni leaves number-pad input unchanged. Only one instance runs per Windows session.

## Features

Macaroni opens or focuses applications, reuses Explorer windows for folders, and runs commands either hidden or in a configurable terminal. Each shortcut chooses a monitor and window sizing behavior.

**See the [Configuration guide](CONFIGURATION.md) for every keyword, defaults, complete examples, window-selection strategies, monitor numbering, and troubleshooting.** The guide includes Windows Terminal, Chrome, ChatGPT, and tag-watch examples. The [example configuration](config.example.json) is the first-run template.

Right-click the tray icon and choose **Open configuration** to edit your active settings. Saved changes apply automatically; invalid edits keep the last working configuration. Numpad 0 or the tray toggles remapping, and Macaroni remembers that state across restarts.

The tray also provides manual configuration reload, monitor identity diagnostics, startup at sign-in, logs, and exit. Monitor numbers follow the current Windows display-path order automatically; optional physical-monitor overrides are documented in the configuration guide. A window already maximized on its configured monitor is simply focused without another maximize animation.

## Architecture

- `Program.cs`: tray, single-instance lifetime, configuration watching, state persistence, startup registration.
- `Configuration.cs`: configuration model, validation, and monitor fallback policy.
- `MonitorCatalog.cs` / `MonitorAssignments.cs`: physical display identities and explicit monitor-number assignments.
- `KeyboardHook.cs` / `KeyPolicy.cs`: physical numpad interception and stable press/release suppression.
- `WindowActions.cs`: process-scoped window matching, foreground history, launch coordination, Explorer COM lookup, placement, and focus.
- `Native.cs`: Win32 API boundary.

The WinForms STA message loop owns hooks and Explorer COM access. Keyboard callbacks enqueue work; asynchronous discovery waits keep the message loop responsive. Configuration is replaced as a complete validated snapshot so an in-progress action keeps its original settings.
