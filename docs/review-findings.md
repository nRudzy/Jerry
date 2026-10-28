# Implementation review tracking

Review is against actual source and runtime tests, not a claim of macOS parity.

## Material findings addressed

- Root: close settings when minimize-to-tray disabled must exit cleanly.
- Root: newly enabled effect should enable master and set spotlight movement origin.
- Root: isolated tap rejects modifiers already held, clicks/scroll and other keys; independent tap deadline.
- Root: zoom duration/movement controls must drive real interpolation; loupe bounded by monitor.
- Root: shutdown waits for pending start cleanup; session-end finalization cannot block its own dispatcher.
- Root: focus re-enable restores same-window cutout; display/lock cancels pending capture.
- Root: service auto-stop updates HUD, tray and last completed output.
- Root: discovered settings ranges agree with UI; unsupported models removed from picker.
- Root/UI: missing sound, keystroke and locate shortcuts; highlighter color; smooth-caption option.
- Media owner: microphone playback disposition, finalized output and running-state events.
- Caption owner: explicit microphone selection, existing-document regroup/reset, word-level SlideUp, 1–3 lines.
- Integration QA: encoded control exclusion and alpha effects; real UI page render artifacts.

All listed source corrections are implemented. Verification includes 51 core
tests, 96 WPF/component assertions, seven lifecycle/render regression modes,
actual audio/video/camera/transcription, native hotkey registration and native
zoom/loupe state queries. Raw RED failures and corrected GREEN runs are retained.
Owned caption editors now close and cancel their active jobs before shutdown
waits for the shared recognition engine.

The review is not a certification of macOS parity. Physical input delivery,
mixed-display configurations, visible cursor hiding and long recordings remain
explicit verification limits in windows-verification.md.


## Minimal UI review — 28 October 2026

An independent source review found three interaction regressions, all fixed and
re-read before delivery:

- Reactive start/stop and draw/finish labels now follow controller state without
  rebuilding focused page controls. Accessible button names follow the label.
- Closing the cached tray flyout hides it; subsequent opens reuse a valid WPF
  window. Actual controller shutdown still destroys it and removes subscribers.
- Physical pointer bounds dismiss the flyout even when drawing suppresses the
  external mouse event, or the master switch is disabled. Deactivation and Escape
  also hide it.

Fresh verification: 52 Release Core tests, 2,156 native UI/component checks and
the custom-caption close lifecycle passed. The final full encoded fixture run
reached 93 checks then failed its included-color witness because the Windows
session was locked. The report does not claim that run passed; see
windows-verification.md for the evidence and remaining limits.


## Neon mascot / tray size — 28 October 2026

The regression first reproduced a 32 px NotifyIcon against Windows’ 24 px
small-icon metric, with a 22 px bright foreground. The dedicated tray image now
matches 24 px and fills 24 × 20; the application retains its separate 32 px
image. Both handles remain owned until the windows close. Native checks, the
full minimal UI suite and close-without-tray passed. Independent read-only
review reported no material findings. Mixed-scale taskbars and DPI changes
during a run remain unverified.
