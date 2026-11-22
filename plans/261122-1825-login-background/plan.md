# Login background

Brief: Product; hotel staff signing in on desktop/mobile. Preserve the current centered white form, Segoe UI and green accent (#21665e). Use the exact user-provided hotel photo as a full-screen cover background with a subtle dark overlay. No animation or authentication changes. Variance 1, motion 1, density 4.

- [x] Copy the supplied JPEG into public assets and set the login background.
- [x] Build and inspect actual login rendering at 375/768/1440 without signing in or modifying DB.

Build passed. Original and copied JPEG SHA256 match. First render attempt captured connection-refused pages because the existing frontend had stopped; those results were rejected. Restarted Vite 5186 (node 6424, launcher 35028); final render check has no errors/warnings and screenshots visually verified. Keep frontend for user. Browser closed. Report: plans/reports/test-261122-1825-login-background.md.
