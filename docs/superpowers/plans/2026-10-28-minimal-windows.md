# Minimal Windows Interface Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans, with independent floating surfaces delegated through dispatching-parallel-agents.

**Goal:** Apply the user's two minimal MOUZZ references to settings and everyday Windows controls.

**Architecture:** Keep WPF, the existing controller and all effects/media services. Share neutral theme resources and vector icons across settings, the tray flyout and the two floating bars.

**Tech Stack:** .NET 10, WPF, Windows notification icon.

**Spec:** `docs/superpowers/specs/2026-10-28-minimal-windows-design.md`.

## Global Constraints

Use the spec palette and native controls; preserve settings and all functions. No GitHub publication. Capture must exclude floating commands.

## Review Focus

- Small work areas: navigation/content scroll, all actions remain reachable.
- State changes by hotkey: visible switches and selected drawing tool update.
- Toggle clicks: the quick menu stays open and saves settings.
- Multi-monitor placement: clamp the menu and floating panels to their monitor.
- Shutdown: window subscriptions and hooks are released, recordings finalize.

### Task 1: Settings and shared primitives

**Files:** `UI/Theme.cs`, `UI/Icons.cs`, `UI/MainWindow.cs`.

- [x] Define neutral tokens, blue toggles, dark dropdowns, keyboard focus, compact row primitives.
- [x] Replace the large branded shell with a thin Windows header and fifteen direct settings pages. Preserve every existing setting and automatic save.
- [x] Inspect rendered pages at regular and small sizes against the reference.

### Task 2: Everyday controls

**Files:** `UI/QuickMenuWindow.cs`, `AppController.cs`, `UI/AnnotationToolbar.cs`, `UI/RecordingControlWindow.cs`.

**Interfaces:** `Icons.Create(string name, double size=18, Brush? brush=null)`; `AppController.ShowQuickMenu()`.

- [x] Add a tray flyout with real switches, external-state synchronization, outside/Escape close, capture exclusion and clamped placement.
- [x] Restyle annotation and recording controls, retaining no-activate behavior and lifecycle cleanup.
- [x] Verify actual controller interactions and capture affinity through the Windows harness.

### Task 3: Delivery

**Files:** `tests/Jerry.Windows.Tests`, verification/delivery documentation.

- [x] Run Core tests and real WPF component checks; inspect new settings/menu/bar screenshots.
- [x] Review changes, resolve material findings, publish and package the Windows build.
- [x] Install the new build alongside the current app, preserve settings and commit verified changes locally. Gabriel chose to keep the old instance open; launch of the new instance is deferred until he quits it.

The final encoded composition retest is recorded as environment-blocked by the Windows lock screen, not passed. UI component/lifecycle checks and Core tests passed.
