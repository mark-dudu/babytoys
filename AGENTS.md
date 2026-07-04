# AGENTS.md

## Project

BabyToys is a Windows desktop child-mode utility. Its first version reduces accidental input and screen interest when a child hits the keyboard or mouse. It is not a security lock screen.

## Tech Stack

- C# with .NET 10.
- WPF for UI.
- Windows 10/11 target, with Windows 11 as the primary supported OS for new work.
- Win32 P/Invoke must stay inside service classes.
- No third-party dependencies unless the standard library and Win32 APIs clearly cannot satisfy the requirement.

## Code Standards

- Keep UI code in windows/views.
- Keep child-mode lifecycle and state transitions in session/coordinator classes.
- Keep input hooks, wallpaper lookup, power actions, settings, and logging in services.
- Use English for code identifiers.
- Use Chinese for user-facing UI text.
- Do not log concrete key values or typed content.
- All hooks, timers, and windows must have explicit cleanup paths.

## Safety Boundaries

- Do not claim to block `Ctrl+Alt+Del`, hardware power buttons, UAC secure desktop, or every system shortcut.
- Preserve a reliable family recovery path before strengthening input blocking.
- Prefer sleep over shutdown to avoid data loss.

## Verification

Run on Windows with .NET 10 SDK:

```powershell
dotnet build
dotnet run
```

Manual verification should include multi-monitor coverage, normal key/mouse blocking, `Ctrl+Alt+U` long-press unlock, timeout sleep request, and recovery after app exit.
