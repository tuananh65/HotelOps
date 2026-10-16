# HotelOps — public UI/UX prototype

High-fidelity React + TypeScript frontend for Duc Vuong Hotel operations. This refactors the existing HotelOps project with a content-first operations interface: navy navigation, white/cool-gray surfaces, dark text and blue interaction accents.

**80 required screens = 80 separate screen-level `.tsx` files.** The screen directory and design-system showcase are additional utility routes, outside this count.

## Review

- `/` opens the public screen directory at `/dev/screens`.
- `/dev/screens`: all 80 screens, EN/VI names and descriptions, FE/module/type filters, search, UC identifiers and Copy Link.
- `/dashboard`: populated operational overview and status charts.
- `/dev/design-system`: color families, typography, spacing, actions and status badges.
- All required routes work directly, including refresh and deep links to populated detail examples.

No account, login session or permission is required. Login/password/permission screens are presentation examples. There is no backend, API or external database. Fictional data and UI mutations persist only in the reviewing browser. Clear the `hotelops-public-prototype-v1` localStorage entry to restore the sample dataset. The EN/VI setting persists under `hotelops-language`.

## Run locally

```sh
npm ci
npm run dev
```

Open `http://127.0.0.1:5173`. Keep the Vite process running while reviewing. A refused connection means the local dev server is not running. The localhost address is for development, not a public share link.

## Public deployment

Build first:

```sh
npm run build
```

The deployable frontend is `dist/`. No secrets or environment variables are required.

**Netlify:** import this repository, or upload `dist/` using a manual deployment. `netlify.toml` specifies the build and SPA fallback; `dist/_redirects` also supports manual uploads.

**Vercel:** import the repository with the Vite framework preset. Build command: `npm run build`; output directory: `dist`. The included `vercel.json` rewrites browser routes to `index.html` while preserving asset requests.

**Cloudflare Pages:** build with `npm run build`, output `dist`. The included `_redirects` provides the SPA fallback.

After deployment, share the host's HTTPS root URL. It opens the 80-screen directory. Copy Link uses the current origin, so links copied from the hosted prototype point to that deployment. Verify `/rooms`, a detail URL from the directory and `/dev/design-system` after refresh.

No public deployment URL has been created in this workspace. Hosting configuration and build artifacts are provided; publication requires a hosting account or an authorized deployment connection.

## Source of truth

`Report-3_Software Requirement Specification.docx` and `UC_Screen List.docx` are the available requirement documents. Their extracted contents are in `docs/requirements/`. `src/catalog.json` retains the 80-screen, FE and UC mapping; `docs/screen-matrix.md` and `docs/screen-routes.md` record files and example routes. Source requirements describe operational roles and business rules; the current public-prototype instruction intentionally removes authentication and permission enforcement. Status transitions and mock validations still illustrate business flows.

The latest operations design supersedes the public hotel marketing references. Auth screens use professional navy surfaces; lists prioritize records, compact KPI strips and one-row filter toolbars.

## Validation

```sh
npm run typecheck
npm test
npm run test:e2e
npm run lint
npm run build
node scripts/verify-screens.mjs
```

Browser tests use installed Microsoft Edge; adjust `playwright.config.ts` for another installation. Tests cover all 80 routes without a session, all 80 Vietnamese titles, language persistence, populated tables/reports, mobile width and mock user creation/editing. Service tests cover data references and persistence, cleaning/inspection, stock posting, material approval/issue and recurring maintenance. This is prototype validation, not production-security verification.

See `docs/design-system.md` for current tokens and component mapping, `docs/ux-improvements.md` for the content-first refactor and `docs/verification/content-first/` for visual verification. Earlier refactor reports describe historical behavior and are superseded by this public prototype.
