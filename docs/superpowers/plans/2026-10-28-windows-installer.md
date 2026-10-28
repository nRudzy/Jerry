# Jerry Windows Installer Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans; independent packaging work may use superpowers:dispatching-parallel-agents. Steps use checkboxes for tracking.

**Goal:** Deliver and publish a per-user French Windows installer that preserves Jerry's data and active work.

**Architecture:** Keep .NET 10/WPF. Share Windows startup and instance control with a small self-contained maintenance executable. Inno Setup packages the entire application and delegates graceful preparation and startup configuration to that executable.

**Tech Stack:** C#/.NET 10, WPF, named pipes, HKCU registry, Inno Setup, PowerShell, GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-10-28-installer-design.md`

## Global Constraints

- Jerry 0.2.0, Windows x64, French per-user installation, no elevation.
- Default `%LOCALAPPDATA%\Programs\Jerry`; retain `%LOCALAPPDATA%\Jerry` and all captures, sessions, models and tools.
- Existing instance mutex `Local\Jerry.CursorStudio`; all installation identities must be distinct from KOL.
- Helpers: `--prepare-update`, `--startup-status` (20 enabled / 0 disabled), `--configure-startup=enable|disable`.
- Never kill or interrupt a capture; destructive installation tests only on disposable hosted Windows CI.
- Public `nRudzy/Jerry`, integrate verified changes into main; publish installer, portable ZIP and SHA-256 sums.

## Review Focus

- Legacy open copies and capture/startup races must abort replacement without losing work.
- Windows and JSON startup choices may disagree; updates preserve Windows' actual enabled/disabled choice.
- Spaces, Unicode, UNC, mapped network drives and stale App Paths entries must select or reject the correct startup target.
- Uninstall cancellation must not touch the application or settings.
- New dependencies or app restarts during replacement must not leave a partial installation or an orphan Run entry.

### Task 1: Native startup and maintenance contract

**Files:** create `src/Jerry.Windows/`, `src/Jerry.Maintenance/`, `tests/Jerry.Windows.UnitTests/`; modify App.cs, AppController.cs and project references.

**Interfaces:** startup registry key `Software\Microsoft\Windows\CurrentVersion\App Paths\Jerry.exe`; maintenance mutex `Local\Jerry.CursorStudio.Maintenance`. Helper command contract from the spec. AppController.TryQuitForMaintenance() refuses active work and otherwise invokes normal shutdown.

- [x] Add failing startup target/preference tests and run them against the missing/current behavior.
- [x] Implement shared local-path/startup configuration and safe instance IPC, plus the autonomous helper.
- [x] Add Windows component regressions proving an active countdown/recording stays active after a refused request.
- [x] Verify unit tests and relevant Windows component cases; commit native integration.

### Task 2: Installer, packaging and disposable lifecycle verification

**Files:** create `installer/Jerry.iss`, `scripts/build-installer.ps1`, `scripts/test-installer.ps1`, `.github/workflows/ci.yml`; adapt `scripts/build-windows.ps1` and notices/version files.

**Interfaces:** consumes published `dist/windows-x64/` and self-contained helper; produces `dist/Jerry-Setup-0.2.0.exe`, `dist/Jerry-Windows-x64-0.2.0.zip`, `dist/SHA256SUMS.txt`.

- [x] Implement lifecycle test with hosted-runner and clean-profile guards; verify local refusal before any mutation.
- [x] Build the installer with current mascot, full payload, local destination and independent Jerry identifiers.
- [x] Exercise native install/update/uninstall, task merging, cancellation, refused maintenance, and retained fixture hashes on hosted Windows CI.
- [x] Verify full payload and checksums; fix observed failures and commit packaging.

### Task 3: Review, documentation and public release

**Files:** README.md, docs/windows-delivery.md and installation verification report.

- [x] Run complete Core/Windows unit suite and installer CI; independently review the branch and fix material findings.
- [ ] Document only actual verification, signature limitation and any deferred local installation.
- [ ] Integrate into main, publish version tag and verified release artifacts, verify remote hashes and clean only the merged working branch.
- [ ] Check local session state; install only if no Jerry instance is active. Otherwise leave installer ready and report the deferred local step.
