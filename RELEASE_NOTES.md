ASUS Utility LITE 1.2.6

- Fixes Advanced failing to open after a fan-curve read reports an insufficient driver response buffer.
- Read-only ASUS requests grow their response buffer from 16 bytes up to a bounded 4096 bytes when the driver reports insufficient space or partial data. Hardware writes are never automatically retried.
- Decodes only bytes actually returned by the driver. Valid 16-byte curves may have zero padding; incomplete curves, invalid points, and unexpected extra payload remain rejected.
- Unavailable optional fan curves and power endpoints do not block the remaining Advanced controls. Access-denied errors remain visible.
- Includes 123 offline checks, including buffer growth, retry limits, write non-retry, response validation, and Advanced opening when optional reads fail.

Retains the fixed-size vertically scrolling window, protected wheel input, saved window placement, remembered performance selection, and approved per-user launcher. Exit the previous tray copy and reopen the installed app to use the update.

Retains AMD/Intel Zephyrus compatibility, supplied logo, GPL-3.0 license, and G-Helper attribution. Other laptop models and live hardware writes still need physical validation. Independent app, not an official ASUS product.