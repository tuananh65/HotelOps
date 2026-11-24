---
title: Room labels and collapsible alert popups
date: 2026-11-24
summary: "Updated local Production room labels, action colors and collapsible popup alerts; five focused browser suites passed."
---

# Room labels and collapsible alert popups

# Nh?n ph?ng v? popup th?ng b?o ? 24/11/2026

?? c?p nh?t Production c?c b? https://127.0.0.1:5080/.

## K?t qu?

- BLOCKED hi?n th? ?B?o tr?? tr?n tab, b? l?c, b?ng, badge v? chi ti?t ph?ng. Kh?ng ??i m? tr?ng th?i ho?c nghi?p v?.
- H?y ??t ph?ng v? n?t x?c nh?n h?y m?u ??; Chuy?n ph?ng v? n?t x?c nh?n chuy?n m?u v?ng, c? hover/focus/disabled v? ch? t??ng ph?n.
- C?c alert/notice trong compact d?ng popup g?c d??i v?i Thu g?n/M? ra. N?i dung m?i t? m?. Nhi?u popup x?p theo t?ng dialog; m?n h?nh th?p cu?n d?c trong popup. Gi? retry, form v? x?c nh?n v??t gi?; kh?ng che n?t ??ng drawer.

## T?p thay ??i trong l??t n?y

M?i: `frontend/src/compact/alert-popup.tsx`, `alert-popup.css`, `room-actions.css`; `frontend/tests/compact-alert-popups.mjs`, `verify-room-alerts-production.mjs`.

S?a giao di?n: `rooms.tsx`, `ui.tsx` v? thay container th?ng b?o trong `account-management.tsx`, `admin.tsx`, `app.tsx`, `assets.tsx`, `auth.tsx`, `catalog.tsx`, `documents.tsx`, `hotel-workspace.tsx`, `housekeeping-checklists.tsx`, `insights.tsx`, `internal-settings.tsx`, `inventory-documents.tsx`, `maintenance-plans.tsx`, `paper-inspection.tsx`, `personal-profile.tsx`, `reset-password-form.tsx`, `role-permissions.tsx`, `room-issues.tsx`, `templates.tsx`, `work.tsx`, `workday-assignment.tsx`, `workday-settings.tsx`, `workday.tsx`. C?c t?p n?y thu?c `frontend/src/compact/`.

S?a test: `compact-reception-rooms.mjs`, `compact-operations-manager.mjs` ??i nh?n; `compact-workday-scheduling.mjs` ph?n bi?t popup l?i v?i popup c?nh b?o v??t gi?. C?p nh?t `docs/ui/compact-ux.md` v? plan `plans/261124-0921-room-alerts/plan.md`. Gi? c?c thay ??i c? s?n c?a phi?n tr??c; kh?ng commit/push.

## Ki?m ch?ng

| Ki?m tra | K?t qu? |
|---|---|
| TypeScript `tsc -b`, Vite production build | Passed |
| Popup: nhi?u l?i, retry, b?n ph?m, thu g?n/m? ra, form kh?ng g?i ngo?i ? mu?n, login | Passed |
| Responsive 1440/375/320px, viewport th?p 140px, n?t ??ng drawer | Passed |
| Lu?ng ph?ng l? t?n | Passed |
| Ph?n ca/v??t gi?/x?c nh?n l?i sau xung ??t | Passed |
| Ki?m tra gi?y v? l?ch s? theo ng?y | Passed |
| C?u h?nh n?i b?, toggle, l?i concurrency v? m?c ??nh y?u c?u | Passed |
| B? qu?n l? v?n h?nh c? | Failed t?i k? v?ng 9 menu so v?i 10 th?c t?; m?c S? c? ph?ng ?? c? t? tr??c, kh?ng do popup. C?c b??c sau ch?a ch?y. |
| Production th?c | HTTP 200, bundle m?i 200, TLS h?p l?, login hi?n th?, API ch?a ??ng nh?p 401, kh?ng l?i JavaScript |
| API health ch? ??c | 200, schema 2.1.0, 40 b?ng |
| .NET build/test, DB workflow | Not Run: kh?ng s?a C#/API/database trong l??t n?y |

C?c ki?m th? nghi?p v? giao di?n d?ng API gi? l?p, kh?ng ghi d? li?u primary. Kh?ng c?n thay schema, grants ho?c sao l?u database cho b?n frontend n?y. Kh?ng tuy?n b? ?? ch?y to?n b? ki?m th? h? th?ng.

R? so?t ??c l?p t?m ???c hai l?i v? tr?/chi?u cao popup; ?? s?a v? ki?m th? l?i. Test ph?n ca l?n ??u l?i selector do gi? c? hai role=alert, ?? s?a selector c? th? v? ch?y Passed. M?t l?nh ch?nh test d?ng nh?m ???ng d?n `frontend/frontend`, kh?ng s?a ???c t?p; ?? s?a ??ng ???ng d?n. Vite c?nh b?o outDir ngo?i frontend kh?ng t? x?a (th? m?c release ri?ng, c? ch? ?); Git c?nh b?o LF/CRLF.

## Runtime

- Frontend: `artifacts/production-261124-room-alerts/frontend`, bundle `index-DpERXcmM.js`, launcher 27804, listener 19920, c?ng 5080.
- API gi? PID 40444/c?ng 5769 t? release internal-settings.
- ?? d?ng frontend c? v? preview t?m 5082; kh?ng thay ti?n tr?nh Demo kh?ng li?n quan.
- B?ng ch?ng: `artifacts/room-alert-popups/verification.json`, `artifacts/production-261124-room-alerts/verification.json` v? c?c b?o c?o browser trong `artifacts/reception-rooms`, `workday-scheduling`, `paper-inspection-ui`, `internal-settings-ui`.

> Historical work record — not durable authority. Prefer docs/specs/ADRs for current decisions.
