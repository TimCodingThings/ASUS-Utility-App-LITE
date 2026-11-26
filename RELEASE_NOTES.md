ASUS Utility LITE 1.2.8

- Restores the last selected performance mode after plugging in, unplugging, and resume, with a two-second settling delay.
- Coalesces repeated power notifications, ignores unknown readings, and does not rewrite profiles for ordinary battery updates.
- Restores only fan curves and power limits successfully applied in the current session after reapplying a profile; unapplied drafts are never replayed.
- Selecting a profile or restoring ASUS defaults clears prior session overrides. A newer user selection takes precedence over a pending restore.
- Restores configured charging and automatic GPU/display options independently, so an unrelated failure does not block the remaining controls.
- Reports background restore failures in the status line and bounded local Power restore.log instead of showing dialogs on power events.
- Includes 152 offline checks, including AC/DC transitions, resume, unchanged notifications, accepted-setting replay, failure isolation, and saved selection behavior.

Firmware restrictions still apply, including model-specific Turbo behavior on battery. The app remains a running tray utility; no service or driver is installed. Exit the previous tray copy and reopen the installed executable to use the update.

Retains the compact scrolling interface, fan-curve draft handling, saved position, approved launcher, supplied logo, GPL-3.0 license, and G-Helper attribution. Other laptop models and live hardware writes are not comprehensively validated. Independent app, not an official ASUS product.