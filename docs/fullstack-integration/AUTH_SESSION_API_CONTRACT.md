# Auth/session contract

Preserve develop cookie/SQL-session architecture, no JWT migration. AuthController: GET /api/auth/csrf; POST /api/auth/login; GET /api/auth/session; POST /api/auth/logout; POST /api/auth/change-password. CurrentUserAccessor requires request-local validated SessionSnapshot. Client actor/role/branch data never grants authority.

Unsafe /api requests require X-CSRF-TOKEN. Bootstrap returns csrfToken/headerName; __Host-HotelOps.Antiforgery is HttpOnly/Secure/SameSite=Lax. Refresh CSRF after login/change-password/identity replacement. Future frontend transport must send credentials:include; real session secrets must not go in localStorage. Session/account/employment/branch checks remain independent. OWN is not BRANCH; SYS_ADMIN name is not a bypass. Resource authorization requires a trusted backend resolver.

Handle ProblemDetails and distinguish401/403. Existing CSRF middleware can return400 before authorization. Do not blindly retry mutations without approved idempotency semantics.

Frontend currently lacks real transport/auth state/CSRF refresh/protected routes/401-403 handling. Vite proxy prepares same-origin development with HTTPS and does not add broad CORS or change cookies. Cross-origin deployment needs DEV3 review. A fallback/public-endpoint/UTC patches are NOT applied under the user's decision; unannotated endpoints are not certified protected by a fallback here. Recovered controller source is inactive. No live HTTPS/SQL acceptance in FS-001.
