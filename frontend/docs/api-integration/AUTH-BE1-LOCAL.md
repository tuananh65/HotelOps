# HotelOps: Front-end integration against BE1 Authentication

Source branches: `fron-end` (this change) and `feature/BE1-authentication` (read-only).
BE1 ASP.NET Core 10 remains the API contract authority. **Only frontend is changed.**

## Windows quick start
1. Install Node.js 22+, Git, .NET 10 SDK, SQL Server Developer/Express and SSMS.
2. Create a separate development HotelOpsDB with the existing SQL scripts and guarded authentication schema update. Back up data; do not run against the shared database.
3. Start BE1 in a separate checkout, configure ConnectionStrings:HotelOpsDb with local User Secrets, trust the developer HTTPS certificate, run `dotnet run --project src/HotelOps.Api/HotelOps.Api.csproj --launch-profile https`; verify `https://localhost:7019/api/health/database`.
4. From this repo's `frontend` directory run `npm ci`, then `npm run typecheck`, `npm test`, `npm run build`, `npm run dev`.
5. Open `https://localhost:5173/login`. The HTTPS development certificate may require browser confirmation.
6. Browser F12 > Network > Fetch/XHR: `GET /api/auth/csrf` 200, `POST /api/auth/login` 200, `GET /api/auth/session` 200 after refresh, `POST /api/auth/change-password` 200, `POST /api/auth/logout` 204. Valid credentials and a compatible Identity V3 test password hash are essential.

## Contract and safety
- Cookie auth with credentials included; CSRF bootstrap before POST login and updated CSRF token from SessionDto for authenticated POST requests.
- `GET /api/auth/session` used to restore authentication after refresh; server owns session and cookie lifecycle.
- The backend response is `{ user:{id,username,name}, expiresAt, absoluteExpiresAt, mustChangePassword, csrfToken }`.
- The original frontend expects roles, branches, departments and permission grants. BE1 does NOT provide them. The temporary adapter uses empty grants, denying protected business features. **Do not invent permissions.**
- Local Vite HTTPS proxy avoids relaxing backend production CORS / SameSite / Secure behavior.
- Do not set `VITE_API_BASE_URL` to an external origin when using the proxy; keep the default empty URL.
- SQL seed data may use bcrypt placeholders, which BE1 Identity V3 PasswordHasher will reject; use a correctly hashed test account on a standalone development DB.
- BE1 current auth branch is not a complete RBAC or business API implementation.
- No frontend/backend/SQL end-to-end test has been performed here; run the commands above and check the Network pane.

## What changed
`src/api/endpoints.ts`, `src/api/modules.ts`, `src/api/types.ts`, `src/api/http.ts`,
`src/services.ts`, new `src/api/authAdapter.ts`; Vite proxy and local environment defaults;
development SSL dependency and lockfile; authentication contract unit tests.
