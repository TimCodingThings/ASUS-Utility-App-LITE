# Zephyrus compatibility

Version 1.2.0 selects controls from firmware capabilities rather than a list of approved model names. It is intended for AMD and Intel ROG Zephyrus families such as G14, G15, G16, and M16 using the ASUS System Control Interface driver. Model names alone do not guarantee a particular control is available.

| Feature | Adaptation |
| --- | --- |
| Performance | Shared ROG Silent/Balanced/Turbo writes; shows the last successfully selected mode. Missing or ambiguous profile readback does not disable otherwise working profile writes. |
| GPU | Eco/Standard/Optimized require an exact readable Eco state. Ultimate requires an exact readable MUX state. Older models without a MUX retain supported hybrid controls. MUX-only models can enter Ultimate and return to Standard. Restart still requires confirmation. |
| Fans | Each fan curve is validated for the selected profile. Only valid CPU/GPU/Middle curves appear, preserving endpoint mapping when a fan is absent. Zero fan speed remains a valid reading. Simple firmware profiles remain available when custom curves are unavailable. |
| Modern AMD power | Readable SPL, sPPT, and fPPT endpoints are offered independently. |
| Intel power | Readable PL1 and PL2 endpoints are offered; AMD fPPT is omitted. |
| Older AMD power | When all modern power endpoints are absent, readable CPU PPT and Total PPT endpoints are offered. These are not offered on Intel or unidentified CPUs. |
| Display | Finds the internal panel and its supported refresh rates; Auto chooses the highest on AC or 60 Hz/lowest on battery. No fixed 120 Hz requirement. |
| Battery | Windows battery enumeration and telemetry do not require a particular model. Charge-limit writes remain checked by firmware; absent/packed readback is not treated as a percentage or proof that writes are unsupported. |
| Temperature | ASUS readings are used when available; NVIDIA fallback also works when the laptop lacks Eco-mode readback. AMD-only models use ASUS temperature telemetry; no AMD GPU driver temperature binding is included. |

Detection reads capabilities without writing settings. Power editors retain a conservative 5–80 W range; detected current values outside this range are clamped for editing only, and nothing is applied until checked and submitted. A successful read is not proof that a write will be accepted; firmware rejection is reported.

## Validation

123 offline checks cover simulated modern AMD, older AMD, Intel, missing MUX/Eco, missing power limits, two-fan, and sparse fan layouts, plus 100%, 150%, and 200% DPI. These scenarios represent firmware layouts, not certification of individual laptop models. Preview flags `--sample-intel`, `--sample-legacy`, and `--sample-unavailable` can be combined with `--preview-advanced`, a DPI flag, and a final PNG path; these do not access real ASUS hardware.

Only the G14 GA403UP has provided physical ASUS/NVIDIA read diagnostics during development. Other models and live hardware writes still need physical validation. Run the included Diagnostics command on another Zephyrus to collect a local read-only report with model, CPU vendor, endpoint responses, and fan curves. The app does not upload it.

Protocol references and GPL attribution are in the main README. No extra driver, background service, bundled runtime, or dependency is required.
