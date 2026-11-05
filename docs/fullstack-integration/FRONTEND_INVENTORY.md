# Frontend inventory

Root frontend/. React19, React Router7, Vite6, TypeScript5.7; exact installed versions come from package-lock.json. npm lockfileVersion3, no Node engines pin. Validation host: Node24.21.0/npm12.2.0. npm ci installed199 packages.

Entry src/main.tsx with BrowserRouter. 80 screens/routes: src/routes/routeConfig.ts and docs/screen-manifest.json. Shell/Form/Toast/useDB in src/ui.tsx; AuthLayout; ScreenBoundary; themes/i18n/charts/report exports. All270 original frontend files including assets/docs/tests/package files are unchanged against origin/feature/react-frontend (SOURCE_INTEGRITY.json).

Current API transport: none. src/services.ts is a localStorage/sessionStorage mock database. Mock factories: src/mocks/createDatabase.ts, enrichDatabase.ts, prototypeData.ts. Login is local and screen routes are public; this is not SQL-backed session authentication. Public demo password literals are inherited prototype fixtures, not real credentials. Never use them for live APIs.

Scripts: dev (vite), build (tsc -b && vite build), typecheck, test (Node), lint (Prettier), test:e2e (Playwright), format. Existing Playwright config hard-codes Windows Edge and port5173/reuseExistingServer.

FS-001 adds vite.config.ts and .env.example only: localhost5173 strictPort, /api proxy to API_PROXY_TARGET (default https://localhost:7443), secure:true. DEV_HTTPS_CERT/KEY point to local PEM files, never committed. Without PEM configuration the prototype remains HTTP and is not compatible with Secure-cookie live auth. Existing screens do not call the proxy. The frontend branch's non-frontend changes are excluded and recorded in FRONTEND_EXCLUDED_DIFF.txt.
