ASUS Utility LITE 1.2.7

- Reads firmware fan curves separately from validating user-created curves. Real GA403UP defaults include repeated temperatures, a terminal raw value of 255, and low default fan speeds; these no longer disable the editor.
- Builds an editable draft with distinct temperatures in the 1-100 C range and the existing minimum terminal fan speed. Any changed defaults are labeled Firmware draft adjusted. Review before Apply.
- Loading a draft does not write hardware. Apply still validates all points before submitting the selected fan curve.
- Preserves bounded driver read-buffer retries and optional-read fallback from v1.2.6. Hardware writes are never automatically retried.
- Saves a bounded local Fan curve diagnostics.log to help investigate firmware replies; no logs are uploaded.
- Includes 133 offline checks, including captured CPU, GPU, and Middle fan replies from the GA403UP.

Exit the previous tray copy and reopen the installed app to use this update. Retains the fixed-size scrolling interface, protected wheel input, saved position and performance selection, and approved launcher.

GPL-3.0; retains the supplied logo and G-Helper attribution. Other laptop models and live hardware writes are not comprehensively validated. Independent app, not an official ASUS product.