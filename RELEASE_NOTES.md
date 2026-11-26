ASUS Utility LITE 1.3.0

- Adds the remembered Let Lite control profiles option to the tray menu. It temporarily stops only ArmouryCrateControlInterface and ArmouryCrateService, then requests the saved profile and restores accepted session overrides. It also takes control on subsequent launches when enabled.
- Restores only services Lite originally stopped when the option is disabled or the app exits. Closing to the tray retains control. Services already stopped by the user stay stopped; startup types and ASUS Optimization/hotkey services are unchanged.
- Adds a persistent local recovery journal, rollback for partial takeover failures, independent restoration attempts, and bounded waits. Service changes run away from the UI thread. Native stop requests do not cascade into unrelated dependent services.
- Detects competing Armoury Crate services and shows a clickable conflict warning in the existing feedback row. The window's dimensions and scrolling are unchanged. If services are restarted externally, the warning returns rather than repeatedly stopping them.
- User testing identified Armoury Crate as the source of the charger-triggered Performance switch: stopping it prevented that switch. Driver acknowledgement still does not verify the actual firmware mode. The new takeover/restoration feature requires live validation; offline checks use simulated services and hardware.
- Includes 178 offline checks, covering original service state, partial rollback, interrupted-session recovery, restoration failures, scope restrictions, and warning layout at 100%, 150%, and 200% DPI.

Exit the previous tray copy and reopen the installed executable, then right-click its tray icon and enable Let Lite control profiles once. Armoury Crate profile controls may be unavailable while this option is enabled. Disable the option or exit Lite to restore services Lite stopped.

Retains the supplied logo, GPL-3.0 licensing, and G-Helper attribution. Independent app, not an official ASUS product.
