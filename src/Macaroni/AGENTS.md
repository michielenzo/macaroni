# Macaroni application

The tray owns the WinForms message loop and application lifetime. Keyboard callbacks only decide whether to suppress input and queue actions; never perform I/O or wait in a callback. KeyPolicy maintains a press/release decision across state changes.

WindowActions runs on the UI STA thread because Explorer automation uses COM. Await window creation without blocking the message loop. Keep matching scoped to the configured process. Configuration replacements must be validated before becoming active.

Use Native for Win32 interop. Document API-specific behavior and keep pure decision logic independently testable.
