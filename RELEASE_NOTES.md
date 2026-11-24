ASUS Utility LITE 1.2.3

- Makes approved-launcher validation accept equivalent Task Scheduler XML: omitted default-enabled settings, boolean lexical forms, zero-duration formats, and account names resolving to the expected user SID.
- Keeps checks for the fixed executable, arguments, directory, elevated user, on-demand behavior, and battery operation.
- Reports the exact validation mismatch and records the returned task XML in Launcher setup.log when registration readback fails.
- Includes 74 offline checks, including first-time setup and normalized task exports.

The existing approved launch task can be reused when valid; reinstalling it is not necessary just because a previous app reported a readback error. Normal prompt-free launch still depends on Windows Task Scheduler access for the current administrator account.

Retains AMD/Intel Zephyrus compatibility, supplied logo, GPL-3.0 license, and G-Helper attribution. Other laptop models and live hardware writes still need physical validation. Independent app, not an official ASUS product.