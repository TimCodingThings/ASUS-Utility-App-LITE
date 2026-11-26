ASUS Utility LITE 1.2.9

- Labels the highlighted performance profile Selected, clearly distinguishing the requested selection from actual firmware state.
- Adds a separate feedback row showing pending charger-change commands and accepted/failed profile writes with timestamps. Actual mode unavailable remains explicit.
- Accepted means the ASUS driver acknowledged a command; it is not independent confirmation of the active firmware mode. Saved button highlights are not valid evidence for charger-switch behavior.
- Keeps profile acknowledgement separate from unrelated automatic-setting failures and does not imply a command was sent merely because a saved selection exists.
- Replaces the misleading Settings restored message and records version, source, profile_write, and actual_mode=unverified in Power restore.log.
- Keeps the original window dimensions, vertical-only cooling view, and protected wheel input. Includes 164 offline checks and layout verification at 100%, 150%, and 200% DPI.

Exit the previous tray copy and reopen the installed executable to load this update. Reliable active-profile readback has not been established on the GA403UP; charger-switch profile persistence is not independently verified.

Retains fan-curve drafts, accepted session-setting replay, saved position, approved launcher, supplied logo, GPL-3.0 license, and G-Helper attribution. Independent app, not an official ASUS product.