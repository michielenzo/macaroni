# Configuration guide

[Back to README](README.md)

Macaroni maps physical numpad keys to applications, folders, or commands. This guide explains every configuration setting and how to find the values for your own applications.

## Contents

- [Where to edit](#where-to-edit)
- [A complete example](#a-complete-example)
- [Multiple actions on one key](#multiple-actions-on-one-key)
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

Saved changes normally apply after about 350 ms without further file changes. No restart is needed. Invalid mappings are disabled individually; valid mappings still load. A notification and the log identify skipped keys. Invalid JSON or invalid shared settings retain the previous working configuration. You can also select **Reload configuration** in the tray.

## A complete example

This is a whole configuration file. Adjust the Chrome path and folder to match your installation. Replacing your whole file with this example also replaces your existing mappings; to add just one shortcut, copy its numbered entry into your existing `mappings` object instead.

```json
{
	"version": 1,
	"terminal": {
		"executable": "wt.exe",
		"process": "WindowsTerminal",
		"arguments": [
			"-w",
			"new"
		]
	},
	"mappings": {
		"4": {
			"actions": [
				{
					"action": "application",
					"executable": "C:/Program Files/Google/Chrome/Application/chrome.exe",
					"process": "chrome",
					"selection": "recent",
					"monitor": 2,
					"layout": "maximize",
					"focus": true
				}
			]
		},
		"7": {
			"actions": [
				{
					"action": "application",
					"executable": "wt.exe",
					"process": "WindowsTerminal",
					"arguments": [
						"-w",
						"new"
					],
					"selection": "dedicated",
					"monitor": 1,
					"layout": "maximize",
					"focus": true
				}
			]
		},
		"9": {
			"actions": [
				{
					"action": "folder",
					"path": "%USERPROFILE%/Documents",
					"monitor": 1,
					"layout": "preserve",
					"focus": true
				}
			]
		}
	}
}
```

Here, **4** opens or focuses the most recently used Chrome window on monitor 2, **7** adopts or launches a Terminal window and keeps it dedicated on monitor 1, and **9** opens or reuses Documents in Explorer.

## File format and keys

The file uses JSON, with two conveniences: `// line comments`, `/* block comments */`, and trailing commas are accepted. Use double quotes around property names and strings. Monitor numbers are numbers, not quoted strings. `arguments` is an array of strings, including when it contains just one argument.

Property names are case-insensitive, but use the spelling shown here. Option values such as `application`, `recent`, and `maximize` must be lowercase. Unknown property names are rejected, so a typo such as `monitro` prevents that edit from loading. Keep each property and mapping key unique. Use the documented types; do not use `null` as a substitute for leaving an optional setting out.

| State | Numpad 1–9 | Numpad 0 | Ctrl + numpad 0 |
| --- | --- | --- | --- |
| Num Lock on | Normal numeric input | Normal numeric input | Normal application behavior |
| Num Lock off, Macaroni enabled | Execute mapped actions; unmapped keys behave normally | Normal Insert behavior | Disable Macaroni |
| Num Lock off, Macaroni disabled | Normal navigation behavior | Normal Insert behavior | Enable Macaroni |

Only keys `"1"` through `"9"` can appear in `mappings`. Key `"0"` is reserved. Remove a numbered entry to unmap it; there is no per-mapping `enabled` setting. An empty `"mappings": {}` is valid.

The top-row number keys and separate navigation keys are not remapped. Holding a key triggers once per physical press. Ctrl + numpad 0 toggles Macaroni; plain numpad 0 never toggles it. Ctrl + a dedicated mapping assigns the focused window; Ctrl + other numpad keys passes through normally. Other modifiers do not disable remapping. Additional presses for a mapping whose window is still being found are ignored until that action finishes.

## Top-level settings

These belong outside `mappings`, next to it in the outermost object.

| Keyword | Type | Default if omitted | Purpose |
| --- | --- | --- | --- |
| `version` | Integer | `1` | Configuration format version. Only `1` is supported. |
| `startupTrackingSeconds` | Integer, `0`–`300` | `8` | Seconds to track newly launched windows for delayed startup layout changes. Applies to all keys. Set `0` to disable tracking, or increase it for slow-starting apps. Does not change the 15-second timeout for finding a window. |
| `mappings` | Object | `{}` | Numbered entries defining your shortcuts. |
| `monitors` | Object | `{}` | Optional physical-monitor overrides. Leave empty or omit to follow Windows Settings numbering automatically. |
| `terminal` | Object | Windows Terminal settings | Shared terminal window settings for commands with `"mode": "terminal"`. See [Choosing a terminal](#choosing-a-terminal) for its nested fields. |

The terminal settings affect **command actions in terminal mode only**. They do not change application mappings that launch `wt.exe`, or hidden commands.

## Mapping settings reference

Each numbered key must contain an `actions` array, even for one action: `"4": { "actions": [ ... ] }`. The key also accepts optional `execution`: `"parallel"` (default) or `"sequential"`. Each array entry is an independent action; the table below describes its fields.

| Keyword | Type / accepted values | Default | When to use it |
| --- | --- | --- | --- |
| `action` | `"application"`, `"folder"`, `"command"` | Required | Must be present in every mapping; there is no default. |
| `name` | Nonempty string | Generated | Optional human-readable base label for the assignment palette and tray picker. |
| `focus` | Boolean | `false` | `true` requests focus as soon as this window is ready. If a later launch steals focus, restore it after startup finishes. At most one valid action per key may have `focus: true`. Forbidden on hidden commands, including `false`. |
| `startMenuName` | String | Empty | Exact Start menu application name, ignoring capitalization. For application actions only. Infers launch target and window identity. |
| `executable` | String | Empty | Required for commands, and applications without `startMenuName`. Cannot be combined with `startMenuName`. Program to launch, without command-line arguments. |
| `process` | String | Empty | Required for applications without `startMenuName`. Process owning the window to find, usually without `.exe`. With `startMenuName`, overrides inferred process matching; Chrome/packaged app identity checks still apply. |
| `arguments` | Array of strings | `[]` | Launch arguments for applications and commands. |
| `launchAlways` | Boolean | `false` | For applications, invoke the executable and arguments on every press, then find and position its window. Useful for opening a specific file in an already-running editor. |
| `selection` | `"recent"`, `"title"`, `"dedicated"` | `"recent"` | How an application mapping selects its window. Multiple `recent` actions for the same application on one key are rejected; use `dedicated` for separate windows. |
| `title` | String | Empty | Required when an application uses `"selection": "title"`. Exact, case-sensitive window title. |
| `path` | String | Empty | Required for folders. Existing filesystem folder to open. |
| `workingDirectory` | String | Empty | Optional launch working directory for applications and commands. Empty leaves it unspecified. |
| `mode` | `"hidden"`, `"terminal"` | `"hidden"` when omitted | Only allowed with `"action": "command"`. `hidden` runs without creating a console window and rejects `monitor` and `layout`; `terminal` runs in the configured terminal and allows both. Other values and `null` are rejected, as is using `mode` on application or folder mappings. |
| `monitor` | Integer, at least `1` | `1` | Destination display for applications, folders, and commands with `"mode": "terminal"`. Rejected for hidden commands, including when `mode` is omitted. |
| `layout` | `"maximize"`, `"preserve"`, `"left"`, `"right"`, `"top"`, `"bottom"`, `"top-left"`, `"top-right"`, `"bottom-left"`, `"bottom-right"` | `"maximize"` | Window placement within the target monitor's working area. Allowed for applications, folders, and terminal commands; rejected for hidden commands. |

Configuration is validated strictly before any changes apply:

- Every key requires a nonempty `actions` array, and every action requires `action`.
- `application` requires exactly one of `startMenuName` or `executable`. Executable mappings also require `process`; Start menu mappings allow an optional nonempty process override.
- `folder` requires `path` and allows `action`, `path`, `monitor`, `layout`, `name`, and `focus`.
- `command` requires `executable` and allows `arguments`, `workingDirectory`, and `mode`. Only terminal commands allow `monitor` and `layout`.
- `selection`, `launchAlways`, and `process` are application-only settings. `title` is allowed only with `selection: title`. `mode` is command-only.
- Unknown fields, invalid values, null fields, conflicting launch sources, and duplicate properties are rejected. This includes multiple definitions of the same numpad key, even if their values are identical. Property-name duplicates are checked ignoring capitalization.

An invalid action disables only that entry; valid siblings on the same key and other keys still load. Malformed key structure and duplicate definitions disable the entire affected key. Multiple `focus: true` actions are all skipped with an error; unrelated non-focused actions still run. Fix and save to restore rejected entries. Duplicate properties within an action invalidate only that action.

## Multiple actions on one key

One press starts the actions in parallel, places each window when it becomes available, and focuses the one with `focus: true` as soon as it is ready:

```json
{
  "1": {
    "actions": [
      {
        "action": "application",
        "startMenuName": "Google Chrome",
        "name": "Browser",
        "selection": "dedicated",
        "monitor": 2,
        "layout": "left"
      },
      {
        "action": "application",
        "startMenuName": "Visual Studio Code",
        "name": "Editor",
        "selection": "dedicated",
        "monitor": 2,
        "layout": "right",
        "focus": true
      }
    ]
  }
}
```

Use names from **List applications** on your PC. With no `focus: true`, Macaroni makes no activation request. It preserves the foreground window during placement as far as Windows allows; launched applications can still activate themselves. Do not expect `focus: false` to suppress a launched program's own behavior. With a focus action, Macaroni checks again after all actions finish and restores that window's focus only if it was lost.

Parallel execution overlaps launch waits, including multiple windows of the same application. Windows are reserved immediately when discovered, so waiting siblings cannot claim the same window. Completion order is not guaranteed, and launching many applications together may briefly increase resource use. Programs that reuse a single window still need suitable new-window launch arguments when separate windows are required.

For actions that depend on each other, set `"execution": "sequential"` next to `"actions"` on that numbered key. This starts actions in array order and waits for each to finish before starting the next; a command's own process may continue running after launch.

Multiple application actions using `selection: recent` for the same application on one key are all disabled with diagnostics; valid siblings still run. Otherwise, focusing one changes the recent-window order and can swap layouts on the next press. Use `selection: dedicated` for stable separate windows. Start menu aliases are checked again after resolution, and distinct installed Chrome apps remain distinct applications. The restriction applies within each key only.

A failed launch or placement is reported and later actions still run. If the designated focus action fails, no other action is focused automatically. A second press while the same key is still discovering and initially placing windows is ignored. Once those actions finish, another press is accepted even if startup tracking continues; the newer run supersedes final focus and stacking from the older run. Distinct actions reuse distinct windows within the workspace; two actions for the same application may need new-window launch arguments so both layouts can be satisfied. Dedicated assignments are tracked per action, not just per key, and later actions' assigned windows are reserved so earlier actions cannot adopt them.

### Assignment palette and labels

For **Ctrl + numpad**, Macaroni first filters this key's dedicated actions by the source window's application, including Chrome app/profile identity:

- One matching action: assign immediately.
- Several matches: show a compact command palette centered on the source window's monitor. Choose with ↑/↓ and Enter, or click a row; Esc or the close button cancels.
- No matches: show an explanatory notification.

The palette snapshots the source window before opening. Selection validates that it still exists and the configuration has not changed; assignment does not move or resize it. It identifies the current owner and marks other choices as transfers. Assigning a window transfers any other dedicated assignment for that same window on the same key. On the next press, the previous owner adopts another available window or launches one; it cannot reclaim the transferred window. Assignments on other keys are independent and can share that window. **Tray → Dedicated windows → key → action → Choose window…** also marks windows as currently assigned here, unassigned, or a transfer from the named action. Selecting a transfer performs it immediately.

Labels use `name` when present; otherwise the Start menu name, executable filename without `.exe`, or folder name. Window actions append monitor and layout; commands also identify their mode, exact-title selection appends the configured title, and `focus: true` appends a focus marker. Identical labels receive their original action-position suffix (`#1`, `#2`, etc.). Long labels are shortened visually with the full text in a tooltip. The palette follows Windows light/dark preference using lightweight owner drawing, a shared system icon, and no blur or animation.

### Migrating an older configuration

Wrap each existing numbered entry in `"actions": [ ... ]`. To preserve its previous activation behavior, add `"focus": true` to its window-producing action. Do not add focus to hidden commands. The old flat per-key structure is rejected; Macaroni does not silently rewrite a user's file during reload. Reordering or changing dedicated actions can reset their session associations.

## Opening and focusing applications

An application action first looks for a suitable window. If it finds one, it places that window. Otherwise it launches the application and waits up to 15 seconds for a matching window. Only the action with `focus: true` requests focus, as soon as its window is ready.

Newly launched windows are observed after initial placement for the top-level `startupTrackingSeconds` duration (default `8`). For slower apps, put `"startupTrackingSeconds": 15` next to `"version"`, outside `mappings`. Some applications restore their saved size or maximize several seconds after their first window appears; Macaroni reapplies the layout if that happens. Observation runs asynchronously, so other actions and immediate focus continue normally. Normal shortcut presses remain available during observation, so closing an app and pressing the key again can reopen it immediately. Dedicated-window reassignment waits until the run finishes. Existing windows skip this extra observation. Manual move/resize, minimization, closing the window, or another shortcut placing it stops correction for that window. Changes to the setting apply to subsequent launches after configuration reload.

This handles delayed geometry changes on the same window, rather than relying on application-specific delays. Applications that replace a splash window with another window, change geometry after the observation period, enforce minimum dimensions, or require elevated permissions can still need separate handling. The guard records the dimensions the app actually accepts so it does not repeatedly resize an unchanged window that cannot fit the requested layout.

## Find applications by Start menu name

Use `startMenuName` to avoid looking up executable paths and process names:

```json
{
  "4": {
    "actions": [
      {
        "action": "application",
        "startMenuName": "Google Chrome",
        "selection": "recent",
        "monitor": 2,
        "layout": "maximize",
        "focus": true
      }
    ]
  },
  "8": {
    "actions": [
      {
        "action": "application",
        "startMenuName": "ChatGPT",
        "monitor": 1,
        "layout": "maximize",
        "focus": true
      }
    ]
  }
}
```

Right-click the tray and choose **List applications** to open `applications.json`, listing the discovered names and launch metadata. Use a complete `Name` value. Lookup ignores capitalization and surrounding spaces; it does not use substring matching, Windows Search ranking, websites, documents, or arbitrary aliases.

Macaroni reads executable shortcuts in your personal and shared Start menu Programs folders, plus packaged applications in Windows' AppsFolder. Names are resolved when configuration loads. After installing or renaming an app, use **Reload configuration**. An unknown name shows nearby names; distinct entries with the same name are rejected with their launch locations instead of choosing arbitrarily. Resolution errors disable only the affected key; other valid mappings still load. Use an explicit `executable`/`process` mapping without `startMenuName` to disambiguate.

### Chrome installed web apps

Chrome's **Install page as app** creates a shortcut containing its app ID and browser profile. Macaroni launches that shortcut to retain both, and matches the window's Windows app identity. For example, if your Start menu lists Gmail:

```json
{
  "2": {
    "actions": [
      {
        "action": "application",
        "startMenuName": "Gmail",
        "selection": "dedicated",
        "monitor": 2,
        "layout": "left",
        "focus": true
      }
    ]
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
    "actions": [
      {
        "action": "application",
        "executable": "C:/Program Files/Notepad\u002B\u002B/notepad\u002B\u002B.exe",
        "arguments": [
          "%LOCALAPPDATA%/Macaroni/config.json"
        ],
        "process": "notepad\u002B\u002B",
        "selection": "recent",
        "launchAlways": true,
        "monitor": 1,
        "layout": "maximize",
        "focus": true
      }
    ]
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
    "actions": [
      {
        "action": "application",
        "executable": "explorer.exe",
        "arguments": [
          "shell:AppsFolder\\OpenAI.Codex_2p2nqsd0c76g0!App"
        ],
        "process": "ChatGPT",
        "selection": "recent",
        "monitor": 1,
        "layout": "maximize",
        "focus": true
      }
    ]
  },
  "5": {
    "actions": [
      {
        "action": "application",
        "executable": "explorer.exe",
        "arguments": [
          "shell:AppsFolder\\TagWatch_qmzd5n1yyct6t!App"
        ],
        "process": "TagWatch",
        "selection": "recent",
        "monitor": 1,
        "layout": "maximize",
        "focus": true
      }
    ]
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
    "actions": [
      {
        "action": "application",
        "executable": "wt.exe",
        "process": "WindowsTerminal",
        "arguments": [
          "-w",
          "new",
          "new-tab",
          "--title",
          "Macaroni Terminal",
          "--suppressApplicationTitle"
        ],
        "selection": "title",
        "title": "Macaroni Terminal",
        "monitor": 1,
        "layout": "maximize",
        "focus": true
      }
    ]
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

You can also right-click the tray icon and select **Dedicated windows → key → action → Choose window…**. The picker lists matching windows and marks the current choice. Process IDs and window handles distinguish identical titles. Closed windows and configuration changes are checked again before an assignment is accepted. The picker is disabled while Macaroni is disabled. Choices have the same session lifetime as automatic dedicated associations; they are not saved to the configuration file. When Ctrl + numpad matches several dedicated actions, use the centered palette described above.

## Opening folders

Use `"action": "folder"` and an existing filesystem `path`:

```json
{
  "9": {
    "actions": [
      {
        "action": "folder",
        "path": "C:/dev",
        "monitor": 1,
        "layout": "maximize",
        "focus": true
      }
    ]
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
    "actions": [
      {
        "action": "command",
        "executable": "powershell.exe",
        "arguments": [
          "-NoProfile",
          "-Command",
          "Set-Content -LiteralPath $env:TEMP\\macaroni-example.txt -Value (Get-Date)"
        ],
        "mode": "hidden"
      }
    ]
  }
}
```

Hidden execution does not suppress windows created by a graphical application. Macaroni does not capture command output, wait for completion, or report nonzero exit codes. For a long-running hidden command, another press can start another copy. Use a terminal while troubleshooting, or have your script write its own log.

### `terminal`

Start a new configured terminal window and run the command inside it. For example:

```json
{
  "3": {
    "actions": [
      {
        "action": "command",
        "executable": "powershell.exe",
        "arguments": [
          "-NoExit",
          "-Command",
          "Get-Date"
        ],
        "mode": "terminal",
        "monitor": 2,
        "layout": "preserve",
        "focus": true
      }
    ]
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
    "actions": [
      {
        "action": "command",
        "executable": "powershell.exe",
        "arguments": [
          "-NoExit",
          "-Command",
          "Get-Date"
        ],
        "mode": "terminal",
        "monitor": 1,
        "layout": "maximize",
        "focus": true
      }
    ]
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
| `"maximize"` | Restore if needed, move to the target display, and maximize. If already maximized there, skip resizing. Final focus requires `focus: true`. |
| `"preserve"` | Use the restored window dimensions and center on the target display. Clamp dimensions to fit the display's working area. An already-maximized window is restored to its normal size. |
| `"left"` / `"right"` | Left or right half: half the available width and full available height. |
| `"top"` / `"bottom"` | Top or bottom half: full available width and half the available height. |
| `"top-left"` / `"top-right"` | Upper-left or upper-right quarter: half the available width and height. |
| `"bottom-left"` / `"bottom-right"` | Lower-left or lower-right quarter: half the available width and height. |

`preserve` preserves normal dimensions as far as they fit, not the previous position or maximized state. All layouts use the working area, excluding space reserved for the taskbar. Matching windows skip restoration, movement, and resizing. Every workspace window is brought forward, even when its layout already matches; only `focus: true` requests keyboard focus. Windows' invisible resize borders are accounted for so visible edges align. Applications that enforce their own minimum size may not fit a small half or quarter layout exactly.

For example, this places Chrome in the left half of monitor 2:

```json
{
  "4": {
    "actions": [
      {
        "action": "application",
        "executable": "C:/Program Files/Google/Chrome/Application/chrome.exe",
        "process": "chrome",
        "monitor": 2,
        "layout": "left",
        "focus": true
      }
    ]
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

- Validation is isolated per action. Valid actions replace the active settings; invalid actions are skipped. A key with no valid actions behaves normally. Notifications and logs identify the key and original action position of each skipped entry. Invalid group structure and duplicate key definitions disable the entire affected key.
- Configuration errors give the tray icon a red error symbol. Its tooltip shows enabled/disabled state plus the invalid keys (including duplicates), or “Invalid configuration file” if the file could not load. The error indicator remains when toggling Macaroni and clears after a reload with no configuration errors. Long key lists are shortened in the tooltip; the log contains full details.
- An in-progress action keeps the settings it started with. Later presses use the new configuration.
- The same per-key behavior applies at startup, including unknown or ambiguous Start menu names. Malformed JSON, invalid shared settings (`terminal`, `monitors`, version), or duplicate top-level properties prevent the file from loading: retain the previous configuration if running, or start with no action mappings if none has loaded yet. The previous valid configuration is retained only in memory, not as a persisted fallback.
- Disabling Macaroni pauses shortcuts but does not stop configuration reloads. Ctrl + numpad 0 (Num Lock off) and the tray can re-enable it.
- Enabled/disabled state is stored separately in `%LOCALAPPDATA%\Macaroni\enabled.txt` and remembered across restarts. There is no JSON keyword for it.
- Toggling with Ctrl + numpad 0 shows a notification stating whether Macaroni is enabled or disabled.
- Every enable/disable change is recorded in `macaroni.log` with a timestamp, process ID, previous state, new state, and reason: keyboard shortcut, tray menu, or tray icon double-click. Startup records whether the state was restored from `enabled.txt` or defaulted to enabled. This logging is always on, independent of `--diagnostics`.
- Automatic startup is controlled by **Start with Windows** in the tray, not by a JSON setting.
- **Exit** stops Macaroni completely; Ctrl + numpad 0 cannot restart an exited application.

## Troubleshooting

Use **Open logs** in the tray to open the settings folder, then read `macaroni.log`. Notifications summarize launch, configuration, placement, and focus errors.

When a grouped taskbar preview prevents an action with `focus: true` from activating, Macaroni briefly activates the preview's taskbar owner to dismiss it, then retries your application's focus. This applies only to recognized previews on the taskbar's process and UI thread; ordinary application dialogs are not dismissed. Held modifiers or mouse buttons delay recovery briefly. A newer shortcut or an enable/disable change supersedes older pending focus recovery. No configuration setting is needed.

Focus recovery always logs the target and foreground window classes, process/thread IDs, taskbar handoff, and success or failure in `macaroni.log`; `--diagnostics` is not required.

For troubleshooting, exit Macaroni and launch `Macaroni.exe --diagnostics`. This session additionally records selected window handles, minimized state, placement bounds, and foreground handles in `diagnostics.log`. It rotates at 256 KiB and retains one previous log. A normal restart disables this diagnostic mode.

| Symptom | What to check |
| --- | --- |
| A number types normally | Turn Num Lock off. |
| Navigation occurs instead of an action | Enable Macaroni with Ctrl + numpad 0 (Num Lock off) or the tray; check the key is mapped and the file loaded successfully. |
| Macaroni unexpectedly disabled | Read the latest `State:` entries in `macaroni.log` to identify the trigger and whether a restart restored a previously disabled state. Tray icon double-click also toggles Macaroni. |
| Saved edits have no effect | Edit the active `config.json`, check JSON syntax and keyword spelling, then use **Reload configuration**. |
| App runs but is never found | Verify the actual window-owning `process`, not just the launcher's name. |
| No matching window after 15 seconds | Check `process`, exact `title`, launch arguments, or whether the application starts too slowly. |
| Title mode keeps launching windows | The existing window title does not exactly match; changing tabs/documents may change it. |
| Dedicated mode opens another window | No eligible window was found for the configured process. Check `process`; after restart or closure, existing windows should be adopted. |
| Folder shortcut opens another Explorer window | Check the path; an existing match may be in an inaccessible inactive tab. |
| Window goes to another monitor | Check `windowsNumbers` in **Show monitor identities** and the fallback rule. Remove `monitors` overrides if you want automatic Windows numbering. |
| Window moves but does not focus | Try with modifier keys released; Windows can still restrict activation, including across privilege boundaries. |
| Focus fails while taskbar window previews are open | Recognized previews are dismissed automatically for `focus: true` actions. Release mouse buttons/modifiers and check `Taskbar preview:` / `Focus recovery:` entries in `macaroni.log` if it still fails. |
| Hidden command appears to do nothing | Run it in terminal mode and check shell/profile requirements. Macaroni does not collect command output. |
| A setting from another example is rejected | Only the keywords in this guide are supported; there is no `enabled`, `hotkey`, `screen`, or `fullscreen` field. |
