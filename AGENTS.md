# AGENTS.md

## Project

BabyToys is a Windows desktop child-mode utility for families with young children. Its goal is to make the computer quickly become uninteresting, reduce accidental keyboard and mouse input, and preserve a reliable recovery path for parents. It is not a security lock screen.

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
- All hooks, timers, windows, system-event subscriptions, and disposable services must have explicit, idempotent cleanup paths.

## Safety Boundaries

- Do not claim to block `Ctrl+Alt+Del`, hardware power buttons, UAC secure desktop, or every system shortcut.
- Preserve `Ctrl+Alt+U` long-press unlock and the Task Manager emergency recovery path before strengthening input blocking.
- Prefer sleep over shutdown to avoid data loss.
- A successful sleep request does not end child mode. After resume, re-establish display coverage, input hooks, and emergency recovery monitoring.
- If post-resume protection cannot be restored completely, clean up all resources and end child mode safely instead of leaving a partially locked state.

## Verification

Run on Windows with the .NET 10 SDK selected by `global.json`:

```powershell
dotnet restore BabyToys.Tests/BabyToys.Tests.csproj
dotnet build BabyToys.Tests/BabyToys.Tests.csproj -c Release --no-restore
dotnet test --project BabyToys.Tests/BabyToys.Tests.csproj -c Release --no-build
```

Changes involving hooks, overlays, sleep, startup, or the tray require Windows device verification. Cover multi-monitor behavior and display changes, normal key/mouse blocking, `Ctrl+Alt+U` long-press unlock, timeout sleep and resume protection, Task Manager emergency recovery, and complete cleanup after unlock or exit.

## CI and Releases

- Ordinary branch and pull-request CI must support changelog content that is still under `未发布`; do not require a finalized version section before release preparation.
- Tag releases must strictly match the project version and a non-empty versioned section in `CHANGELOG.md`.
- Public GitHub Release notes are for users. Include user-visible features, improvements, fixes, compatibility changes, and important limitations; omit CI, build scripts, refactoring, test infrastructure, and other internal engineering work unless it directly changes user behavior.
- Keep commit, push, CI verification, tag creation, and release verification as distinct steps. After release, download the remote assets and verify the SHA-256 file, ZIP contents, and packaged user README.
