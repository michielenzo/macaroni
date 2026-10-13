# Configuration guide

[Back to README](README.md)

Macaroni maps physical numpad keys to applications, folders, or commands. This guide explains every configuration setting and how to find the values for your own applications.

## Contents

- [Where to edit](#where-to-edit)
- [A complete example](#a-complete-example)
- [File format and keys](#file-format-and-keys)
- [Top-level settings](#top-level-settings)
- [Mapping settings reference](#mapping-settings-reference)
- [Opening and focusing applications](#opening-and-focusing-applications)
- [Find applications by Start menu name](#find-applications-by-start-menu-name)
- [Choosing a window](#choosing-a-window)
- [Opening folders](#opening-folders)
- [Running commands](#running-commands)
- [Choosing a terminal](#choosing-a-terminal)
- [Monitors and window size](#monitors-and-window-size)
- [Paths, arguments, and environment variables](#paths-arguments-and-environment-variables)
- [Reloading and saved state](#reloading-and-saved-state)
- [Troubleshooting](#troubleshooting)

## Where to edit

1. Right-click Macaroni's system-tray icon. You may need to expand the tray's hidden icons first.
2. Select **Open configuration**.
3. Edit the file, save it, and try the mapped key with **Num Lock off**.

Your active file is `%LOCALAPPDATA%\Macaroni\config.json`. You can paste that path into File Explorer's address bar to open it. The repository's [config.example.json](config.example.json) is the template copied on first launch; editing the template does not change your active configuration.

Saved changes normally apply after about 350 ms without further file changes. No restart is needed. If an edit is invalid, Macaroni keeps the previous working configuration and shows a notification. You can also select **Reload configuration** in the tray.

## A complete example

This is a whole configuration file. Adjust the Chrome path and folder to match your installation. Replacing your whole file with this example also replaces your existing mappings; to add just one shortcut, copy its numbered entry into your existing `mappings` object instead.

```json
{
  "version": 1,
  "terminal": {
    "executable": "wt.exe",
    "process": "WindowsTerminal",
    "arguments": ["-w", "new"]
  },
  "mappings": {
    "4": {
      "action": "application",
      "executable": "C:/Program Files/Google/Chrome/Application/chrome.exe",
      "process": "chrome",
      "selection": "recent",
      "monitor": 2,
      "layout": "maximize"
    },
    "7": {
      "action": "application",
      "executable": "wt.exe",
      "process": "WindowsTerminal",
      "arguments": ["-w", "new"],
      "selection": "dedicated",
      "monitor": 1,
      "layout": "maximize"
    },
    "9": {
      "action": "folder",
      "path": "%USERPROFILE%/Documents",
      "monitor": 1,
      "layout": "preserve"
    }
  }
}
```

Here, **4** opens or focuses the most recently used Chrome window on monitor 2, **7** adopts or launches a Terminal window and keeps it dedicated on monitor 1, and **9** opens or reuses Documents in Explorer.

## File format and keys

The file uses JSON, with two conveniences: `// line comments`, `/* block comments */`, and trailing commas are accepted. Use double quotes around property names and strings. Monitor numbers are numbers, not quoted strings. `arguments` is an array of strings, including when it contains just one argument.

Property names are case-insensitive, but use the spelling shown here. Option values such as `application`, `recent`, and `maximize` must be lowercase. Unknown property names are rejected, so a typo such as `monitro` prevents that edit from loading. Keep each property and mapping key unique. Use the documented types; do not use `null` as a substitute for leaving an optional setting out.

| State | Numpad 1–9 | Numpad 0 |
| --- | --- | --- |
| Num Lock on | Normal numeric input | Normal numeric input |
| Num Lock off, Macaroni enabled | Execute mapped actions; unmapped keys behave normally | Disable Macaroni |
| Num Lock off, Macaroni disabled | Normal navigation behavior | Enable Macaroni |

Only keys `"1"` through `"9"` can appear in `mappings`. Key `"0"` is reserved. Remove a numbered entry to unmap it; there is no per-mapping `enabled` setting. An empty `"mappings": {}` is valid.

The top-row number keys and separate navigation keys are not remapped. Holding a key triggers once per physical press. Ctrl + a dedicated mapping assigns the focused window; Ctrl + other numpad keys passes through normally (including Ctrl + 0). Other modifiers do not disable remapping. Additional presses for a mapping whose window is still being found are ignored until that action finishes.

## Top-level settings

These belong outside `mappings`, next to it in the outermost object.

| Keyword | Type | Default if omitted | Purpose |
| --- | --- | --- | --- |
| `version` | Integer | `1` | Configuration format version. Only `1` is supported. |
| `mappings` | Object | `{}` | Numbered entries defining your shortcuts. |
| `monitors` | Object | `{}` | Optional physical-monitor overrides. Leave empty or omit to follow Windows Settings numbering automatically. |
| `terminal` | Object | Windows Terminal settings | Shared terminal window settings for commands with `"mode": "terminal"`. See [Choosing a terminal](#choosing-a-terminal) for its nested fields. |

The terminal settings affect **command actions in terminal mode only**. They do not change application mappings that launch `wt.exe`, or hidden commands.

## Mapping settings reference

These settings belong inside a numbered mapping, for example inside `"4": { ... }`.

| Keyword | Type / accepted values | Default | When to use it |
| --- | --- | --- | --- |
| `action` | `"application"`, `"folder"`, `"command"` | Required | Must be present in every mapping; there is no default. |
| `startMenuName` | String | Empty | Exact Start menu application name, ignoring capitalization. For application actions only. Infers launch target and window identity. |
| `executable` | String | Empty | Required for commands, and applications without `startMenuName`. Cannot be combined with `startMenuName`. Program to launch, without command-line arguments. |
| `process` | String | Empty | Required for applications without `startMenuName`. Process owning the window to find, usually without `.exe`. With `startMenuName`, overrides inferred process matching; Chrome/packaged app identity checks still apply. |
| `arguments` | Array of strings | `[]` | Launch arguments for applications and commands. |
| `launchAlways` | Boolean | `false` | For applications, invoke the executable and arguments on every press, then find and position its window. Useful for opening a specific file in an already-running editor. |
| `selection` | `"recent"`, `"title"`, `"dedicated"` | `"recent"` | How an application mapping selects its window. |
| `title` | String | Empty | Required when an application uses `"selection": "title"`. Exact, case-sensitive window title. |
| `path` | String | Empty | Required for folders. Existing filesystem folder to open. |
| `workingDirectory` | String | Empty | Optional launch working directory for applications and commands. Empty leaves it unspecified. |
| `mode` | `"hidden"`, `"terminal"` | `"hidden"` when omitted | Only allowed with `"action": "command"`. `hidden` runs without creating a console window and rejects `monitor` and `layout`; `terminal` runs in the configured terminal and allows both. Other values and `null` are rejected, as is using `mode` on application or folder mappings. |
| `monitor` | Integer, at least `1` | `1` | Destination display for applications, folders, and commands with `"mode": "terminal"`. Rejected for hidden commands, including when `mode` is omitted. |
| `layout` | `"maximize"`, `"preserve"`, `"left"`, `"right"`, `"top"`, `"bottom"`, `"top-left"`, `"top-right"`, `"bottom-left"`, `"bottom-right"` | `"maximize"` | Window placement within the target monitor's working area. Allowed for applications, folders, and terminal commands; rejected for hidden commands. |

Configuration is validated strictly before any changes apply:

- Every mapping requires `action`.
- `application` requires exactly one of `startMenuName` or `executable`. Executable mappings also require `process`; Start menu mappings allow an optional nonempty process override.
- `folder` requires `path` and only allows `action`, `path`, `monitor`, and `layout`.
- `command` requires `executable` and allows `arguments`, `workingDirectory`, and `mode`. Only terminal commands allow `monitor` and `layout`.
- `selection`, `launchAlways`, and `process` are application-only settings. `title` is allowed only with `selection: title`. `mode` is command-only.
- Unknown fields, invalid values, null fields, conflicting launch sources, and duplicate properties are rejected. This includes multiple definitions of the same numpad key, even if their values are identical. Property-name duplicates are checked ignoring capitalization.

One invalid mapping rejects the entire edit, preserving the active configuration. For older files, rename `app` to `startMenuName`, add missing `action` settings, remove unused titles from dedicated mappings, and remove window-placement fields from hidden commands.

## Opening and focusing applications

An application mapping first looks for a suitable window. If it finds one, it places and focuses that window. Otherwise it launches `executable` with `arguments`, then waits up to 15 seconds for a matching window.

## Find applications by Start menu name

Use `startMenuName` to avoid looking up executable paths and process names:

```json
{
  "4": {
    "action": "application",
    "startMenuName": "Google Chrome",
    "selection": "recent",
    "monitor": 2,
    "layout": "maximize"
  },
  "8": {
    "action": "application",
    "startMenuName": "ChatGPT",
    "monitor": 1,
    "layout": "maximize"
  }
}
```

Right-click the tray and choose **List applications** to open `applications.json`, listing the discovered names and launch metadata. Use a complete `Name` value. Lookup ignores capitalization and surrounding spaces; it does not use substring matching, Windows Search ranking, websites, documents, or arbitrary aliases.

Macaroni reads executable shortcuts in your personal and shared Start menu Programs folders, plus packaged applications in Windows' AppsFolder. Names are resolved when configuration loads. After installing or renaming an app, use **Reload configuration**. An unknown name shows nearby names; distinct entries with the same name are rejected with their launch locations instead of choosing arbitrarily. Resolution errors retain the last active configuration, just like invalid JSON. Use an explicit `executable`/`process` mapping without `startMenuName` to disambiguate.

### Chrome installed web apps

Chrome's **Install page as app** creates a shortcut containing its app ID and browser profile. Macaroni launches that shortcut to retain both, and matches the window's Windows app identity. For example, if your Start menu lists Gmail:

```json
{
  "2": {
    "action": "application",
    "startMenuName": "Gmail",
    "selection": "dedicated",
    "monitor": 2,
    "layout": "left"
  }
}
```

It matches that installed Gmail app and profile, rather than any window owned by `chrome.exe`. A missing or mismatched app identity does not fall back to an unrelated Chrome window. The normal `Google Chrome` name excludes installed Chrome app windows. If identical names exist under multiple profiles, use explicit configuration to disambiguate or give the shortcuts distinct Start menu names.

All selection modes, dedicated-window reassignment, `monitor`, and `layout` work with `startMenuName`. `title` adds an exact title condition after identifying the app. Shortcut launch arguments and working directories are preserved when launching by name. Configured `arguments` are additional arguments. For packaged apps that need custom executable arguments, use a separate explicit `executable`/`process` mapping instead of `startMenuName`.

Ordinary desktop shortcuts infer the window process from their target executable; common aliases such as `wt.exe` and `chrome_proxy.exe` are accounted for. Unusual launchers that create windows in another process may need a `process` override. Packaged apps and Chrome installed apps use their app identity instead. Existing explicit mappings continue to work and need not be converted.

### Explicit application mappings

### Executable versus process

These have different jobs:

- **`executable`** tells Windows what to launch when needed. It can be a full path or a command Windows can resolve, such as `wt.exe`.
- **`process`** tells Macaroni which running application's windows to look for. It is matched case-insensitively. A trailing `.exe` is accepted, but the examples omit it.

They can differ: `wt.exe` launches a window owned by `WindowsTerminal`. A packaged app can be launched through `explorer.exe`, while its actual window belongs to `ChatGPT` or `TagWatch`.

### Open a file and position its editor

An application mapping normally sends `arguments` only when no matching window exists. Set `launchAlways` to `true` to send them every time, then apply the usual window selection, monitor, and layout. Editors such as Notepad++ normally forward the file request to an existing instance. An application that creates a new window for every invocation may still do so; this option does not force single-instance behavior. If a matching window existed, Macaroni waits up to two seconds for a short-lived launcher to hand off before selecting the window.

```json
{
  "3": {
    "action": "application",
    "executable": "C:/Program Files/Notepad++/notepad++.exe",
    "arguments": ["%LOCALAPPDATA%/Macaroni/config.json"],
    "process": "notepad++",
    "selection": "recent",
    "launchAlways": true,
    "monitor": 1,
    "layout": "maximize"
  }
}
```

This sends the file request on each press, including when another tab is selected, then maximizes the selected Notepad++ window on monitor 1. Use this application mode for editor placement; `command` with `mode: hidden` does not apply `monitor` or `layout`.

To find an application's process name:

1. Open the application and Task Manager (**Ctrl + Shift + Esc**).
2. Under **Processes**, right-click the application and choose **Go to details**.
3. Use the highlighted executable's name for `process`, normally dropping `.exe`.
4. Right-click that entry and choose **Open file location** to find its executable path.

Do not assume the application's display name is its process name. For applications with launchers or helper processes, use the process that owns the visible window.

### Packaged applications, including ChatGPT and tag-watch

An installed app ID avoids hard-coding a versioned `WindowsApps` directory. In PowerShell, list matching Start menu entries with:

```powershell
Get-StartApps | Where-Object Name -match 'ChatGPT|Codex|tag-watch'
```

Use the returned **AppID** after `shell:AppsFolder\` in the launch argument. The IDs below were verified on the development PC; another installation may have different IDs. These examples are numbered entries to merge into your existing `mappings` object:

```json
{
  "8": {
    "action": "application",
    "executable": "explorer.exe",
    "arguments": ["shell:AppsFolder\\OpenAI.Codex_2p2nqsd0c76g0!App"],
    "process": "ChatGPT",
    "selection": "recent",
    "monitor": 1,
    "layout": "maximize"
  },
  "5": {
    "action": "application",
    "executable": "explorer.exe",
    "arguments": ["shell:AppsFolder\\TagWatch_qmzd5n1yyct6t!App"],
    "process": "TagWatch",
    "selection": "recent",
    "monitor": 1,
    "layout": "maximize"
  }
}
```

Here, `explorer.exe` is only the launcher. Macaroni still searches for the configured application's window.

## Choosing a window

### `recent`

Reuse the most recently focused window belonging to `process`. If no suitable window exists, launch one. Macaroni tracks foreground changes while it runs; it does not recover focus history from before startup. Windows without recorded history fall back to window enumeration order.

Use this for apps such as Chrome or ChatGPT when any recently used window is acceptable. It selects a window, not a particular browser tab or profile.

### `title`

Require both the configured process and the **complete, case-sensitive title**. Substrings, wildcards, and regular expressions are not supported. If several windows match, use the most recently focused matching window.

`title` is a search condition, not an instruction to rename a window. Launch arguments must make the application produce that title, or Macaroni will time out even if it successfully launched the program. Titles that change with a document or current terminal command can stop matching.

For Windows Terminal, this example requests a fixed tab title and suppresses application-driven title changes:

```json
{
  "7": {
    "action": "application",
    "executable": "wt.exe",
    "process": "WindowsTerminal",
    "arguments": ["-w", "new", "new-tab", "--title", "Macaroni Terminal", "--suppressApplicationTitle"],
    "selection": "title",
    "title": "Macaroni Terminal",
    "monitor": 1,
    "layout": "maximize"
  }
}
```

Terminal settings or switching tabs may affect the window's displayed title. Check that the actual window title matches what you configured.

### `dedicated`

Keep using one chosen window until it closes. On first use, or if the tracked window has closed, adopt an existing window belonging to the configured process. If only one exists, use it; if several exist, choose the most recently focused one. When no focus history is available, window enumeration order is the fallback. Launch a new window only if no suitable existing window is available.

Once adopted, that window remains dedicated even if you focus another window of the same application. Launch arguments such as `["-w", "new"]` for Windows Terminal are used only when a launch is necessary. Do not set `title` in this mode; matching uses the configured process or Start menu application identity.

The association is kept in memory for the current Macaroni session. After restarting Macaroni or changing the mapping, the next press adopts an available window again. Existing application windows are never closed by Macaroni.

#### Choose a different dedicated window

1. Focus the application window you want to use, for example a particular terminal.
2. With Num Lock off and Macaroni enabled, press **Ctrl + numpad 7** (replace 7 with your dedicated mapping's key).
3. A notification confirms which window was assigned. Future presses of **numpad 7** use that window.

Assignment does not move, resize, or close any window. Placement happens on the next normal shortcut press. The focused window must belong to the mapping's configured `process`; otherwise Macaroni reports the mismatch and retains the previous assignment. This shortcut only captures Ctrl combinations for dedicated application mappings. With Num Lock on or Macaroni disabled, it passes through normally. Assignment is temporarily unavailable while that mapping is opening or placing a window.

You can also right-click the tray icon and select **Dedicated windows → key and current title → Choose window…**. The picker lists matching windows and marks the current choice. Process IDs and window handles distinguish identical titles. Closed windows and configuration changes are checked again before an assignment is accepted. The picker is disabled while Macaroni is disabled. Choices have the same session lifetime as automatic dedicated associations; they are not saved to the configuration file.

## Opening folders

Use `"action": "folder"` and an existing filesystem `path`:

```json
{
  "9": {
    "action": "folder",
    "path": "C:/dev",
    "monitor": 1,
    "layout": "maximize"
  }
}
```

Macaroni looks for an Explorer window already showing the same normalized path. Comparison ignores case and trailing backslashes. It reuses a matching window, preferring one it has seen focused recently; otherwise it opens Explorer at that folder. It then applies monitor placement and sizing.

Use a normal folder path, not a website or a virtual Explorer location such as “This PC.” The folder must already exist. Different paths to the same underlying folder, such as a symbolic link and its target, are not necessarily treated as identical.

Inactive Explorer tabs are a limitation: Windows may not expose them through the automation interface, and Macaroni cannot guarantee selecting a matching inactive tab. Separate Explorer windows provide more predictable behavior.

## Running commands

A command mapping runs the command on each separate press. It does not search for or reuse an existing command process.

### `hidden`

Run without creating a console window. For example, this writes the current date to a file in your temporary directory:

```json
{
  "6": {
    "action": "command",
    "executable": "powershell.exe",
    "arguments": ["-NoProfile", "-Command", "Set-Content -LiteralPath $env:TEMP\\macaroni-example.txt -Value (Get-Date)"],
    "mode": "hidden"
  }
}
```

Hidden execution does not suppress windows created by a graphical application. Macaroni does not capture command output, wait for completion, or report nonzero exit codes. For a long-running hidden command, another press can start another copy. Use a terminal while troubleshooting, or have your script write its own log.

### `terminal`

Start a new configured terminal window and run the command inside it. For example:

```json
{
  "3": {
    "action": "command",
    "executable": "powershell.exe",
    "arguments": ["-NoExit", "-Command", "Get-Date"],
    "mode": "terminal",
    "monitor": 2,
    "layout": "preserve"
  }
}
```

`-NoExit` is a PowerShell option that keeps this example open so you can see its output. It is not a Macaroni keyword. A command that immediately closes its terminal may disappear before Macaroni can place it.

Shell features such as pipes, redirection, aliases, and functions require a shell executable. Put a PowerShell expression in one argument after `-Command`, or use `cmd.exe` with `/c`. An executable value such as `"open aumc"` does not mean “run this shell command.” A function such as `open` must exist in the shell/profile you launch; `-NoProfile` skips profile-defined functions.

## Choosing a terminal

The top-level `terminal` object chooses the **window application that displays a command**. A command mapping's `executable` chooses **the program to run inside that window**. These settings describe two different programs.

For example, Windows Terminal (`wt.exe`) provides the window; PowerShell (`powershell.exe`) runs inside it and executes `Get-Date`. PowerShell is a shell, while Windows Terminal hosts shells and other console programs.

The default terminal object is:

```json
{
  "terminal": {
    "executable": "wt.exe",
    "process": "WindowsTerminal",
    "arguments": ["-w", "new"]
  }
}
```

Put this object at the top level, alongside `version` and `mappings`. Omitting the object or any of its fields uses the defaults below. All terminal-mode commands share it.

| Field inside `terminal` | Type | Default | Purpose |
| --- | --- | --- | --- |
| `executable` | String | `"wt.exe"` | Terminal window application to launch. |
| `process` | String | `"WindowsTerminal"` | Process owning the terminal window, so Macaroni can find, place, and focus it. This can differ from the launcher filename. |
| `arguments` | Array of strings | `["-w", "new"]` | Options for the terminal window application. The defaults ask Windows Terminal to create a new window. |

By comparison, this numbered mapping defines the **command inside the terminal**:

```json
{
  "3": {
    "action": "command",
    "executable": "powershell.exe",
    "arguments": ["-NoExit", "-Command", "Get-Date"],
    "mode": "terminal",
    "monitor": 1,
    "layout": "maximize"
  }
}
```

`terminal.arguments` controls Windows Terminal; the mapping's `arguments` controls PowerShell. Macaroni constructs the launch in this order:

```text
terminal.executable + terminal.arguments + mapping.executable + mapping.arguments
```

For the `Get-Date` example, the individual arguments are equivalent to:

```powershell
wt.exe -w new powershell.exe -NoExit -Command Get-Date
```

To use another terminal, configure its executable, the process owning its window, and any arguments needed to create a new window and accept a command executable followed by arguments. Macaroni does not automatically translate between different terminals' command-line conventions. The `workingDirectory` setting is passed to the launched process; a terminal or shell may override it with its profile settings.

The `terminal` object is not used by hidden commands or by application mappings that directly open a terminal. An application mapping for your usual terminal still declares its own `startMenuName` or `executable`/`process` as usual. There is no per-mapping terminal override.

Older configurations must move the old top-level `terminal` string into `terminal.executable`, `terminalProcess` into `terminal.process`, and `terminalArguments` into `terminal.arguments`. The old flat format is rejected. Nested unknown fields, duplicates, null values, and empty executable/process values are also rejected.

## Monitors and window size

### `monitor`

Supply a positive integer. The default is `1`.

By default, `monitor` follows Windows' active display-path numbering, using the same approach documented by [Microsoft PowerToys Power Display](https://github.com/microsoft/PowerToys/blob/main/doc/devdocs/modules/powerdisplay/design.md#monitor-identification-handles-ids-and-names). Macaroni reads the current topology every time a shortcut runs. It does not infer display numbers from screen position, primary status, or the suffix of a `DISPLAY` device name.

Leave the top-level `monitors` setting empty or omit it for this behavior. Rearranging a display from right to left changes its position, not the meaning of the configured number. If Windows changes its display-path numbering after a connection change, the next shortcut uses that new numbering. The current three-display setup has been verified against Windows Settings; mirrored displays share a desktop area and cannot act as independent window destinations.

Use **Show monitor identities** in the tray to inspect `windowsNumbers`, device names, current screen positions, and physical identities. If the requested number is unavailable, Macaroni uses the closest lower available number, or the lowest available number if none is lower. A failed topology query reports an error instead of guessing from internal device names.

#### Optional physical-monitor overrides

Only configure `monitors` if you intentionally want a number to stay attached to a particular physical monitor instead of following Windows numbering. A nonempty object switches selection to these explicit assignments. To set them:

1. Use **Show monitor identities** in the tray. It opens a diagnostic `monitors.json` containing each screen's device name, position (`bounds`), primary status, and identity. This report does not change configuration.
2. Match each screen's position to the layout in Windows Display Settings. Use **Identify** there if needed.
3. Copy each complete `identity` JSON string, including its escaped backslashes, into a top-level `monitors` object in your active `config.json`. Choose the numbers to correspond to your Windows Settings layout.

This illustrates the structure; replace the placeholder strings with identities from your report:

```json
{
  "monitors": {
    "1": "COPY-LAPTOP-IDENTITY-HERE",
    "2": "COPY-SECOND-DISPLAY-IDENTITY-HERE",
    "3": "COPY-THIRD-DISPLAY-IDENTITY-HERE"
  }
}
```

Each number must be a positive integer written as a string, and each identity can have only one number. Include all displays you want to use as destinations or fallbacks. Identities are matched without case sensitivity. These are explicit assignments, not an automatic lookup of Windows Settings' labels. A different connection port, driver change, or replacement display may change an identity; regenerate the report and update the assignment if necessary.

With assignments configured, only connected, assigned displays participate in selection. If the requested display is missing, Macaroni chooses the closest lower available assigned number. If none is lower, it uses the lowest available assigned number:

| Available numbers | Requested | Used |
| --- | --- | --- |
| 1, 2, 3 | 2 | 2 |
| 1, 3 | 2 | 1 |
| 1, 3, 5 | 4 | 3 |
| 3, 5 | 1 | 3 |

Connected displays are checked when the shortcut runs, so fallback also applies after disconnecting a monitor. If no assigned display is connected, Macaroni reports an error instead of moving a window to an arbitrary destination.

Earlier builds used the numeric suffix of a GDI device name or required manual assignments. The default now uses live Windows display-path numbering. Remove an old `monitors` object to use the automatic behavior.

### `layout`

This setting was previously named `size`. Rename that key to `layout` in older configurations; the old keyword is no longer accepted. The existing values `maximize` and `preserve` still work.

| Value | Behavior |
| --- | --- |
| `"maximize"` | Restore if needed, move to the target display, maximize, and focus. If already maximized on that display, only focus it; no repeated maximize animation. |
| `"preserve"` | Use the restored window dimensions, center on the target display, and focus. Clamp dimensions to fit the display's working area. An already-maximized window is restored to its normal size. |
| `"left"` / `"right"` | Left or right half: half the available width and full available height. |
| `"top"` / `"bottom"` | Top or bottom half: full available width and half the available height. |
| `"top-left"` / `"top-right"` | Upper-left or upper-right quarter: half the available width and height. |
| `"bottom-left"` / `"bottom-right"` | Lower-left or lower-right quarter: half the available width and height. |

`preserve` preserves normal dimensions as far as they fit, not the previous position or maximized state. All layouts use the working area, excluding space reserved for the taskbar. Matching windows are only focused, without another restore, move, or resize. Windows' invisible resize borders are accounted for so visible edges align. Applications that enforce their own minimum size may not fit a small half or quarter layout exactly.

For example, this places Chrome in the left half of monitor 2:

```json
{
  "4": {
    "action": "application",
    "executable": "C:/Program Files/Google/Chrome/Application/chrome.exe",
    "process": "chrome",
    "monitor": 2,
    "layout": "left"
  }
}
```

Layouts work for application mappings, folders, and terminal-mode commands. Hidden commands have no window to arrange. These are direct window placements, not Windows Snap groups. Fixed coordinates, explicit width/height, fullscreen, and minimize actions are not configurable.

## Paths, arguments, and environment variables

Use either forward slashes or escaped backslashes in JSON paths:

- `"C:/dev/projects"`
- `"C:\\dev\\projects"`

A single backslash in a JSON string starts an escape. For example, `\t` means a tab, so writing an unescaped Windows path can produce an error or an unintended path.

Use a full executable path when the program is not discoverable by name. Do not add extra quote characters around a path with spaces inside the `executable` value; JSON's outer quotes are enough.

Each `arguments` array element is one argument. Macaroni handles process argument quoting. For example, use `["--profile-directory=Default", "https://example.com"]`, not one string containing both arguments. When launching an application through a shell, the shell's own syntax still applies inside its command argument.

Macaroni expands `%VARIABLE%` environment references in `executable`, each argument, folder `path`, `workingDirectory`, `terminal.executable`, and `terminal.arguments`. Examples include `%USERPROFILE%`, `%LOCALAPPDATA%`, and `%TEMP%`. It does not expand them in `title` or `process`. `$env:TEMP` in a PowerShell command is interpreted by PowerShell, not Macaroni. `~` is not a Macaroni home-directory shorthand.

Prefer absolute folder and working-directory paths. Relative paths depend on Macaroni's launch environment, not the configuration file's directory. Setting `workingDirectory` affects a newly launched process; it does not change an already-running application's current folder.

## Reloading and saved state

- The entire file is validated before it replaces the active settings. One invalid mapping prevents all changes in that save from taking effect.
- An in-progress action keeps the settings it started with. Later presses use the new configuration.
- Invalid configuration at startup leaves no action mappings active until corrected. The previous valid configuration is retained only in memory during a running session, not as a persisted fallback.
- Disabling Macaroni pauses shortcuts but does not stop configuration reloads. Key 0 and the tray can re-enable it.
- Enabled/disabled state is stored separately in `%LOCALAPPDATA%\Macaroni\enabled.txt` and remembered across restarts. There is no JSON keyword for it.
- Automatic startup is controlled by **Start with Windows** in the tray, not by a JSON setting.
- **Exit** stops Macaroni completely; numpad 0 cannot restart an exited application.

## Troubleshooting

Use **Open logs** in the tray to open the settings folder, then read `macaroni.log`. Notifications summarize launch, configuration, placement, and focus errors.

| Symptom | What to check |
| --- | --- |
| A number types normally | Turn Num Lock off. |
| Navigation occurs instead of an action | Enable Macaroni with numpad 0 or the tray; check the key is mapped and the file loaded successfully. |
| Saved edits have no effect | Edit the active `config.json`, check JSON syntax and keyword spelling, then use **Reload configuration**. |
| App runs but is never found | Verify the actual window-owning `process`, not just the launcher's name. |
| No matching window after 15 seconds | Check `process`, exact `title`, launch arguments, or whether the application starts too slowly. |
| Title mode keeps launching windows | The existing window title does not exactly match; changing tabs/documents may change it. |
| Dedicated mode opens another window | No eligible window was found for the configured process. Check `process`; after restart or closure, existing windows should be adopted. |
| Folder shortcut opens another Explorer window | Check the path; an existing match may be in an inaccessible inactive tab. |
| Window goes to another monitor | Check `windowsNumbers` in **Show monitor identities** and the fallback rule. Remove `monitors` overrides if you want automatic Windows numbering. |
| Window moves but does not focus | Try with modifier keys released; Windows can still restrict activation, including across privilege boundaries. |
| Hidden command appears to do nothing | Run it in terminal mode and check shell/profile requirements. Macaroni does not collect command output. |
| A setting from another example is rejected | Only the keywords in this guide are supported; there is no `enabled`, `hotkey`, `screen`, or `fullscreen` field. |
